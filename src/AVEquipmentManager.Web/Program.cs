using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AVEquipmentManager.Web;
using AVEquipmentManager.Web.Auth;
using AVEquipmentManager.Web.Services;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5184";

// ── Authorization core (enables [Authorize] in Blazor) ───────────────────────
builder.Services.AddAuthorizationCore();

// ── Auth state provider ───────────────────────────────────────────────────────
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<CustomAuthStateProvider>());

// ── Bearer token handler for authenticated API calls ─────────────────────────
builder.Services.AddTransient<BearerTokenHandler>();

// ── AuthService uses a plain HttpClient (login doesn't need a token) ─────────
builder.Services.AddHttpClient<AuthService>(client =>
    client.BaseAddress = new Uri(apiBaseUrl));

// ── All other API services use a client that auto-attaches the JWT ────────────
builder.Services.AddHttpClient<EquipmentService>(client =>
    client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();

builder.Services.AddHttpClient<StaffService>(client =>
    client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();

// Maintenance Records (Tickets) — re-enabled as the in-system maintenance/repair transaction workflow.
builder.Services.AddHttpClient<TicketService>(client =>
    client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();

// Disposal transactions
builder.Services.AddHttpClient<DisposalService>(client =>
    client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();

// Acquisition transactions (Plan → Acquire → Deploy)
builder.Services.AddHttpClient<AcquisitionService>(client =>
    client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();

// KPIs
builder.Services.AddHttpClient<KpiService>(client =>
    client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();

builder.Services.AddHttpClient<ChatService>(client =>
    client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();

builder.Services.AddHttpClient<AdminService>(client =>
    client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();

// ── MudBlazor ─────────────────────────────────────────────────────────────────
builder.Services.AddMudServices();

await builder.Build().RunAsync();
