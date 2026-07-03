# AVAMS — AV Equipment Management System

A complete Audio-Visual asset management system built for the AV Department of an
institution, with ITIL-aligned lifecycle workflows, transaction-proof
backend operations, and a four-role authorisation model.

**Stack:** Blazor WebAssembly · MudBlazor · ASP.NET Core 8 Web API · EF Core 8 · SQLite

```
asset-manager-chatbot.sln
└── src/
    ├── AVEquipmentManager.Shared/   Models, DTOs, Enums (Class Library)
    ├── AVEquipmentManager.API/      ASP.NET Core Web API + EF Core + SQLite
    └── AVEquipmentManager.Web/      Blazor WebAssembly (MudBlazor UI)
```

---

## 🚀 First-run setup — EVERY teammate must do this once

The API is hardened against committing secrets (per the Phase 3 security audit).
The JWT signing key is **not in the repo** and the API will refuse to start without it.
Each contributor sets the key on their own machine via .NET's `user-secrets`,
which stores it in `%APPDATA%\Microsoft\UserSecrets\` — outside the repository.

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git
- (Recommended) [VS Code](https://code.visualstudio.com/) with the C# Dev Kit extension
- (Optional) [DB Browser for SQLite](https://sqlitebrowser.org/) for inspecting the database

### Step 1 — Clone

```powershell
git clone <repo-url> asset-manager-chatbot
cd asset-manager-chatbot
```

### Step 2 — Trust the local HTTPS development certificate (one-time per machine)

```powershell
dotnet dev-certs https --trust
```

A Windows dialog asks for confirmation — click Yes. This stops browsers from
blocking the API's self-signed HTTPS certificate.

### Step 3 — Set the JWT signing key (MANDATORY — API won't start without it)

```powershell
cd src\AVEquipmentManager.API
dotnet user-secrets init

# Generate a 48-character random key and store it outside the repo
$key = -join ((33..126) | Get-Random -Count 48 | ForEach-Object {[char]$_})
dotnet user-secrets set "Jwt:Key" "$key"
```

> The key never leaves your machine, never reaches the repo. Each teammate has
> their own. Your tokens won't be verifiable on a teammate's machine and vice
> versa — that's the point.

### Step 4 — Set the seed account passwords (optional but recommended)

If you skip this step, the seeder generates cryptographically random passwords on
first boot and prints them once to the API console (look for the warning banner).
For demo predictability, set them explicitly:

```powershell
# Still inside src\AVEquipmentManager.API
dotnet user-secrets set "Seeds:Admin:Password"      "Admin@CapstoneDemo2026!"
dotnet user-secrets set "Seeds:Supervisor:Password" "Supervisor@CapstoneDemo2026!"
dotnet user-secrets set "Seeds:AVStaff:Password"    "AVStaff@CapstoneDemo2026!"
dotnet user-secrets set "Seeds:ITSpec:Password"     "ITSpec@CapstoneDemo2026!"
```

Verify everything landed:

```powershell
dotnet user-secrets list
```

You should see your `Jwt:Key` and the four `Seeds:*:Password` entries.

### Step 5 — Restore packages and build once

```powershell
cd ..\..    # back to repo root
dotnet build asset-manager-chatbot.sln
```

You're now ready to run.

---

## ▶️ Running the application

Open **two PowerShell terminals**, both at the repo root:

### Terminal 1 — API

```powershell
dotnet run --project src\AVEquipmentManager.API --launch-profile https
```

Wait for:

```
Now listening on: https://localhost:7127
Now listening on: http://localhost:5033
```

### Terminal 2 — Web (Blazor WASM)

```powershell
dotnet run --project src\AVEquipmentManager.Web --launch-profile https
```

Wait for:

```
Now listening on: https://localhost:7022
```

### Open in browser

```
https://localhost:7022
```

You'll see the login page. Use one of these accounts (case-sensitive usernames):

| Role | Username | Default password |
|---|---|---|
| Admin (AV Head) | `admin` | `Admin@CapstoneDemo2026!` |
| Supervisor (approves) | `supervisor` | `Supervisor@CapstoneDemo2026!` |
| AVStaff (operational) | `avstaff` | `AVStaff@CapstoneDemo2026!` |
| IT Specialist (submits tickets only) | `itspec` | `ITSpec@CapstoneDemo2026!` |

> If you skipped Step 4, the passwords are whatever the seeder printed to your
> API console banner. Capture them from there.

### Default URLs

| Project | HTTP | HTTPS (use this) |
|---|---|---|
| API     | http://localhost:5033 | **https://localhost:7127** |
| API Swagger | — | **https://localhost:7127/swagger** |
| Web     | http://localhost:5072 | **https://localhost:7022** |

---

## 👥 Role matrix

The system enforces strict separation of duties — Admin proposes, Supervisor approves.

| Capability | Admin | Supervisor | AVStaff | ITSpecialist |
|---|:-:|:-:|:-:|:-:|
| View dashboard, equipment, staff | ✅ | Approvals only | ✅ | ❌ |
| Equipment / Staff CRUD | ✅ | ❌ | ✅ | ❌ |
| Open / cancel disposals & acquisitions | ✅ | ❌ | ✅ | ❌ |
| **Approve disposals / Mark Disposed** | ❌ | ✅ | ❌ | ❌ |
| **Deploy acquisitions (adds Equipment)** | ❌ | ✅ | ❌ | ❌ |
| Acknowledge / Resolve tickets | ✅ | ✅ | ✅ | ❌ |
| Close tickets | ✅ | ✅ | ❌ | ❌ |
| Delete Forever (purge from Archive) | ✅ | ❌ | ❌ | ❌ |
| Manage user accounts | ✅ | ❌ | ❌ | ❌ |
| Submit Ticket (and view own) | ✅ | ❌ | ✅ | ✅ |

---

## 🛡️ Security model — what's in place

Per the Phase 3 security audit (see `SECURITY.md`):

- **JWT key never in source** — fail-fast on startup if not configured.
- **Seed passwords never hard-coded** — configuration-driven with crypto-random fallback.
- **HTTPS redirect + HSTS** enabled.
- **Rate limiting** on `/api/auth/login` (5 attempts per minute per IP).
- **Constant-time login** — dummy BCrypt run on missing-user path to defeat timing oracles.
- **JWT in sessionStorage** (cleared on tab close, narrower XSS window than localStorage).
- **XSS sinks closed** in Razor confirmation dialogs.
- **RowVersion concurrency tokens** on all state-changing entities.
- **Pinned package versions** for reproducible builds.

---

## ⚙️ Architecture highlights

- **ACID transactions** wrap every multi-row write through three lifecycle services
  (`DisposalLifecycleService`, `AcquisitionLifecycleService`, `TicketLifecycleService`).
  Each method opens an explicit `IDbContextTransaction`, performs all writes, then
  commits or rolls back atomically.
- **Append-only `LifecycleLogs` audit table** written inside the same transaction as
  every state change. Audit trail and live record can never disagree.
- **Optimistic concurrency** via `[Timestamp] RowVersion` on Disposal, Acquisition,
  Equipment, and Ticket. Two simultaneous edits trigger
  `DbUpdateConcurrencyException` and a clean rollback.
- **Unique filtered index** on `Disposals(EquipmentId, Status WHERE Pending/Approved)`
  enforces at the DB layer that no equipment has two simultaneously open disposals.
- **PRAGMA `journal_mode = WAL`** so analytics queries don't block on in-flight
  lifecycle transactions; `PRAGMA foreign_keys = ON` for FK enforcement.

---

## 🆘 Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| API crashes at startup: *"Jwt:Key is not configured"* | You skipped Step 3 | Run the `dotnet user-secrets set "Jwt:Key" ...` command |
| API crashes: *"Jwt:Key must be at least 32 characters"* | The key you set is too short | Regenerate with the PowerShell one-liner in Step 3 |
| Web shows *"TypeError: Failed to fetch"* | Web is hitting `http://` and the API issues a 307 redirect that the browser cancels | Make sure the Web is launched with `--launch-profile https` and `wwwroot/appsettings.json` has `"ApiBaseUrl": "https://localhost:7127"` |
| Browser shows untrusted certificate warning | You skipped Step 2 | Run `dotnet dev-certs https --trust` |
| Login fails with *"Invalid username or password"* even with the right password | Either you typed the username with a different case (e.g. `Admin` vs `admin`), or the SQLite file has stale rows from before you set the password | Type the username in lowercase. If still failing, stop the API and `Remove-Item src\AVEquipmentManager.API\avequipment.db*`, then restart |
| Login returns HTTP 429 | Rate limiter caught you (5 attempts per minute per IP) | Wait 60 seconds |
| Migration error on first launch about `RowVersion` / `LifecycleLogs` | The TxProofing migration somehow isn't applying | `dotnet ef database update --project src\AVEquipmentManager.API --startup-project src\AVEquipmentManager.API` |
| `MSBUILD : error MSB1011` | Running `dotnet build` from a folder with multiple project files | Use `dotnet build asset-manager-chatbot.sln` from the repo root |
| The API console prints a *"AVAMS — generated seed credentials"* banner | You skipped Step 4 (passwords not configured), so the seeder generated random ones | Capture them from the console, log in once, rotate via Admin → Users; or set passwords in Step 4 and restart from a fresh DB |

---

## 📝 Modifying seed data

Sample staff and equipment seed data is in `src/AVEquipmentManager.API/Data/SeedData.cs`.
Each block uses an `if (!context.X.Any())` guard, so seed only runs against an empty
DB. To force a re-seed, stop the API and delete the database files:

```powershell
Remove-Item src\AVEquipmentManager.API\avequipment.db, `
            src\AVEquipmentManager.API\avequipment.db-shm, `
            src\AVEquipmentManager.API\avequipment.db-wal -ErrorAction SilentlyContinue
```

Then restart the API. The database is rebuilt from migrations and the seeders
re-populate it.

---

## 🤝 Contributing

When you make changes that touch the schema, generate a migration before pushing:

```powershell
dotnet ef migrations add <descriptive_name> `
  --project src\AVEquipmentManager.API `
  --startup-project src\AVEquipmentManager.API
```

The migration file goes in `src/AVEquipmentManager.API/Data/Migrations/` and **must
be committed**. `Database.Migrate()` runs at every API startup, so any teammate
who pulls your change will get the schema update automatically — no manual EF
commands needed on their end.

---

## 📂 Related documents

- `SECURITY.md` — what was patched in the Phase 3 audit and what's still open.
- `TRANSACTION_PROOFING_BLUEPRINT.md` — the design document behind the ACID
  transaction work.
- `chapter3-artifacts/` — Chapter 3 paper artifacts (use case diagrams, DBML
  schema, Mermaid activity & DFD diagrams, data dictionary).
- `prototype/` — interactive click-through prototype (no backend required).
