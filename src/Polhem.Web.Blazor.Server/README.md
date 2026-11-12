# Polhem.Web.Blazor.Server

> The Blazor Server host of Polhem: the circuit client in Local or Remote mode, and the shell's registration and culture endpoint. The controls come from a UI package.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: Web Frontend, host package (Razor Class Library)
- **Hosting model**: Blazor Server — component logic executes on the ASP.NET Core server; the browser receives DOM diffs via SignalR.
- **Provider binding**: chosen with `AddPolhemBlazor` (see below) — each circuit gets a `PolhemApiClient` from
  `Polhem.Api.Client` that calls the backend in process (`CreateLocal`) or over HTTP (`CreateRemote`).
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/architecture/dependency-map.md). Not enumerated here — the csproj files are the authority, and a prose copy in every package README drifts with nothing to catch it. These did: `Polhem.Hosting` was missing as a dependent from four of them for months after it was extracted.
- Consumed by ASP.NET Core host applications.

## Target Framework

- `net10.0`

## Registration

```csharp
using Polhem.Web.Blazor.Server.DependencyInjection;

// Remote: the components call a Polhem API server over HTTP, where every call is checked like any
// other API client's. No AddPolhemFramework is needed in this host. Pass the API key issued for this
// application: every circuit's client sends it.
builder.Services.AddPolhemBlazor(options => options.UseRemoteProvider(
    "https://api.example.com/api", builder.Configuration["Polhem:ApiKey"] ?? string.Empty));

// Local (the default): the host is also the backend. Register it with AddPolhemFramework
// (Polhem.Hosting) on the same service collection, then:
// builder.Services.AddPolhemBlazor(options => options.UseLocalProvider());
```

`AddPolhemBlazor` registers `PolhemBlazorOptions`, a per-circuit `PolhemApiClient` that components inject to reach
the backend (each circuit signs in on its own client, so one user's identity never reaches another's calls), a
per-circuit `BlazorUserContext` (also registered as the circuit's `IUserContext`), and the
localizer for the components' own text. It does not call `AddPolhemFramework`. `options.UseDefinitionLoader` (on by default)
decides whether the form page localizes its definitions; see below.

> **Remote mode needs the application's API key.** The server's default `ApiAuthorizationValidator` refuses every
> method except `System.Ping` when the `X-Api-Key` header is missing or not an enabled key, and the request is
> answered with `401 Unauthorized`. The key is the second argument of `UseRemoteProvider`: it identifies the calling application
> rather than a user, so every circuit sends the same one. With an empty key the
> first call, usually the sign-in, fails with "Response status code does not indicate success: 401 (Unauthorized)."
> Until the server has issued any key, a non-empty value is enough; once one is enabled, only an enabled key passes.
> The configuration key in the example is a placeholder: read the value from wherever the host keeps its secrets.

> **Local mode is for trusted users only.** Every call it makes is an in-process call, which the backend treats
> as trusted whichever browser user caused it. Use it when every user of the site is trusted with the whole
> backend, such as an internal administration tool; otherwise use Remote mode. The XML documentation of
> `PolhemBlazorProviderMode.Local` lists what a local call is allowed to skip.

## What this package provides

- `PolhemAccessTokenProvider` -- holds the page's signed-in state (the access token) and cascades it to descendant
  components. The calls themselves use the circuit's `PolhemApiClient`, which the sign-in has signed in.

The controls come from a UI package such as [`Polhem.Web.Blazor.Mud`](../Polhem.Web.Blazor.Mud/README.md), and the state from the shared [`Polhem.Web.Blazor`](../Polhem.Web.Blazor/README.md). An application references one UI package and this host.

## Shell

The shell is a ready-made front end for a site, the counterpart of the Avalonia
[`Polhem.UI.Avalonia.Shell`](../Polhem.UI.Avalonia.Shell/README.md): a sign-in screen that signs in and enters a
company, then a top bar (title, user and company, Sign out), the server's menu and the routed page. A site supplies
its title and the values the sign-in screen starts with, and keeps only its own pages. The layout, menu and sign-in components come from the UI package (`Polhem.Web.Blazor.Mud.Shell`); `PolhemShellHome` and
`PolhemShellOptions` are in the shared `Polhem.Web.Blazor.Shell`; the registration and the endpoint are in this package.

| Part | What it does |
|------|--------------|
| `PolhemShellLayout` | The router's default layout. Shows `PolhemShellSignIn` until the circuit's user has entered a company, then the top bar, `PolhemShellNavMenu` and the page. The menu button collapses the menu on a wide screen and slides it over the page below 600 CSS pixels, the form page's default compact threshold; picking a form closes it. |
| `PolhemShellSignIn` | Title, hint, company and a `PolhemLoginPanel`; signs in, then enters the company through `BlazorUserContext.EnterCompanyAsync`. A refusal is shown on the screen and signs the circuit out again. Offers a switch between the configured languages, and lists `PolhemShellOptions.SignInLinks` at the foot, leaving out a link to the site's own base address. |
| `PolhemShellNavMenu` | The server's `MenuSettings` as folder headers and links to `{FormRoute}/{ProgId}`, captions in the circuit's UI culture. |
| `PolhemShellHome` | Opens the menu's first form, as the Avalonia shell does after sign-in, or says that the menu has no forms yet. Place it on the root page. |
| `AddPolhemShell` | Registers `PolhemShellOptions`. Every option is described in its XML documentation. |
| `UsePolhemShellLocalization` | Request localization over the configured languages, so a circuit starts in the language the viewer picked. |
| `MapPolhemShellCulture` | `GET /culture`, the endpoint behind the language switch. It stores an offered culture in the request-localization cookie and redirects back, to a path on this site only; any other address returns to the root, so the endpoint is not an open redirect (`PolhemShellCultureEndpointTests`). Both are taken under the request's path base, so an app mounted with `app.Map("/sub", ...)` maps it in its own branch and returns to its own pages. |

```csharp
using Polhem.Web.Blazor.Mud.DependencyInjection;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Polhem.Web.Blazor.Server.Shell;

builder.Services.AddPolhemBlazor(options => options.UseLocalProvider());
builder.Services.AddPolhemShell(shell =>
{
    shell.AppTitle = "My App";
    shell.DefaultCompanyId = "ACME";
});
builder.Services.AddPolhemMud();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.UsePolhemShellLocalization();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapPolhemShellCulture();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
```

The application keeps its `App.razor` (which links the UI package's assets; see its README), its router with
`DefaultLayout="typeof(PolhemShellLayout)"`, and two pages:

```razor
@* Pages/Home.razor *@
@page "/"
<PolhemShellHome />

@* Pages/FormHost.razor: the route is "/{FormRoute}/{ProgId}", "form" by default. *@
@page "/form/{ProgId}"
<FormPage ProgId="@ProgId" />
@code {
    [Parameter] public string ProgId { get; set; } = string.Empty;
}
```

The sign-in belongs to the circuit, so a reload or a new tab shows the sign-in screen again. A circuit's culture is
fixed when it connects, so the language is picked on the sign-in screen, before anyone is signed in: the switch goes
through `/culture` and reloads the page. The account's own culture is not applied afterwards. The shell's own text
exists in English and Traditional Chinese; the components and the definitions follow their own language resources.

The shell runs on the provider the host chose, so the Local mode warning above applies to it unchanged.

## Dependency Constraints

References [`Polhem.Web.Blazor`](../Polhem.Web.Blazor/README.md), which brings the API client and the shared rules,
and the ASP.NET Core shared framework (`Microsoft.AspNetCore.App`). The backend services are registered by the host:
with `AddPolhemFramework` in Local mode, or by the remote server in Remote mode. The rule that keeps `UIServices` and
`Polhem.UI.ApiBridge` out of the Blazor packages is described in the `Polhem.Web.Blazor` README.

## License

MIT
