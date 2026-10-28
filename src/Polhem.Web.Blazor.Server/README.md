# Polhem.Web.Blazor.Server

> Blazor Server component library for Polhem — FormSchema-driven UI components running in the ASP.NET Core host process.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: Web Frontend (Razor Class Library)
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
per-circuit `CircuitUserContext` (also registered as the circuit's `IUserContext`), and the
localizer for the components' own text. It does not call `AddPolhemFramework`. `options.UseDefinitionLoader` (on by default)
decides whether `FormPage` localizes its definitions; see below.

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

## Components

- `FormPage` -- the list and the record of one program, one at a time, as the Avalonia `ListView` and `FormView`:
  the list has View, New, Edit and Delete (a click selects a row, a double-click views it), a viewed record has Back
  and an edited one Save and Cancel; a successful save returns to the reloaded list. It
  loads its definitions through a `FormDefinitionLoader` by default, so captions follow the circuit's UI culture
  and the tenant's customized layout applies; its `DefinitionLoader` parameter overrides the loader for one page.
  Before a save it checks the fields marked `Required` and names the empty ones instead of sending the save.
  It recomputes computed fields (`ValueExpression`) as the user edits, through `FormLiveComputation`, and previews
  numbers with the entered company's decimals and the currency and unit masters when they are deployed; the server
  rounds authoritatively on save. A record's detail tables render as grids below its master fields. On a viewport narrower
  than `CompactWidthThreshold` (600 CSS pixels by default) the form switches to its compact layout, measured by a
  `CompactViewportObserver`; setting `IsCompact` fixes the layout instead, and `DetailEditMode` picks the detail
  editing model outside it. The page loads again when `ProgId` changes, so one routed page (`/form/{ProgId}`) can serve
  every program. A failed load, save or delete is shown as a notice above the form, which keeps what the user entered;
  only a page that cannot load its definitions shows the error in place of the form. With a capability snapshot on the
  circuit's `IUserContext`, View, New, Edit and Delete are hidden when the user lacks `Read`, `Create`, `Update` and
  `Delete` on the form's permission model, Save when they lack both `Create` and `Update`, and sensitive fields are hidden or read-only by their category, through
  `LayoutCapabilityApplier` (`Polhem.UI.Core`); the numbers preview with the entered company's decimals. The backend
  still checks every call.
- `CompactViewportObserver` -- renders nothing and raises `CompactChanged` when the browser's viewport crosses
  `CompactWidthThreshold`. A page that uses `FormLayoutPresenter` directly places one next to it and passes the value
  to `IsCompact`. It measures the viewport through a small JavaScript module that ships in the package and loads
  through JavaScript isolation, so the host page adds no `<script>`; the host must serve static web assets
  (`UseStaticFiles` or `MapStaticAssets`). A prerendered page starts wide and switches once the circuit connects.
- `DynamicGrid` -- presentation-only list over a `LayoutGrid`; raises `OnRowSelected` with the row id on a click and
  `OnRowActivated` on a double-click, and highlights the row whose id the host passes as `SelectedRowId`.
- `FormLayoutPresenter` -- renders the master section(s) of a `FormLayout`, one field editor per field, chosen from
  its `ControlType` by `FieldEditorFactory`. Its `FormMode` parameter decides which editors accept input, following
  each field's `ReadOnly` and `AllowEditModes`. Each detail table of the layout follows as a `GridControl`, in
  `DetailEditMode`. `IsCompact` renders for a phone-sized screen: the master fields in one column whatever the
  layout's `ColumnCount`, and the detail grids in the edit form. The root element then also carries
  `polhem-form-layout--compact`.
- `GridControl` -- a grid over a detail table of a `FormDataObject`, or over a caller-supplied `DataTable` in list
  mode, with the editing rules of the Avalonia `GridControl`: an Add / Edit / Delete toolbar while the form mode
  and the layout's `AllowEditModes` and `AllowActions` allow it, and `EditMode` `InCell` (each editable cell shows
  its field editor) or `EditForm` (a row is edited in `RowEditDialog`, a modal over `RowEditPanel`, which buffers
  the edits until OK). Cell text follows the shared `GridCellFormatter`; a read-only header is parenthesised and a
  required one carries `polhem-grid__header--required`.
- Field editors (`Polhem.Web.Blazor.Server.Controls.Editors`) -- `TextEdit`, `MemoEdit`, `NumericEdit`, `TimeEdit`,
  `DateTimeEdit`, `DateEdit`, `YearMonthEdit`, `CheckEdit`, `DropDownEdit` and `ButtonEdit`, with the same names, contract
  (`IFieldEditor`) and behavior as the field editors of `Polhem.UI.Avalonia`. An editor binds to a
  `FormDataObject` through its `DataObject` parameter, an enclosing `FormScope`, or an explicit `Bind`; it applies
  the schema field's metadata (such as `MaxLength` and `ListItems`) itself and refreshes when another party writes
  its field. New editors derive from `FieldEditorBase`. Their markup carries the `polhem-field-editor` class, a
  per-editor modifier such as `polhem-field-editor--numeric`, and `polhem-field-editor--readonly` when they do not
  accept input; the package's optional stylesheet styles these classes (see Styling below). Its only script is the
  module of `CompactViewportObserver`.
- Lookup -- a `ButtonEdit` bound to a field with a `RelationProgId` shows the field's display fields, opens
  `LookupDialog` (a modal over `LookupPanel`: search, list, OK / Cancel) from its button, writes the chosen row's id
  and mapped fields through `FormDataObject.ApplyLookupSelection`, and clears them on Delete. The dialog takes the
  target schema and connector from its parameters, else from an `IFormDefinitionSource` and an
  `IFormApiConnectorFactory` registered in the container (scoped, so each circuit has its own), else from the circuit's
  `PolhemApiClient`; an application with a Polhem backend registers nothing.
- `PolhemLoginPanel` -- a minimal sign-in form; `OnLoggedIn` receives the `LoginResponse`. A successful sign-in is
  also recorded on the circuit's `CircuitUserContext`. `InitialUserId` and `InitialPassword` fill the inputs once, when
  the panel is initialized.
- `CircuitUserContext` -- the signed-in user, entered company and capabilities of one circuit. The host enters the
  company with `EnterCompanyAsync` and signs out with `SignOut`, which keep the context and the client in step.
  Entering the company through the client directly leaves the context without capabilities, so the pages treat the
  user as unrestricted.
- `PolhemAccessTokenProvider` -- holds the page's signed-in state (the access token) and cascades it to descendant
  components. The calls themselves use the circuit's `PolhemApiClient`, which the sign-in has signed in.
- `FormDataObject` -- derives an in-memory `DataSet` (master row + detail tables) from `FormSchema`, exposes
  `GetField` / `SetField` (for the master row or a given row) for two-way binding, raises `FieldValueChanged` and
  `DataSetReplaced` when its values change, and runs `LoadAsync` / `SaveAsync` / `DeleteAsync` / `NewAsync`
  through the connector. A new row is seeded with the schema's defaults (a fresh `sys_rowid`, the link to the
  master, non-null values by type) and raises `RowAdded`; `BeginRowEdit` / `CommitRowEdit` / `CancelRowEdit` buffer
  the edits of one row, which raise `RowEditFieldChanged` until the commit publishes them.
- `FormLiveComputation` -- recomputes a form's computed fields and evaluates new-row default expressions, with the
  same rules as the Avalonia head (`FormLiveComputationEngine` in `Polhem.UI.Core`). A page of its own subscribes
  `FieldValueChanged` and calls `Recompute`; pass the circuit's time zone, never a process-wide one.

## Shell

`Polhem.Web.Blazor.Server.Shell` is a ready-made front end for a site, the counterpart of the Avalonia
[`Polhem.UI.Avalonia.Shell`](../Polhem.UI.Avalonia.Shell/README.md): a sign-in screen that signs in and enters a
company, then a top bar (title, user and company, Sign out), the server's menu and the routed page. A site supplies
its title and the values the sign-in screen starts with, and keeps only its own pages.

| Part | What it does |
|------|--------------|
| `PolhemShellLayout` | The router's default layout. Shows `PolhemShellSignIn` until the circuit's user has entered a company, then the top bar, `PolhemShellNavMenu` and the page. The menu button collapses the menu on a wide screen and slides it over the page below 600 CSS pixels, the form page's default compact threshold; picking a form closes it. |
| `PolhemShellSignIn` | Title, hint, company and a `PolhemLoginPanel`; signs in, then enters the company through `CircuitUserContext.EnterCompanyAsync`. A refusal is shown on the screen and signs the circuit out again. Offers a switch between the configured languages. |
| `PolhemShellNavMenu` | The server's `MenuSettings` as folder headers and links to `{FormRoute}/{ProgId}`, captions in the circuit's UI culture. |
| `PolhemShellHome` | Opens the menu's first form, as the Avalonia shell does after sign-in. Place it on the root page. |
| `AddPolhemShell` | Registers `PolhemShellOptions`. Every option is described in its XML documentation. |
| `UsePolhemShellLocalization` | Request localization over the configured languages, so a circuit starts in the language the viewer picked. |
| `MapPolhemShellCulture` | `GET /culture`, the endpoint behind the language switch. It stores an offered culture in the request-localization cookie and redirects back, to a path on this site only; any other address returns to the root, so the endpoint is not an open redirect (`PolhemShellCultureEndpointTests`). |

```csharp
using Polhem.Web.Blazor.Server.DependencyInjection;
using Polhem.Web.Blazor.Server.Shell;

builder.Services.AddPolhemBlazor(options => options.UseLocalProvider());
builder.Services.AddPolhemShell(shell =>
{
    shell.AppTitle = "My App";
    shell.DefaultCompanyId = "ACME";
});
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.UsePolhemShellLocalization();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapPolhemShellCulture();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
```

The application keeps its `App.razor` (which links the stylesheet, below), its router with
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

## Styling

The package ships one optional stylesheet, `_content/Polhem.Web.Blazor.Server/polhem.css`, with the default look of
the components and the shell. Link it in `App.razor`, before the site's own sheet:

```html
<link rel="stylesheet" href="_content/Polhem.Web.Blazor.Server/polhem.css" />
<link rel="stylesheet" href="app.css" />
```

It styles only the `polhem-*` classes; the page's body (font, background, margin) stays the site's. To restyle,
redefine its custom properties (`--polhem-accent`, `--polhem-surface`, `--polhem-shell-header` and the others at the top of
the file) on `:root` in the site's sheet, or override any rule with a selector of the same class. A site that does not
link it styles the classes itself. `CssClassNameGateTests` (`tests/Polhem.Web.Blazor.Server.UnitTests`) fails when a
selector in the sheet matches no class the components render.

## Dependency Constraints

References `Polhem.Api.Client`, [`Polhem.UI.Core`](../Polhem.UI.Core/README.md) and ASP.NET Core. The backend
services are registered by the host: with `AddPolhemFramework` in Local mode, or by the remote server in Remote mode.

`Polhem.UI.Core` supplies the form value rules every head shares (`FormValueBinding`, `FormDataGuard`, the element
capability resolver). Its process-wide parts are off limits: this head serves several users from one process, so it
must not use `UIServices` or reference `Polhem.UI.ApiBridge`, both of which hold one user per process.
`UIServicesUsageGateTests` (`tests/Polhem.Web.Blazor.Server.UnitTests`) reads the compiled assembly's references and
fails when it does. See [ADR-054](../../maintainers/adr/adr-054-front-end-decoupled-from-api-client.md).

## License

MIT
