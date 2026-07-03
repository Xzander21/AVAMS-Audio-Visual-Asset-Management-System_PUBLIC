using System.Text;
using System.Threading.RateLimiting;
using AVEquipmentManager.API.Data;
using AVEquipmentManager.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
// Singleton interceptor that stamps every entity's RowVersion column before
// SaveChanges, giving us SQLite-compatible optimistic concurrency.
builder.Services.AddSingleton<RowVersionInterceptor>();

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=avequipment.db")
           .AddInterceptors(sp.GetRequiredService<RowVersionInterceptor>()));

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ChatbotService>();

// Transaction-proof lifecycle services. Each wraps multi-step writes
// in an explicit IDbContextTransaction with try/catch/rollback.
builder.Services.AddScoped<IDisposalLifecycleService,    DisposalLifecycleService>();
builder.Services.AddScoped<IAcquisitionLifecycleService, AcquisitionLifecycleService>();
builder.Services.AddScoped<ITicketLifecycleService,      TicketLifecycleService>();

// Read-only ITIL analytics (per-asset lifespan exhaustion detail).
builder.Services.AddScoped<IItilAnalyticsService, ItilAnalyticsService>();

// ── JWT Authentication ─────────────────────────────────────────────────────────
// Finding #1 (CWE-321 / CWE-798) fix: the signing key is no longer stored in
// appsettings.json. It must be provided out-of-band via one of:
//   • Environment variable:  Jwt__Key=<random>     (Linux / container)
//   • user-secrets:          dotnet user-secrets set "Jwt:Key" "<random>"
//   • CI/CD secret store:    Azure Key Vault, AWS SSM, etc.
// The application fails fast at startup if the key is missing or too weak.
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "Jwt:Key is not configured. Set it via environment variable Jwt__Key " +
        "or run: dotnet user-secrets set \"Jwt:Key\" \"<random 32+ char string>\". " +
        "Storing the key in appsettings.json is forbidden by the security audit (CWE-321).");
}
if (jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key must be at least 32 characters for HMAC-SHA256 security. " +
        "Generate one with: openssl rand -base64 48");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            RequireExpirationTime    = true,   // Finding #12 hardening
            RequireSignedTokens      = true,   // Finding #12 hardening
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew                = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ── Rate Limiting (Finding #6 / CWE-307 patch) ───────────────────────────────
// Login endpoint policy: 5 attempts per IP per minute, no queueing. After the
// fifth failed POST in a rolling window the client receives HTTP 429.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
    {
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit          = 5,
                Window               = TimeSpan.FromMinutes(1),
                QueueLimit           = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment    = true
            });
    });
});

// ── Controllers ───────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── Swagger with JWT Bearer support ──────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AVAMS API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Paste your JWT token here (no 'Bearer ' prefix needed)."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── CORS — allow Blazor WASM client ──────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy
            .WithOrigins(
                builder.Configuration["CorsOrigins:BlazorClient"] ?? "http://localhost:5072",
                "https://localhost:7022",
                "http://localhost:5072",
                "https://localhost:7173",
                "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// ── Migrate & seed ────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Transaction-proofing pragmas:
    //  - foreign_keys: SQLite has FK enforcement off by default on some builds.
    //  - journal_mode=WAL: readers don't block on writers, so KPI queries can
    //    run while a lifecycle transaction is in flight on the same file.
    db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode = WAL;");

    // Finding #2 / CWE-521 patch: the seeder now reads passwords from
    // configuration and falls back to cryptographically random ones,
    // logging them once on first boot. Pass config + logger so it can.
    var seedLogger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("AVAMS.Seed");
    SeedData.Initialize(db, builder.Configuration, seedLogger);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Finding #4 / CWE-319 patch: in production, force browsers to remember to
    // use HTTPS for one year, including subdomains. HSTS is intentionally OFF
    // in development to avoid breaking the local http://localhost workflow.
    app.UseHsts();
}

// Finding #4 / CWE-319 patch: redirect any plaintext HTTP request to HTTPS.
// Safe in development too (the launch profile binds both http and https ports).
app.UseHttpsRedirection();

app.UseCors("AllowBlazorClient");
app.UseRateLimiter();      // ← Finding #6: apply rate-limiting middleware
app.UseAuthentication();   // ← must come before UseAuthorization
app.UseAuthorization();
app.MapControllers();

app.Run();
