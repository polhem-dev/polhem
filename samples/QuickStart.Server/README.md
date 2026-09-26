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

1. Load `SystemSettings.xml` / `DbCategorySettings.xml` / `DatabaseSettings.xml` from `samples/Define/`
2. Use the master key from the `POLHEM_MASTER_KEY` environment variable. The demo bootstrap (`DemoBackend.AddPolhemBackend`) auto-injects a hard-coded demo value when the variable is unset, so a fresh clone runs with zero setup.
3. Create `quickstart.db` in the working directory (gitignored)

The console should print `Now listening on: http://localhost:5050`.

> **Production hosts must override the demo master key.** The hard-coded demo
> value lives in `Polhem.Samples.Shared.DemoCredentials.DemoMasterKey` and is
> committed to source — it is intended only for demos. Real deployments must
> set `POLHEM_MASTER_KEY` to a deployment-managed secret (K8s Secret, env file,
> Vault, AWS Secrets Manager, …) **before** the process starts; the bootstrap
> only fills the variable when it is unset, so an externally injected value is
> always preserved.

## What this maps to in the library

| Code | Library feature |
|------|-----------------|
| `DbProviderRegistry.Register(DatabaseType.SQLite, SqliteFactory.Instance)` | `Polhem.Db.Manager.DbProviderRegistry` — pluggable ADO.NET providers |
| `DbDialectRegistry.Register(DatabaseType.SQLite, new SqliteDialectFactory())` | `Polhem.Db.Providers.Sqlite` — SQLite dialect (form CRUD, schema reflection, DDL) |
| `SystemSettingsLoader.Load(paths)` | `Polhem.Definition.SystemSettingsLoader` — boot-time XML loading |
| `services.AddPolhemFramework(...)` | `Polhem.Hosting.PolhemFrameworkServiceCollectionExtensions` — backend composition root |
| `IBoTypeResolver` override | `Polhem.Business.IBoTypeResolver` — custom progId → BO type mapping |
| Empty `: ApiServiceController` controller | `Polhem.Api.AspNetCore.Controllers.ApiServiceController` — `[Route("api")]` JSON-RPC endpoint |
| `[ApiAccessControl(Public, Anonymous)]` | `Polhem.Definition.Attributes.ApiAccessControlAttribute` — API access control |

## Try it without the console demo

```bash
curl -s -X POST http://localhost:5050/api \
  -H 'Content-Type: application/json' \
  -H 'X-Api-Key: quickstart-demo' \
  -d '{
        "jsonrpc": "2.0",
        "id": "1",
        "method": "Echo.Echo",
        "params": { "Value": { "Message": "hello" } }
      }'
```

The response should be a JSON-RPC envelope whose `result.value` contains `Response = "echo: hello"`.
