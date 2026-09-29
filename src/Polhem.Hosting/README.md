# Polhem.Hosting

> Composition root for the Polhem framework — registers the backend services into any `IServiceCollection`, with no ASP.NET Core dependency.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: Composition root (DI registration)
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/architecture/dependency-map.md). Not enumerated here — the csproj files are the authority, and a prose copy in every package README drifts with nothing to catch it. These did: `Polhem.Hosting` was missing as a dependent from four of them for months after it was extracted.
- Also usable from hosts without ASP.NET Core: desktop heads that run the backend in-process, console, Worker Service and integration tests.

A composition root reaches across every layer by definition, so the "API layer must not reference the
Repository layer" constraint does not apply here. What does apply: this package currently holds **no SQL of
its own**, and should stay that way. Its hosted services are shells — the cache-notify poller reads through
`ICacheNotifyReader` (`Polhem.Db`) and the default audit sink writes through `IAuditLogWriteRepository`
(`Polhem.Repository.Abstractions`, implemented in `Polhem.Repository`). Statement construction and
execution belong to those layers; new SQL added here would be a layering regression.

## Target Framework

- `net10.0`

## When to Reference This Package

| Host type | Reference |
|-----------|-----------|
| ASP.NET Core web host | `Polhem.Api.AspNetCore` (transitively brings in `Polhem.Hosting`) |
| Desktop head running the backend in-process, console, Worker Service | `Polhem.Hosting` directly |
| Integration tests | `Polhem.Hosting` directly |

A head that only talks to a remote server (a `Polhem.Api.Client` consumer with remote endpoints) does not need
this package. A head that runs the backend in its own process references both: it builds the backend with
`AddPolhemFramework` and passes the resulting `IServiceProvider` to the in-process connector constructors.

## Key Public APIs

| Class / Member | Purpose |
|----------------|---------|
| `PolhemFrameworkServiceCollectionExtensions.AddPolhemFramework` | Registers the framework services (`IDefineAccess`, `IDbAccessFactory`, `IBusinessObjectFactory`, `JsonRpcExecutor`, the hosted services, …) into the supplied `IServiceCollection` |
| `IAuditLogSink` | Where audit records go. The default writes to the log database; register your own before `AddPolhemFramework` to send them elsewhere |

## Usage

Register the database providers first (see the `Polhem.Db` README).

### ASP.NET Core host

```csharp
using Polhem.Api.AspNetCore;
using Polhem.Api.Core;
using Polhem.Core;
using Polhem.Definition;
using Polhem.Hosting;

var builder = WebApplication.CreateBuilder(args);

var paths = new PathOptions { DefinePath = Path.Combine(AppContext.BaseDirectory, "Define") };
var settings = SystemSettingsLoader.Load(paths);
SysInfo.Initialize(settings.CommonConfiguration);
ApiServiceOptions.Initialize(settings.CommonConfiguration.ApiPayloadOptions, settings.CommonConfiguration.IsDebugMode);

builder.Services.AddPolhemFramework(settings.BackendConfiguration, paths);
builder.Services.AddControllers();

var app = builder.Build();
app.UsePolhemFramework();
app.MapControllers();
app.Run();
```

The controller is a class derived from `ApiServiceController` (see the `Polhem.Api.AspNetCore` README).

### Host without ASP.NET Core (a desktop head running the backend in-process)

The framework registers hosted services (reserved progId registration, the cache-notify poller, session cleanup,
the background audit writer), so build a generic host and start it. This needs the
`Microsoft.Extensions.Hosting` package.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core;
using Polhem.Core;
using Polhem.Definition;
using Polhem.Hosting;

var paths = new PathOptions { DefinePath = definePath };
var settings = SystemSettingsLoader.Load(paths);
SysInfo.Initialize(settings.CommonConfiguration);
ApiServiceOptions.Initialize(settings.CommonConfiguration.ApiPayloadOptions, settings.CommonConfiguration.IsDebugMode);

var builder = Host.CreateApplicationBuilder();
builder.Services.AddPolhemFramework(settings.BackendConfiguration, paths);
using var host = builder.Build();
await host.StartAsync();

// In-process connectors take the backend service provider instead of an endpoint URL.
var connector = new SystemApiConnector(host.Services, Guid.Empty);
var login = await connector.LoginAsync("demo", "demo");
```

A `Polhem.UI.Core` head assigns `host.Services` to `ClientInfo.LocalServiceProvider` instead, and the connectors
`ClientInfo` creates use it.

## Design Conventions

- **Composition root** — DI registration lives here, separated from ASP.NET Core middleware (which stays in `Polhem.Api.AspNetCore`)
- **No ASP.NET Core dependency** — references only the `Microsoft.Extensions.DependencyInjection` and `Microsoft.Extensions.Hosting` abstractions, so non-web hosts can register the framework without pulling in the web stack
- **Replaceable implementations by type name** — the services listed in `BackendComponents` (in `SystemSettings.xml`), such as `IDefineAccess`, `ISessionInfoService`, `ICacheDataSourceProvider` and the `RepositoryFactory`, can be replaced by naming a type there; a blank entry means the framework default. A wrong type name fails at startup, naming the setting. Other services, such as `IBusinessObjectFactory`, are registered directly
- **Hosted services are internal** — the audit writer, cache-notify poller, expired session cleanup and startup checks are registered by `AddPolhemFramework` and configured through `BackendConfiguration`; `IAuditLogSink` is the public seam

## Directory Structure

- `PolhemFrameworkServiceCollectionExtensions*.cs` -- `AddPolhemFramework` and its helpers
- `Audit/` -- `IAuditLogSink` and the internal audit writers
- `CacheNotify/` -- the cache-notify poller
- `Database/` -- the startup check that `DatabaseSettings` has the items the framework requires
- `Session/` -- expired session cleanup
- `Registry/` -- startup registration and warning services
