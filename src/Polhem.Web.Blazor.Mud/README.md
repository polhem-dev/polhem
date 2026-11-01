# Polhem.Web.Blazor.Mud

> The MudBlazor UI package of the Polhem Blazor head. An application references it with a host package such as [`Polhem.Web.Blazor.Server`](../Polhem.Web.Blazor.Server/README.md).

[繁體中文](README.zh-TW.md)

The layering is in [ADR-059](../../maintainers/adr/adr-059-blazor-shared-layer.md), and why MudBlazor in [ADR-060](../../maintainers/adr/adr-060-mudblazor-first-ui-package.md). Pages an application writes directly with MudBlazor components do not carry over to another UI package; definition-driven pages do.

## Contents

- `FieldEditorFactory` and the field editors (`Polhem.Web.Blazor.Mud.Controls.Editors`), over the editor bases of `Polhem.Web.Blazor`.
- `ButtonEdit`, `LookupPanel` and `LookupDialog` in the same namespace, the lookup editor and its picker.
- `GridControl` (`Polhem.Web.Blazor.Mud.Controls`), a `MudDataGrid` over a layout. In `InCell` mode a cell shows text and opens its editor when it takes the focus, with one editor open in a grid. Escape in an open editor drops what was typed and returns the cell to text. A list grid takes `OnSortRequested` and `SortFieldName`; it asks its host to read the rows in a new order and does not sort them itself.
- `RowEditPanel` and `RowEditDialog`, the edit form of `EditForm` mode.
- `FormLayoutPresenter` (`Polhem.Web.Blazor.Mud.Controls`), a record laid out from a `FormLayout`. The fields stay on a CSS grid, because `MudGrid` cannot do a `RowSpan` or a `ColumnCount` that does not divide twelve.
- `FormPage` and `RelatedRecordDialog` (`Polhem.Web.Blazor.Mud.Components`), over the bases of `Polhem.Web.Blazor`; `ConfirmDialog` asks before a delete with the focus on Cancel.
- `PolhemLoginPanel` (`Polhem.Web.Blazor.Mud.Components`), the sign-in form.
- `PolhemShellLayout`, `PolhemShellNavMenu` and `PolhemShellSignIn` (`Polhem.Web.Blazor.Mud.Shell`), the shell over `MudLayout`. The layout places `PolhemMudProviders` itself. A host registers the options with its host package (`AddPolhemShell` on Blazor Server).
- `PolhemMudProviders` and `PolhemMudTheme` (`Polhem.Web.Blazor.Mud.Theming`), MudBlazor's providers and the default theme.
- `AddPolhemMud` (`Polhem.Web.Blazor.Mud.DependencyInjection`), which registers MudBlazor's services.

## Components

An editor, a grid or a page here is the same on every UI package: the rules are in the bases of `Polhem.Web.Blazor`, and `tests/Polhem.Web.Blazor.BehaviorTests` holds each package to them.

- `FormPage` -- the list, shown a page at a time, and the record of one program, one at a time, as the Avalonia `ListView` and `FormView`:
  the list has View, New, Edit and Delete (a click selects a row, a double-click views it), a viewed record has Back
  and an edited one Save and Cancel; a successful save returns to the reloaded list. Delete asks first unless the
  `ConfirmDelete` parameter is `false`. It
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
- `FormLayoutPresenter` -- renders the master section(s) of a `FormLayout`, one field editor per field, chosen from
  its `ControlType` by `FieldEditorFactory`. Its `FormMode` parameter decides which editors accept input, following
  each field's `ReadOnly` and `AllowEditModes`. Each detail table of the layout follows as a `GridControl`, in
  `DetailEditMode`. `IsCompact` renders for a phone-sized screen: the master fields in one column whatever the
  layout's `ColumnCount`, and the detail grids in the edit form.
- `GridControl` -- a grid over a detail table of a `FormDataObject`, or over a caller-supplied `DataTable` in list
  mode, with the editing rules of the Avalonia `GridControl`: an Add / Edit / Delete toolbar while the form mode
  and the layout's `AllowEditModes` and `AllowActions` allow it, and `EditMode` `InCell` (each editable cell shows
  its field editor) or `EditForm` (a row is edited in `RowEditDialog`, a modal over `RowEditPanel`, which buffers
  the edits until OK). Cell text follows the shared `GridCellFormatter`; a read-only header is parenthesised, and a required one shows the required cue.
- Field editors (`Polhem.Web.Blazor.Mud.Controls.Editors`) -- `TextEdit`, `MemoEdit`, `NumericEdit`, `TimeEdit`,
  `DateTimeEdit`, `DateEdit`, `YearMonthEdit`, `CheckEdit`, `DropDownEdit` and `ButtonEdit`, with the same names, contract
  (`IFieldEditor`) and behavior as the field editors of `Polhem.UI.Avalonia`. An editor binds to a
  `FormDataObject` through its `DataObject` parameter, an enclosing `FormScope`, or an explicit `Bind`; it applies
  the schema field's metadata (such as `MaxLength` and `ListItems`) itself and refreshes when another party writes
  its field. New editors derive from `FieldEditorBase`.
- Lookup -- a `ButtonEdit` bound to a field with a `RelationProgId` shows the field's display fields, opens
  `LookupDialog` (a modal over `LookupPanel`: search, list, OK / Cancel) from its button, writes the chosen row's id
  and mapped fields through `FormDataObject.ApplyLookupSelection`, and clears them on Delete. The dialog takes the
  target schema and connector from its parameters, else from an `IFormDefinitionSource` and an
  `IFormApiConnectorFactory` registered in the container (scoped, so each circuit has its own), else from the circuit's
  `PolhemApiClient`; an application with a Polhem backend registers nothing.
- Related records -- on a read-only form a `ButtonEdit` with a value shows a hand cursor and a hint instead of its
  button, and a double-click passes the record it points at to `FormLayoutPresenter.OpenRelation`; a detail
  `GridControl`'s relation cell does the same. `FormPage` handles
  it with `RelatedRecordDialog`, which shows that record read-only when the user holds `Read` on the related form's
  permission model and otherwise lets the page name the form in a notice; the related form's `GetData` checks again on
  the server. The shared rules are `RelationOpenRules` in `Polhem.UI.Core`.
- `PolhemLoginPanel` -- a minimal sign-in form; `OnLoggedIn` receives the `LoginResponse`. A successful sign-in is
  also recorded on the circuit's `BlazorUserContext`. `InitialUserId` and `InitialPassword` fill the inputs once, when
  the panel is initialized.

## Using it

Call `AddPolhemMud`, place one `PolhemMudProviders` in the layout unless the shell's layout is in use, and add to the host page:

```html
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
<link href="_content/Polhem.Web.Blazor.Mud/polhem-mud.css" rel="stylesheet" />
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
```

The package pins an exact MudBlazor version, so do not reference a different one. To restyle, pass a `MudTheme` to `PolhemMudProviders`.

Wrap desktop screens in an element with the `polhem-dense` class for high-density controls. Leave it out on phone-sized screens, where the controls would be too small.
