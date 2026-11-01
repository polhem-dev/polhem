# Polhem.Web.Blazor.Mud

> The MudBlazor UI package of the Polhem Blazor head. It is under construction and is not published yet.

[繁體中文](README.zh-TW.md)

The layering is in [ADR-059](../../maintainers/adr/adr-059-blazor-shared-layer.md).

## Contents

- `FieldEditorFactory` and the field editors (`Polhem.Web.Blazor.Mud.Controls.Editors`), over the editor bases of `Polhem.Web.Blazor`.
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
