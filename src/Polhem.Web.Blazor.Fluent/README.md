# Polhem.Web.Blazor.Fluent

> The Fluent UI UI package of the Polhem Blazor head. An application references it with a host package such as [`Polhem.Web.Blazor.Server`](../Polhem.Web.Blazor.Server/README.md).

[繁體中文](README.zh-TW.md)

The layering is in [ADR-059](../../maintainers/adr/adr-059-blazor-shared-layer.md), and why this package works as it does in [ADR-061](../../maintainers/adr/adr-061-fluent-ui-second-ui-package.md).

## Contents

The types have the same names as in [`Polhem.Web.Blazor.Mud`](../Polhem.Web.Blazor.Mud/README.md), under `Polhem.Web.Blazor.Fluent`, and the same behavior, because the rules are in the bases of `Polhem.Web.Blazor`. That README describes the components. A page built from definitions moves between the two by changing the package and the `@using` lines. A page written directly with Fluent UI components stays a Fluent UI page.

`PolhemFluentProviders` replaces `PolhemMudProviders`, and `AddPolhemFluent` replaces `AddPolhemMud`.

## Using it

Call `AddPolhemFluent`, place one `PolhemFluentProviders` in the layout unless the shell's layout is in use, and add to the host page:

```html
<link href="_content/Microsoft.FluentUI.AspNetCore.Components/css/default-fuib.css" rel="stylesheet" />
<link href="_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.bundle.scp.css" rel="stylesheet" />
<link href="_content/Polhem.Web.Blazor.Fluent/polhem-fluent.css" rel="stylesheet" />
```

The package pins an exact Fluent UI version, so do not reference a different one.

## Differences from the MudBlazor package

- The sign-in, the column filter and the lookup search use native inputs, styled to match. See the ADR for why.
- Dialogs render where they are placed, not in a provider. They do not see the surrounding `FormScope`.
- A date shows in its storage format, `yyyy-MM-dd`, and a year and month as `yyyy-MM`.
- A column can be moved only after the grid's script has loaded, a moment after the page.
- Toolbar and filter buttons use text symbols rather than icons.

Northwind runs the package on both hosts: `Polhem.Northwind.BlazorFluent` on Blazor Server and `Polhem.Northwind.BlazorFluentWasm` on Blazor WebAssembly.
