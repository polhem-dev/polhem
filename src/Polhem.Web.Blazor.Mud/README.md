# Polhem.Web.Blazor.Mud

> The MudBlazor UI package of the Polhem Blazor head. It is under construction and is not published yet.

[繁體中文](README.zh-TW.md)

The layering is in [ADR-059](../../maintainers/adr/adr-059-blazor-shared-layer.md).

## Contents

- `FieldEditorFactory` and the field editors (`Polhem.Web.Blazor.Mud.Controls.Editors`), over the editor bases of `Polhem.Web.Blazor`.
- `ButtonEdit`, `LookupPanel` and `LookupDialog` in the same namespace, the lookup editor and its picker.
- `GridControl` (`Polhem.Web.Blazor.Mud.Controls`), a `MudDataGrid` over a layout. In `InCell` mode a cell shows text and opens its editor when it takes the focus, with one editor open in a grid. Escape in an open editor drops what was typed and returns the cell to text. A list grid takes `OnSortRequested` and `SortFieldName`; it asks its host to read the rows in a new order and does not sort them itself.
- `RowEditPanel` and `RowEditDialog`, the edit form of `EditForm` mode.
- `FormLayoutPresenter` (`Polhem.Web.Blazor.Mud.Controls`), a record laid out from a `FormLayout`. The fields stay on a CSS grid, because `MudGrid` cannot do a `RowSpan` or a `ColumnCount` that does not divide twelve.
- `FormPage` and `RelatedRecordDialog` (`Polhem.Web.Blazor.Mud.Components`), over the bases of `Polhem.Web.Blazor`; `ConfirmDialog` asks before a delete with the focus on Cancel.
- On a narrow viewport, or with `IsCompact`, `FormPage` shows the list as cards and reads the next page as the user scrolls, up to 5000 rows. Every card stays rendered, so a long scroll costs circuit memory and traffic.
- `PolhemLoginPanel` (`Polhem.Web.Blazor.Mud.Components`), the sign-in form.
- `PolhemShellLayout`, `PolhemShellNavMenu` and `PolhemShellSignIn` (`Polhem.Web.Blazor.Mud.Shell`), the shell over `MudLayout`. The layout places `PolhemMudProviders` itself. A host registers the options with its host package (`AddPolhemShell` on Blazor Server).
- `PolhemMudProviders` and `PolhemMudTheme` (`Polhem.Web.Blazor.Mud.Theming`), MudBlazor's providers and the default theme.
- `AddPolhemMud` (`Polhem.Web.Blazor.Mud.DependencyInjection`), which registers MudBlazor's services.

## Using it

Call `AddPolhemMud`, place one `PolhemMudProviders` in the layout unless the shell's layout is in use, and add to the host page:

```html
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
<link href="_content/Polhem.Web.Blazor.Mud/polhem-mud.css" rel="stylesheet" />
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
```

Wrap desktop screens in an element with the `polhem-dense` class for high-density controls. Leave it out on phone-sized screens, where the controls would be too small.
