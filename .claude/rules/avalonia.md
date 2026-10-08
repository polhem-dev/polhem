# Avalonia rules (core)

> Default theme (Semi.Avalonia), UI architecture positioning, control acceptance criteria, pointers to control pitfalls
> → `src/Polhem.UI.Avalonia/CLAUDE.md` (loaded automatically when you touch that project).
> Trim / AOT pitfalls for mobile heads (`net10.0-ios` / `net10.0-android`) → `rules/apple-mobile-trim.md`.

This file keeps only the one rule that applies **across all four Avalonia heads**: `src/Polhem.UI.Avalonia`,
`tools/DefineEditor`, `samples/Avalonia.*`, `apps/Polhem.Northwind`.

## Before upgrading Avalonia / UI packages, assess compatibility of the related packages first

**Compatibility beats "upgrade to latest".** Before upgrading any Avalonia / UI package (the Avalonia core family,
`Avalonia.Controls.DataGrid`, `Semi.Avalonia`, `Semi.Avalonia.DataGrid`, any third-party theme / control library),
assess the compatibility of **all related UI packages**:

1. **Version availability**: does every related package have a matching target version? Third-party themes
   (such as Semi) often lag behind, and the Avalonia core and its companion packages **are released
   asynchronously by nature**, so identical version numbers are not required.
2. **Runtime compatibility**: does the theme / control render incorrectly or misalign on the new core?
   **Builds ≠ compatible**: 0 errors across the whole solution still does not prove the Semi theme works on the new
   core. This layer is only visible at runtime.

### Upgrade principles

- **No hard "same version number" rule.** It is impossible under asynchronous releases; dependencies use NuGet
  min-version semantics, so mixed versions restore, but a successful restore does not mean the theme is compatible.
- **Do not blindly upgrade the core to latest.** When Semi lags behind, prefer keeping the whole set on **the version
  line `Semi.Avalonia` supports** for stability. Do not let the Avalonia core get ahead of Semi.
- **When to upgrade the whole set together**: once every related package has a matching release and you can
  actually verify at runtime that the theme renders correctly, upgrade the whole set together.
- Before upgrading, scan the whole repository for references so nothing is missed (across `src/`, `tools/`,
  `samples/`, `apps/`):

```bash
grep -rn "Include=\"\(Avalonia\|Semi\)" --include="*.csproj" .
```

> On 2026-07-09 the core was fully upgraded to 12.1.0 (8 csproj files built with 0 errors), and it was still
> **stopped and rolled back** while wrapping up. The reason: Semi.Avalonia was at 12.0.3, its theme styles were built
> against the old core, and the visual risk was not worth taking.
> This is a real case of "compatibility beats upgrading to latest".
>
> On 2026-10-07 the whole set moved to the 12.1 line once Semi.Avalonia 12.1.0.1 existed. Every build was clean, and
> the runtime check still found a regression no build or test showed: Avalonia 12.1 pins the UI thread's culture
> after each dispatcher operation, so the signed-in user's culture stopped applying (an en-US account showed the
> operating system's Chinese). The fix is in `ClientInfo.ApplyCulture`. This is why the runtime check is not optional.
