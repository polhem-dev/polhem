# QuickStart.Server

**English** | [繁體中文](README.zh-TW.md)

The minimal runnable Polhem JSON-RPC API host. It exposes:

- `System.Ping` — framework built-in, anonymous; confirms the host is reachable.
- `Echo.Echo` — sample BO, anonymous; returns the request message decorated with an `"echo: "` prefix.

## How to run

```bash
cd samples/QuickStart.Server
dotnet run
```

The host listens on `http://localhost:5050`; the JSON-RPC endpoint is `POST /api`.

## What to expect

On first startup it will:

1. Load the definitions in `samples/Define/`, including `SystemSettings.xml`, `DatabaseSettings.xml` and `ProgramSettings.xml` (which binds the `Echo` progId to this sample's business object)
2. Use the master key from the `POLHEM_MASTER_KEY` environment variable. The demo bootstrap (`DemoBackend.AddPolhemBackend`) auto-injects a hard-coded demo value when the variable is unset, so a fresh clone runs with zero setup.
3. Create `quickstart.db` (SQLite, gitignored) in the project folder and seed it through `DemoSchemaSeeder`

The console should print `Now listening on: http://localhost:5050`.

> **Production hosts must override the demo master key.** The hard-coded demo
> value lives in `Polhem.Samples.Shared.DemoCredentials.DemoMasterKey` and is
> committed to source — it is intended only for demos. Real deployments must
> set `POLHEM_MASTER_KEY` to a deployment-managed secret (K8s Secret, env file,
> Vault, AWS Secrets Manager, …) **before** the process starts; the bootstrap
> only fills the variable when it is unset, so an externally injected value is
> always preserved.

## What this maps to in the library

`Program.cs` stays short because the backend wiring lives in the shared [`DemoBackend`](../Polhem.Samples.Shared/DemoBackend.cs) (`AddPolhemBackend` / `UsePolhemBackend`), which the Blazor demo uses too. The calls below are in that file unless noted.

| Code | Library feature |
|------|-----------------|
| `DbProviderRegistry.Register(DatabaseType.SQLite, new SqliteProviderFactory(SqliteFactory.Instance))` | `Polhem.Db.Manager.DbProviderRegistry` — pluggable ADO.NET providers. `SqliteProviderFactory` wraps the driver's factory, which has no data adapter of its own |
| `DbDialectRegistry.Register(DatabaseType.SQLite, new SqliteDialectFactory())` | `Polhem.Db.Providers.Sqlite` — SQLite dialect (form CRUD, schema reflection, DDL) |
| `SystemSettingsLoader.Load(paths)` | `Polhem.Definition.SystemSettingsLoader` — boot-time XML loading |
| `services.AddPolhemFramework(...)` | `Polhem.Hosting.PolhemFrameworkServiceCollectionExtensions` — backend composition root |
| `<ProgramItem ProgId="Echo" BusinessObject="…" />` in `samples/Define/ProgramSettings.xml` | `Polhem.Definition.Settings.ProgramSettings` — the progId → business object registry; no code registers the binding |
| `services.AddJsonRpcServer()` + `app.MapJsonRpc("/api")` (`Program.cs`) | `Polhem.JsonRpc.AspNetCore` — the JSON-RPC endpoint, served on the options `AddPolhemFramework` registered |
| `services.AddPolhemApiKeyGateCheck()` (`Program.cs`) | `Polhem.Hosting` — logs at startup while no API key has been issued |
| `[ApiAccessControl(Public, Anonymous)]` (`BusinessObjects/EchoBusinessObject.cs`) | `Polhem.Definition.Attributes.ApiAccessControlAttribute` — API access control |

## Try it without the console demo

```bash
curl -s -X POST http://localhost:5050/api \
  -H 'Content-Type: application/json' \
  -H 'X-Api-Key: quickstart-demo' \
  -d '{
        "jsonrpc": "2.0",
        "id": "1",
        "method": "Echo.Echo",
        "params": { "format": 0, "value": { "message": "hello" } }
      }'
```

The response is a JSON-RPC envelope whose `result.value.response` is `"echo: hello"`. The payload property names are matched exactly and are lower case (`format`, `value`); sent as `Value`, the business object receives no argument and the call fails. `format: 0` is `Plain`, which `Echo` accepts because it is declared `Public`.
