# Blazor rules (core)

> The package split is in `maintainers/adr/adr-059-blazor-shared-layer.md`, and why MudBlazor and why no UI abstraction in
> `maintainers/adr/adr-060-mudblazor-first-ui-package.md`.

## Three kinds of package, and what each may not reference

| Package | Holds | May not reference |
|---------|-------|-------------------|
| `Polhem.Web.Blazor` | State and rules, as abstract component bases | A UI framework, `Microsoft.AspNetCore.App` |
| `Polhem.Web.Blazor.<UI>` (`.Mud`, `.Fluent`) | Markup and theme for one framework | A host package |
| `Polhem.Web.Blazor.<Host>` (`.Server`, `.Wasm`) | Wiring for one host | A UI package |

What is not tied to a framework lives in `Polhem.Web.Blazor` too: the grid's and the card list's scripts,
`polhem-blazor.css`, which a UI package's stylesheet imports, and the components over them. The framework-specific part
stays in the UI package and is passed in (`GridEditorScript` takes the selector of an open popup).

`POLHEM9001`, `BlazorSharedLayerDependencyGateTests`, `BlazorWasmHostDependencyGateTests`,
`BlazorServerHostDependencyGateTests`, `MudPackageDependencyGateTests` and `FluentPackageDependencyGateTests` enforce the
references. Which projects `POLHEM9001` governs is set in `src/Directory.Build.targets` (`PolhemEnforceDependencyBoundary`,
and `PolhemVerifyBrowserFramework` for the shared framework). The closure tests read `.deps.json`, which shows no shared
framework and no reference marked `PrivateAssets="all"`; the targets cover the first, and the tests also check each
assembly's compiled references for the second.

**Put a rule in the shared base and only markup in the UI package.** A new behavior gets a test in
`tests/Polhem.Web.Blazor.BehaviorTests`, which every UI package derives from, not a test against one package's markup.
Do not add a generic control interface such as `IGrid`.

## A component's type summary goes in its `.razor.cs`

The Razor generator turns CS1591 off in the code it generates, so the build does not ask for a component's type
summary. Every public component therefore has a `<Name>.razor.cs` holding `public partial class <Name>` with its
`/// <summary>`. The `*TypeDocGateTests` in the unit test projects of `Polhem.Web.Blazor`, `.Mud`, `.Fluent` and
`.Server` read the built documentation file and fail on a public type without one; a new package with components needs
its own.

## Before upgrading MudBlazor, verify at run time

The version is pinned exactly in `src/Polhem.Web.Blazor.Mud/Polhem.Web.Blazor.Mud.csproj`. `polhem-mud.css` and some
tests use MudBlazor's internal class names, so **a clean build does not show that an upgrade works**. Before upgrading:

1. Run `tests/Polhem.Web.Blazor.Mud.UnitTests` and `tests/Polhem.Web.Blazor.Mud.BrowserTests`.
2. Run Northwind's Blazor head against its backend: sign-in, a list, in-cell editing, a lookup, a save, the delete
   question, and a phone-sized viewport.

## Before upgrading Fluent UI, verify at run time too

The version is pinned exactly in `src/Polhem.Web.Blazor.Fluent/Polhem.Web.Blazor.Fluent.csproj`. The package works around
behavior that is not documented, each with a `WARNING:` where it is: `PolhemDataGrid`, `PolhemDialog`, the popover in
`GridColumnFilter`, the replay delay in `GridControl`, and the native inputs in the sign-in, the filter and the lookup search. An upgrade can break or remove the need for any of them, so run
`tests/Polhem.Web.Blazor.Fluent.UnitTests` and `tests/Polhem.Web.Blazor.Fluent.BrowserTests` first.
DefineEditor's Fluent UI preview draws with the same package, and its `wwwroot/browser-preview.js` finds a grid's columns
by where the package puts the header class, so open a layout with a detail grid in that preview and click a column
header as well.

## Anything a round trip delays needs a browser test over a slow link

bUnit runs no script and no focus. Focus, key handling and dialogs have failed only in a browser, and some only over a
slow link. The scenarios are abstract classes in `tests/Polhem.Web.Blazor.BrowserTests`, which every UI package derives from over
its own test pages; `SlowTcpProxy` is there as well. A test that passes on localhost says little
about a key pressed before its editor or dialog arrived.
