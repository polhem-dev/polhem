# Polhem.Web.Blazor.Server

> Blazor Server component library for Polhem — FormSchema-driven UI components running in the ASP.NET Core host process.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: Web Frontend (Razor Class Library)
- **Hosting model**: Blazor Server — component logic executes on the ASP.NET Core server; the browser receives DOM diffs via SignalR.
- **Provider binding**: chosen with `AddPolhemBlazor` (see below) — in-process through `LocalApiProvider`, or over
  HTTP through `RemoteApiProvider`, both from `Polhem.Api.Client`.
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/dependency-map.md). Not enumerated here — the csproj files are the authority, and a prose copy in every package README drifts with nothing to catch it. These did: `Polhem.Hosting` was missing as a dependent from four of them for months after it was extracted.
- Consumed by ASP.NET Core host applications.

## Target Framework

- `net10.0`

## Registration

```csharp
using Polhem.Web.Blazor.Server.DependencyInjection;

// Remote: the components call a Polhem API server over HTTP, where every call is checked like any
// other API client's. No AddPolhemFramework is needed in this host.
builder.Services.AddPolhemBlazor(options => options.UseRemoteProvider("https://api.example.com/api"));

// Local (the default): the host is also the backend. Register it with AddPolhemFramework
// (Polhem.Hosting) on the same service collection, then:
// builder.Services.AddPolhemBlazor(options => options.UseLocalProvider());
```

`AddPolhemBlazor` registers `PolhemBlazorOptions`, a per-circuit `ApiSessionContext`, the
`PolhemApiConnectorFactory` that components use to build connectors and definition loaders, and the localizer for
the components' own text. It does not call `AddPolhemFramework`. `options.UseDefinitionLoader` (on by default)
decides whether `FormPage` localizes its definitions; see below.

> **Local mode is for trusted users only.** Every call it makes is an in-process call, which the backend treats
> as trusted whichever browser user caused it. Use it when every user of the site is trusted with the whole
> backend, such as an internal administration tool; otherwise use Remote mode. The XML documentation of
> `PolhemBlazorProviderMode.Local` lists what a local call is allowed to skip.

## Components

- `FormPage` -- list plus master-record editing of one program, wired through a shared `FormDataObject`. It
  loads its definitions through a `FormDefinitionLoader` by default, so captions follow the circuit's UI culture
  and the tenant's customized layout applies; its `DefinitionLoader` parameter overrides the loader for one page.
  Before a save it checks the fields marked `Required` and names the empty ones instead of sending the save.
  Detail tables are not rendered.
- `DynamicGrid` -- presentation-only list over a `LayoutGrid`; raises `OnRowSelected` with the row id.
- `DynamicForm` -- renders the master section(s) of a `FormLayout`, choosing the input element from each field's
  `ControlType` (text, date, month, time, checkbox, textarea, dropdown).
- `PolhemLoginPanel` -- a minimal sign-in form; `OnLoggedIn` receives the `LoginResponse`.
- `PolhemAccessTokenProvider` -- holds the circuit's access token and cascades it to descendant components.
- `FormDataObject` -- derives an in-memory `DataSet` (master row + detail tables) from `FormSchema`, exposes
  `GetField` / `SetField` for two-way binding, and runs `LoadAsync` / `SaveAsync` / `DeleteAsync` / `NewAsync`
  through the connector.

## Dependency Constraints

References `Polhem.Api.Client` (and ASP.NET Core). The backend services are registered by the host: with
`AddPolhemFramework` in Local mode, or by the remote server in Remote mode.

## License

MIT
