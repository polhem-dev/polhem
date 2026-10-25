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
the backend (each circuit signs in on its own client, so one user's identity never reaches another's calls), and the
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

- `FormPage` -- list plus master-record editing of one program, wired through a shared `FormDataObject`. It
  loads its definitions through a `FormDefinitionLoader` by default, so captions follow the circuit's UI culture
  and the tenant's customized layout applies; its `DefinitionLoader` parameter overrides the loader for one page.
  Before a save it checks the fields marked `Required` and names the empty ones instead of sending the save.
  It recomputes computed fields (`ValueExpression`) as the user edits, through `FormLiveComputation`, and previews
  numbers with the entered company's decimals and the currency and unit masters when they are deployed; the server
  rounds authoritatively on save. Detail tables render as grids below the master fields. On a viewport narrower
  than `CompactWidthThreshold` (600 CSS pixels by default) the form switches to its compact layout, measured by a
  `CompactViewportObserver`; setting `IsCompact` fixes the layout instead, and `DetailEditMode` picks the detail
  editing model outside it.
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
  accept input; the package ships no CSS of its own. Its only script is the module of `CompactViewportObserver`.
- Lookup -- a `ButtonEdit` bound to a field with a `RelationProgId` shows the field's display fields, opens
  `LookupDialog` (a modal over `LookupPanel`: search, list, OK / Cancel) from its button, writes the chosen row's id
  and mapped fields through `FormDataObject.ApplyLookupSelection`, and clears them on Delete. The dialog takes the
  target schema and connector from its parameters, else from an `IFormDefinitionSource` and an
  `IFormApiConnectorFactory` registered in the container (scoped, so each circuit has its own), else from the circuit's
  `PolhemApiClient`; an application with a Polhem backend registers nothing.
- `PolhemLoginPanel` -- a minimal sign-in form; `OnLoggedIn` receives the `LoginResponse`.
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
