# AVAMS Security Notes

## Configuration secrets — required out-of-band

The API will refuse to start unless the JWT signing key is supplied through a
mechanism other than `appsettings.json`. Pick one of the three options below.

### Option 1 — user-secrets (recommended for local development)

```powershell
cd src\AVEquipmentManager.API
dotnet user-secrets init                                # one-time, per project
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"

# Optional: override the seeded passwords (see Seeds section below)
dotnet user-secrets set "Seeds:Admin:Password"      "$(openssl rand -base64 24)"
dotnet user-secrets set "Seeds:Supervisor:Password" "$(openssl rand -base64 24)"
dotnet user-secrets set "Seeds:AVStaff:Password"    "$(openssl rand -base64 24)"
dotnet user-secrets set "Seeds:ITSpec:Password"     "$(openssl rand -base64 24)"
```

User-secrets are stored outside the repo in
`%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json` and are never
committed.

### Option 2 — environment variables (production / container)

```bash
export Jwt__Key="$(openssl rand -base64 48)"
export Seeds__Admin__Password="$(openssl rand -base64 24)"
# ...
dotnet run --project src/AVEquipmentManager.API
```

Double-underscore (`__`) is the standard `IConfiguration` separator on Linux.

### Option 3 — managed secret store

For production deployments, wire `Jwt:Key` through Azure Key Vault, AWS Systems
Manager Parameter Store, HashiCorp Vault, or equivalent. The same configuration
binding paths apply.

## Seeded accounts

The SeedData routine creates four default accounts on first run (`admin`,
`supervisor`, `avstaff`, `itspec`). If you do not override their passwords via
configuration, the routine generates a cryptographically random 32-character
password for each account and writes it **once** to the API console with a
prominent warning. **Capture those passwords on first boot and rotate them
through the Admin → Users page before any real deployment.**

If you want fixed passwords for the capstone demo, set them explicitly:

```powershell
dotnet user-secrets set "Seeds:Admin:Password" "<your-demo-password>"
```

## Other hardening applied in the Phase 3 patch pass

| Finding | CWE | Fix |
|---|---|---|
| #1 — Hardcoded JWT key | CWE-321 / CWE-798 | Key removed from `appsettings.json`; fail-fast in `Program.cs` if absent; minimum 32 chars enforced. |
| #2 — Weak seed passwords | CWE-521 / CWE-798 | `SeedData.cs` reads `Seeds:<role>:Password` from config; generates secure random + logs once if not set. |
| #3 — JWT in localStorage | CWE-922 | Switched to `sessionStorage` in `AuthService`, `BearerTokenHandler`, `CustomAuthStateProvider`. Cleared on tab close. |
| #4 — No HTTPS redirect / HSTS | CWE-319 | `UseHttpsRedirection()` + `UseHsts()` added in `Program.cs`. |
| #5 — Login timing oracle | CWE-204 / CWE-208 | `AuthController.Login` always invokes `BCrypt.Verify` against a known hash when the user is missing, eliminating the latency difference. |
| #6 — No login rate limit | CWE-307 | `AddRateLimiter()` with a fixed-window policy of 5 attempts per minute per IP on `/api/auth/login`. |
| #7 — `MarkupString` XSS sinks | CWE-79 | Razor pages (`Archive`, `Disposals`, `Staff`) no longer wrap user-controlled fields in `MarkupString`. |
| #8 — Wildcard package versions | CWE-1104 | All `Version="8.0.*"` / `7.*` floats pinned to concrete patch versions. |

## What is NOT yet addressed (deferred Medium / Low findings)

| Finding | Status | Notes |
|---|---|---|
| #9 — Permissive CORS allowlist | Open | Trim down to the actual production origin; remove stale Vite ports. |
| #10 — Weak password complexity | Open | Add complexity validator and breach-list check to `RegisterUserDto`. |
| #11 — No account-disable mechanism | Open | Add `IsActive`, `LockedUntilUtc`, `FailedLoginCount` columns to User. |
| #13 — Swagger gated by environment only | Open | Add explicit `EnableSwagger` config flag. |
| #14 — No chatbot rate limit | Open | Apply the rate limiter middleware to `ChatController` as well. |
| #15 — Missing security headers | Open | Add `X-Content-Type-Options`, `X-Frame-Options`, CSP middleware. |
| #16 — Legacy "Staff" role still accepted | Open | One-time DB migration to remap `Staff` → `AVStaff` then remove the legacy string. |
| #17 — Username enumeration on Register | Open | Return generic 200 OK regardless of conflict; deliver result via separate email. |
| #18 — JWT issued without `nbf` | Open | Add `notBefore: DateTime.UtcNow` in `GenerateJwt`. |
| #19 — Stale `ChatController` XML docstring | Open | Cosmetic — update comment to match the 4-role model. |

Open issues are tracked for a follow-up patch pass after the panel defence.
