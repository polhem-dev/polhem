# Polhem.UI.Avalonia: UI architecture role and control rules

This file loads automatically when an agent touches any file under `src/Polhem.UI.Avalonia/` (nested `CLAUDE.md`
files are lazily loaded).

**The package version compatibility rule stays in `.claude/rules/avalonia.md` (always loaded)**: it applies to
**any csproj** of the four Avalonia heads (this project, `tools/DefineEditor`, `samples/Avalonia.*`,
`apps/Polhem.Northwind`), not only here.

For the trim / AOT pitfalls of the mobile heads (`net10.0-ios` / `net10.0-android`) see
`.claude/rules/apple-mobile-trim.md`.

## Default theme: Semi.Avalonia

Avalonia UI development **always uses Semi.Avalonia by default** (MIT; the user's decision, 2026-06-10). Do not use
FluentTheme and do not build your own color dictionaries. Entry point:
`<StyleInclude Source="avares://Semi.Avalonia/Index.axaml"/>` (in v12 it is at the assembly root); light/dark goes
through `RequestedThemeVariant`; semantic colors use Semi tokens (`SemiColorText0-3` / `SemiColorBackground0-4` /
`SemiColorBorder` / `SemiColorWarning` / `SemiColorDanger`); the primary action button is `Classes="Primary"` +
`Theme="{DynamicResource SolidButton}"`. Reference implementation: `tools/DefineEditor`. The colours on top of Semi
(VS Code's Dark / Light (Visual Studio)) live once, in `Themes/VisualStudioTheme`; DefineEditor and the shell apply
it, so change them there rather than in either head.

## Role in the UI architecture

This project is the **reference design** of the framework's UI architecture (**field editors that inherit native
controls + a composed GridControl + a FormView/ListView View layer + the lookup dialog mechanism**).

- **When designing a new control, default to "inherit the native control and override it"**. Do not suggest the
  opposite direction of "vanilla control + external binding/behavior": the control itself must understand the
  metadata of `FormField`/`FormSchema` (`MaxLength` / `ListItems` / `ReadOnly` / relation→lookup), and
  schema-driven behavior must be **built into the subclass**.
- **When evaluating structure or naming decisions, do not object on the grounds of "aligning with Blazor"**:
  Avalonia is the leading reference implementation and may deliberately diverge; its decisions are rather the
  template Blazor.Server will follow later.
- The UI families have converged on **two tracks, Avalonia + Blazor.Server** (`Polhem.UI.Maui` /
  `Polhem.Web.Blazor.Wasm` have been removed), so **"port to another UI family" has only one target left,
  Blazor.Server**.
- **Head-agnostic logic goes to `Polhem.UI.Core`**, which every front end references, Blazor.Server included: the
  `FormDataObject` value rules live there once, in `FormValueBinding` and `FormDataGuard`. Blazor must not use
  `UIServices` (process-wide) or reference `Polhem.UI.ApiBridge` (single-user); `UIServicesUsageGateTests` enforces it.
- **This project never references the API client.** Views reach the backend through `IFormApiConnector`
  (`Polhem.Api.Contracts`) and resolve their services as: their own property, then the `Resolve*` hook, then
  `UIServices.Provider`; with nothing registered they run without a backend. `POLHEM9001` blocks a reference to
  `Polhem.Api.Client`, `Polhem.Api.Core` or `Polhem.UI.ApiBridge`. See
  `../../maintainers/adr/adr-054-front-end-decoupled-from-api-client.md`.

## Acceptance baseline for control behavior

`samples/Avalonia.DemoCenter` is the showcase for this project's controls and also **the alignment baseline**: when
porting a control to another UI head, the behavior of each of its cases (binding, read-only, required, FormMode,
AllowEditModes, Layout, Grid, Master-Detail) is the acceptance baseline. Verify changes to a control's appearance
visually here first, then carry them back to the other platforms.

## Known pitfalls

Proven pitfalls (centering with Stretch+MaxWidth, a control's semantic events not firing when the value is set from
code, the DataGrid editing pipeline conflicting with popup editors, the mandatory `StyleKeyOverride` fix, display
desync caused by template recycling, hidden parts of the read-only appearance template…) are in
`../../maintainers/gotchas/avalonia-controls.md`. **Read it before changing this project's controls.**

> The user's preference: **a change is ready to hand over once it compiles; the user launches and tests it
> themselves** (an agent driving the UI is too slow).
