# Polhem — Samples

**English** | [繁體中文](README.zh-TW.md)

A collection of minimal, runnable Polhem demos. Each demo has a single focus and consumes the libraries via `ProjectReference` directly from `src/` (no NuGet round-trip) — changes to library code are immediately reflected.

> Solution: [`samples/Polhem.Samples.slnx`](Polhem.Samples.slnx) (kept separate from the main `Polhem.slnx` so it never weighs down CI or main-solution build time).

## See Polhem running in 30 seconds

```bash
# Terminal 1 — start the JSON-RPC API host
cd samples/QuickStart.Server
dotnet run                          # listens on http://localhost:5050

# Terminal 2 — connect and invoke the Echo BO
cd samples/QuickStart.Console
dotnet run
```

You should see `response : echo: hello from QuickStart.Console` in Terminal 2.

To watch Blazor components render a `FormSchema` and drive a Login + Employee CRUD flow:

```bash
# Blazor Server (in-process LocalApiProvider, no HTTP round-trip)
cd samples/Blazor.Server.Demo
dotnet run                          # → http://localhost:5055
```

Sign in with **`demo / demo`** to render the `Employee` FormSchema.

## Where should I start?

| I want to learn… | Look at |
|------------------|---------|
| How to spin up a Polhem backend, register a custom BO, and expose the JSON-RPC API | [`QuickStart.Server`](QuickStart.Server/README.md) |
| How to call Polhem from a third-party client with `Polhem.Api.Client` (Remote mode) | [`QuickStart.Console`](QuickStart.Console/README.md) |
| How to use `Polhem.Web.Blazor.Server` components (Local, in-process dispatch) | [`Blazor.Server.Demo`](Blazor.Server.Demo/README.md) |
| How the same `FormSchema` renders inside a desktop / browser / mobile Avalonia app | [`apps/Polhem.Northwind`](../apps/Polhem.Northwind/README.md) |
| Theme-oriented control demo center (theme → case nav, Demo/Source tabs, theme/FormMode toolbar): data binding, read-only/required, FormMode, layout, grid, native-vs-inherited parity | [`Avalonia.DemoCenter`](Avalonia.DemoCenter/README.md) |
| How to call Polhem from pure JavaScript (no .NET on the client, Plain wire format) | [`Web.Js.Demo`](Web.Js.Demo/README.md) |

## Demo catalog

| Project | Role | Default port | Launch | Library focus |
|---------|------|--------------|--------|---------------|
| [`QuickStart.Server`](QuickStart.Server/README.md) | API host | `5050` | `dotnet run` | Polhem.Api.AspNetCore + Polhem.Hosting + Polhem.Business + Polhem.Db |
| [`QuickStart.Console`](QuickStart.Console/README.md) | API client | — | `dotnet run` | Polhem.Api.Client |
| [`Blazor.Server.Demo`](Blazor.Server.Demo/README.md) | Full-stack Blazor Server | `5055` | `dotnet run` | Polhem.Web.Blazor.Server + Polhem.Samples.Shared |
| [`Avalonia.DemoCenter`](Avalonia.DemoCenter/README.md) | Desktop Avalonia control demo center | — (no backend) | `dotnet run -c Debug` | Polhem.UI.Avalonia |
| [`Web.Js.Demo`](Web.Js.Demo/README.md) | Pure-JS browser client | — (talks to 5050) | `open index.html` | (no .NET — vanilla HTML/JS) |
| [`Polhem.Samples.Shared`](Polhem.Samples.Shared/) | Shared backend wiring | — | (consumed by other demos) | Polhem.Business + Polhem.Db + Polhem.Hosting + Polhem.Api.Client |

### Inter-demo dependencies

```
QuickStart.Console ──HTTP──▶ QuickStart.Server
Web.Js.Demo        ──HTTP──▶ QuickStart.Server  ← must be started first (CORS enabled)

Blazor.Server.Demo                ← no separate server; front-end and back-end share the process
```

## Shared credentials

`Blazor.Server.Demo` and `Web.Js.Demo` (through `QuickStart.Server`) sign in with `demo / demo`:

| Field | Value |
|-------|-------|
| User ID | `demo` |
| Password | `demo` |
| Display name | `Demo User` |

`samples/Define/ProgramSettings.xml` binds the reserved `System` progId to [`DemoAuthenticatingSystemBusinessObject`](Polhem.Samples.Shared/DemoAuthenticatingSystemBusinessObject.cs), which replaces only the credential check with a hard-coded comparison, so no password hashing or user maintenance is involved. The rest of sign-in is the framework's own, which is why [`DemoSchemaSeeder`](Polhem.Samples.Shared/DemoSchemaSeeder.cs) still creates `st_user` and `st_session` and seeds a `demo` row in `st_user` (the user's time zone and culture are read from it).

`QuickStart.Server`'s `Echo.Echo` BO is annotated `[ApiAccessControl(Public, Anonymous)]`, so `QuickStart.Console` **needs no login**.

## Shared Define directory

[`samples/Define/`](Define/) is the shared definition directory used by every demo: the settings files, the FormSchemas and their stored FormLayouts, and the TableSchemas of both the demo tables and the framework tables the demos need. Look at the folder itself for the current file list. Each host locates it by walking up from `AppContext.BaseDirectory` looking for `Define/SystemSettings.xml` (see [`DemoBackend.ResolveDefinePath`](Polhem.Samples.Shared/DemoBackend.cs)), so one set of definitions drives every front-end.

### Why the demo tables live in `common`

In an application, business tables (`ft_*`) belong to the **company** category, and `common` holds only the framework tables shared across companies. The samples deliberately put everything in `common`, with one SQLite database and no company: that keeps each demo to a single database and lets it skip the company sign-in step (`EnterCompany`). It is a simplification for the demos, not a pattern to copy. [`apps/Polhem.Northwind`](../apps/Polhem.Northwind/README.md) shows the real layout, with `common`, `company` and `log` categories and a company that the session enters.

## Master key

`SystemSettings.xml` ships with `MasterKeySource.Type = Environment` and `Value = POLHEM_MASTER_KEY`, so each demo host reads the encryption master key from the environment. [`DemoBackend.AddPolhemBackend`](Polhem.Samples.Shared/DemoBackend.cs) injects a fixed demo value (`DemoCredentials.DemoMasterKey`) when `POLHEM_MASTER_KEY` is unset, so a fresh clone runs with zero setup. The per-session API encryption keys are derived from the master key, so a fixed value also keeps a signed-in session usable across a host restart.

> **Production hosts must override the demo master key.** The demo constant is committed to source and intended only for demos. Set `POLHEM_MASTER_KEY` from a deployment-managed secret (K8s Secret, env file, Vault, AWS Secrets Manager, …) **before** the process starts — the bootstrap only fills the variable when it is unset, so any externally injected value is preserved.

## Files generated on first run

The files below are **not** in git — they are runtime artifacts. A fresh clone will create them on the first `dotnet run`:

| File | Created by | Contents | gitignore rule |
|------|------------|----------|----------------|
| `samples/<Host>/quickstart.db` | [`DemoSchemaSeeder`](Polhem.Samples.Shared/DemoSchemaSeeder.cs) | SQLite with the demo tables and the framework tables, seeded with demo employees, departments and the `demo` user row | `/samples/**/*.db` |

> Both hosts (`QuickStart.Server` / `Blazor.Server.Demo`) **each get their own `quickstart.db`** and don't interfere with each other. Re-running the same host reuses existing data (both schema creation and seeding are idempotent).

To reset demo data: delete `samples/<Host>/quickstart.db` and re-run. To rotate the demo master key: change `DemoCredentials.DemoMasterKey`, or set `POLHEM_MASTER_KEY` to a different value externally. Sessions signed in under the old key stop working, so sign in again.

## Local vs Remote dispatch

`Polhem.Api.Client` exposes a **uniform API surface** to callers; only the underlying provider differs:

| Mode | Path | Used by | Sample demo |
|------|------|---------|-------------|
| **Local** | client → `LocalApiProvider` → `JsonRpcExecutor` → BO (same process) | Blazor Server, in-process tooling, BO-to-BO calls | `Blazor.Server.Demo` |
| **Remote** | client → `RemoteApiProvider` → HTTP POST → `ApiServiceController` → `JsonRpcExecutor` → BO | Console, desktop, mobile, cross-machine | `QuickStart.Console` |

In a Blazor Server host, switching modes is one line in `AddPolhemBlazor`; elsewhere it is the choice of connector constructor (an endpoint for Remote, the backend's `IServiceProvider` for Local, as in [`QuickStart.Console`](QuickStart.Console/README.md#local-vs-remote-modes)):

```csharp
// Local
builder.Services.AddPolhemBlazor(o => o.UseLocalProvider());

// Remote
builder.Services.AddPolhemBlazor(o => o.UseRemoteProvider("http://host:5050/api"));
```

A Local call is a trusted in-process call: the backend skips the access token check and the `LocalOnly` restriction for it. Use Remote for a site whose users must be held to their own permissions.

## Build all samples

```bash
dotnet build samples/Polhem.Samples.slnx
```

> Neither `./test.sh` nor the main `Polhem.slnx` touch the samples directory; the samples are always "try-when-you-want" rather than CI-validated.

## FAQ

**Q: Port 5050 / 5055 is already in use — what now?**
Edit `samples/<Host>/Properties/launchSettings.json` and change `applicationUrl`. Don't forget to update anything that points at that host — for example the `--endpoint` flag for `QuickStart.Console`.

**Q: I'm getting `Could not locate 'Define/SystemSettings.xml' walking up from ...`**
Run `dotnet run` from inside the polhem checkout. Don't copy the built binaries outside the repo — `DemoBackend` walks upward from `AppContext.BaseDirectory` looking for `Define/`, and that walk fails outside the repo.

**Q: Can I run both hosts at the same time without conflicts?**
Yes. The two hosts listen on different ports (5050 / 5055), each has its own `quickstart.db`, and they share `samples/Define/`. Running both plus the Console demo in parallel works.

**Q: I edited code under `src/` — how do I see it in the demos?**
Just re-run. `ProjectReference` rebuilds automatically. No `dotnet pack` and no cache flushing required.

## Deliberately out of scope

- Realistic ERP scenarios (orders, master-detail documents, lookups, a company database) — see [`apps/Polhem.Northwind`](../apps/Polhem.Northwind/README.md)
- SQL Server / PostgreSQL / Oracle / MySQL — SQLite is enough for demonstration
- Full auth/authz flows (OAuth, JWT, stored password hashes, companies and roles) — the credential check is short-circuited with hard-coded `demo/demo`
- Deployment scripts (Docker / k8s / TestFlight / Microsoft Store)
