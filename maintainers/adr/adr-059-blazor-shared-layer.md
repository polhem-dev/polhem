# ADR-059: The Blazor head splits into a shared layer, UI packages and host packages

## Status

**Accepted (2026-11-01)**

## Context

`Polhem.Web.Blazor.Server` holds three things in one package: the state and rules of forms and editors, controls drawn
in plain HTML, and ASP.NET Core wiring. That blocks two directions the Blazor head is taking:

- **WebAssembly.** The package references `Microsoft.AspNetCore.App`, which cannot load in the browser.
- **A mature UI framework.** The Avalonia head builds on mature controls and adds definition-driven behavior. The
  Blazor head should work the same way, and more than one UI framework may be offered later.

## Decision

### 1. Three kinds of package

| Package | Holds | May not reference |
|---------|-------|-------------------|
| `Polhem.Web.Blazor` | Host-independent and UI-framework-independent state and rules | A UI framework, `Microsoft.AspNetCore.App`, a host package |
| `Polhem.Web.Blazor.<UI>` (`.Mud` first) | Markup and theme for one UI framework | A host package |
| `Polhem.Web.Blazor.<Host>` (`.Server`) | Wiring for one host | A UI package |

An application chooses one UI package and one host package, so a new UI framework or host adds one package rather than
one per combination.

### 2. Behavior is shared, markup is not

There is no generic `IGrid` or `ITextInput`. UI frameworks differ too much in grids, focus handling and input semantics
for a common interface to offer more than their lowest common denominator. Each UI package writes its own markup on
top of the shared state.

### 3. The API client and the expression engine stay in the shared layer

Every Blazor host is an API client, so `Polhem.Api.Client` belongs here. The rule in ADR-054 that keeps the transport
out of `Polhem.UI.Core` protects front ends that may have no backend, which does not describe this head.

`Polhem.Expressions` is here as well, because both hosts need live computation. The Avalonia browser head already runs
the same engine in WebAssembly under the rules in `rules/serialization.md`.

### 4. Names

A UI package's last namespace segment differs from the framework's root namespace: `.Mud` rather than `.MudBlazor`.
Inside a namespace ending in `MudBlazor`, the name `MudBlazor.X` resolves to that namespace.

## Enforcement

- `POLHEM9001` (`src/Directory.Build.targets`) fails the build when `Polhem.Web.Blazor` gains a reference outside its
  allowlist, or references the `Microsoft.AspNetCore.App` framework.
- `BlazorSharedLayerDependencyGateTests` walks the transitive closure for UI framework and host packages.

## Consequences

- Moved types change namespace, and `CircuitUserContext` is renamed `BlazorUserContext`, since a WebAssembly host has
  no circuit. Its constructor takes the default language instead of the host's options. These are breaking changes in a
  1.x minor version (ADR-052).
- The hand-drawn controls stay in `Polhem.Web.Blazor.Server` until the applications move to a UI package, so they keep
  working during the migration.
- WebAssembly downloads now include MessagePack and DynamicExpresso through the shared layer. The Avalonia browser head
  already does.

## Shared behavior tests

`tests/Polhem.Web.Blazor.BehaviorTests` holds abstract tests of what a UI package's editors and grid must do. The library references the shared layer and no UI package. Each UI package's test project derives from them and supplies how to find and operate its controls, as `tests/Polhem.Web.Blazor.Server.UnitTests/Behavior` does for the HTML controls.
