# QuickStart.Console

**English** | [繁體中文](README.zh-TW.md)

The minimal consumer demo for `Polhem.Api.Client`. Connects to [`QuickStart.Server`](../QuickStart.Server/README.md) and invokes both the built-in `System.Ping` and the custom `Echo.Echo` BO.

## How to run

```bash
# Start QuickStart.Server in another terminal first
cd samples/QuickStart.Console
dotnet run
```

The default endpoint is `http://localhost:5050/api`. To override:

```bash
dotnet run -- --endpoint http://other-host:5050/api --apikey app-id.secret
```

## What to expect

```
→ endpoint: http://localhost:5050/api

• System.Ping
  status: ok

• Echo.Echo (message="hello from QuickStart.Console")
  response : echo: hello from QuickStart.Console
  serverTime: 2026-05-23T13:00:00.0000000Z
```

## What this maps to in the library

| Code | Library feature |
|------|-----------------|
| `ApiClientInfo.ApiKey = ParseApiKey(args) ?? DefaultApiKey` | `Polhem.Api.Client.ApiClientInfo` — in Remote mode every request carries this as `X-Api-Key`. Pass `--apikey <key>` once the server has issued a real one; the built-in value is only a demo default, which works because a deployment with no issued key still accepts any non-empty value |
| `new SystemApiConnector(endpoint, Guid.Empty)` | `Polhem.Api.Client.Connectors.SystemApiConnector` — internally uses `RemoteApiProvider` over HTTP |
| `await connector.PingAsync()` | Invokes `System.Ping`; treated as anonymous by the framework and returns `status=ok` |
| `new FormApiConnector(endpoint, Guid.Empty, "Echo")` | `Polhem.Api.Client.Connectors.FormApiConnector` — bound to progId "Echo", invokes `Echo.<action>` |
| `connector.ExecuteAsync<EchoResponse>("Echo", req, PayloadFormat.Plain)` | Calls `Echo.Echo`; `PayloadFormat.Plain` is sufficient because the BO is annotated `[ApiAccessControl(Public, Anonymous)]` |

## Local vs Remote modes

This console uses **Remote mode** (HTTP to the server). `Polhem.Api.Client` also supports **Local mode** (in-process dispatch to the backend) with an identical caller-side API — only the connector construction differs:

```csharp
// Remote (this demo)
var connector = new SystemApiConnector("http://localhost:5050/api", Guid.Empty);

// Local (same process): pass the service provider that holds the backend
var services = new ServiceCollection();
services.AddPolhemFramework(settings.BackendConfiguration, paths);
using var provider = services.BuildServiceProvider();
var connector = new SystemApiConnector(provider, Guid.Empty);
```

The local connector constructors take the `IServiceProvider` the backend was registered in; `BuildServiceProvider` comes from the `Microsoft.Extensions.DependencyInjection` package. A complete backend bootstrap (master key, SQLite registration, settings loading, `AddPolhemFramework`) is [`DemoBackend.AddPolhemBackend`](../Polhem.Samples.Shared/DemoBackend.cs), which `QuickStart.Server` calls; a console host follows the same steps on a plain `ServiceCollection`.

> A Local call is a trusted in-process call: the backend skips the access token check and the `LocalOnly` restriction for it. Use it only where the calling code is trusted with the whole backend.
