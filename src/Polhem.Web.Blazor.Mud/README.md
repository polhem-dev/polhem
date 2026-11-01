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
- `PolhemMudProviders` and `PolhemMudTheme` (`Polhem.Web.Blazor.Mud.Theming`), MudBlazor's providers and the default theme.
- `AddPolhemMud` (`Polhem.Web.Blazor.Mud.DependencyInjection`), which registers MudBlazor's services.

## Using it

Call `AddPolhemMud`, place one `PolhemMudProviders` in the layout, and add to the host page:

```html
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
<link href="_content/Polhem.Web.Blazor.Mud/polhem-mud.css" rel="stylesheet" />
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
```

Wrap desktop screens in an element with the `polhem-dense` class for high-density controls. Leave it out on phone-sized screens, where the controls would be too small.
