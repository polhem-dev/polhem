# Blazor rules (core)

> The package split is in `maintainers/adr/adr-059-blazor-shared-layer.md`, and why MudBlazor and why no UI abstraction in
> `maintainers/adr/adr-060-mudblazor-first-ui-package.md`.

## Three kinds of package, and what each may not reference

| Package | Holds | May not reference |
|---------|-------|-------------------|
| `Polhem.Web.Blazor` | State and rules, as abstract component bases | A UI framework, `Microsoft.AspNetCore.App` |
| `Polhem.Web.Blazor.<UI>` (`.Mud`) | Markup and theme for one framework | A host package |
| `Polhem.Web.Blazor.<Host>` (`.Server`) | Wiring for one host | A UI package |

`POLHEM9001`, `BlazorSharedLayerDependencyGateTests` and `MudPackageDependencyGateTests` enforce the references.

**Put a rule in the shared base and only markup in the UI package.** A new behavior gets a test in
`tests/Polhem.Web.Blazor.BehaviorTests`, which every UI package derives from, not a test against one package's markup.
Do not add a generic control interface such as `IGrid`.

## Before upgrading MudBlazor, verify at run time

The version is pinned exactly in `src/Polhem.Web.Blazor.Mud/Polhem.Web.Blazor.Mud.csproj`. `polhem-mud.css` and some
tests use MudBlazor's internal class names, so **a clean build does not show that an upgrade works**. Before upgrading:

1. Run `tests/Polhem.Web.Blazor.Mud.UnitTests` and `tests/Polhem.Web.Blazor.Mud.BrowserTests`.
2. Run Northwind's Blazor head against its backend: sign-in, a list, in-cell editing, a lookup, a save, the delete
   question, and a phone-sized viewport.

## Anything a round trip delays needs a browser test over a slow link

bUnit runs no script and no focus. Focus, key handling and dialogs have failed only in a browser, and some only over a
slow link. Use `SlowTcpProxy` in `tests/Polhem.Web.Blazor.Mud.BrowserTests`. A test that passes on localhost says little
about a key pressed before its editor or dialog arrived.
