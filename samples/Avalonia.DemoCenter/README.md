# Avalonia.DemoCenter

[繁體中文](README.zh-TW.md)

A showcase of the `Polhem.UI.Avalonia` controls, organized by **topic** in the style of the DevExpress Demo Center: a
navigation tree on the left (topic → case), `Demo` / `Source` tabs on the right, and a global toolbar at the top
(Light / Dark theme). **Dark by default.** Switching the FormMode is not on the global toolbar; it lives inside the
"FormMode States" topic, so it does not drive unrelated cases.

Each case shows **a single topic** (data binding, read-only, FormMode…), keeping to the DevExpress idea that each demo
says one thing.

## Role

| Role | Description |
|------|-------------|
| Showroom | The front door where framework users browse what the controls can do |
| Living documentation / alignment baseline | The reference behavior and appearance of each control and concept; the template to align with when porting to other UI heads |
| Development check | Visual feedback when changing the appearance of a control (such as removing the border of a read-only field) |

The Demo Center covers **the control layer only**. It has no back end and no login or CRUD flow. For an end-to-end
application (sign-in, connection, JSON-RPC, rendering forms) see [`apps/Polhem.Northwind`](../../apps/Polhem.Northwind/README.md).

## Prerequisites

- .NET 10 SDK
- No back end and no database: the data comes from an in-memory `FormSchema` + `FormDataObject`

## Run it

```bash
dotnet run --project samples/Avalonia.DemoCenter/Avalonia.DemoCenter.csproj
```

## Global toolbar

- **Theme switch**: the ToggleSwitch at the top right switches between Light and Dark (using `Semi.Avalonia`; Dark by
  default).

> Switching the FormMode (View / Add / Edit) is **not** on the global toolbar. It belongs only to the "FormMode States"
> topic (an interactive switching case and three pinned columns to compare) and should not drive unrelated cases. The
> other cases use Edit mode (editable).

## Topics and cases

The navigation tree has two levels: **topic (`Category`) → case (`Title`)**. The table follows the order in
[`DemoModuleRegistry`](Modules/DemoModuleRegistry.cs).

| Topic | Case | Focus |
|-------|------|-------|
| **Control Types** | Control gallery | One derived control per `ControlType`, editable, with live values |
| | Native vs derived (BindFieldControl) | Field-level controls (`IBindFieldControl` / `IFieldEditor`) side by side with native ones, normal and read-only |
| | Native vs derived (BindTableControl) | The table-level control (`IBindTableControl`: `GridControl`) side by side with the native `DataGrid` |
| **Data Binding** | Ambient binding | The container sets `FormScope.SetDataObject` once; child editors only set `FieldName` and bind automatically |
| | Explicit binding | `editor.Bind(dataObject, layoutField)` without the ambient scope |
| | Two-way sync | Two controls bound to the same field sync once an edit is committed by leaving the control or pressing Enter (`FormDataObject` is the single source) |
| | DataObject events | A live log of `FieldValueChanged` / `RowAdded` / `RowDeleted` / `IsDirtyChanged` / `DataSetReplaced` |
| **Read-only & Required** | LayoutField.ReadOnly | Permanently read-only: the border is removed and an underline remains; `CheckEdit` gets a grey box and keeps its text |
| | Required / read-only markers | `GridControl` header colors: read-only brown, required blue (colored by the library) |
| **FormMode States** | Interactive switching | A FormMode drop-down drives the read-only / editable state of a set of controls and a detail grid live (the only place the FormMode is switched) |
| | Controls × the three FormModes (with AllowEditModes) | Three columns pinned to View / Add / Edit; fields carry different `AllowEditModes` (All / Add / Edit / None) to show how each control renders and how editing is gated per field |
| | Grid × FormMode | How editing and toolbar visibility of `GridControl` differ across the three modes |
| **Lookup** | ButtonEdit lookup picker | The icon opens a local picker that writes the value back (the production flow, `RelationProgId` → `LookupDialog` against a back end, runs in `apps/Polhem.Northwind`) |
| **Layout** | FormLayout generated at design time | `FormLayoutGenerator.Generate` produces sections and field placement from the schema as a design-time starting point |
| | Multi-column layout (ColumnCount / ColumnSpan) | `ColumnCount=2`, with fields spanning columns through `ColumnSpan` |
| **Grid** | In-cell editing | Double-click a cell, or swap in a popup editor (the strategy is in [ADR-021](../../docs/adr/adr-021-avalonia-datagrid-editing-strategy.md)) |
| | EditForm dialog | The grid is read-only; a dialog edits the whole row |
| | Ambient binding | Only `TableName` is set; the grid binds automatically and generates its columns |
| | List mode (read-only list) | Bound to a standalone `DataTable`: a read-only list with the toolbar hidden |
| | Number formatting | Display decimals resolved from each column's `NumberKind`, from the row's unit, and from the company's overrides |
| | Multi-currency amounts | Amount decimals follow the row's currency; totals per currency and in the home currency |
| | Multi-unit quantities | Quantity decimals follow the row's unit; the total is shown only when every row has the same unit |
| **Master-Detail** | Master + detail | `FormLayoutRenderer` renders a master section and a detail grid |
| **Permission Capability** | Interactive permission simulator (master/detail) | Ticking simulated role grants degrades a master and its detail live, as the capability snapshot from `EnterCompany` would |

> **The Views (`FormView` / `ListView`)**: both are coupled to a back end, and the Demo Center has none. So it uses
> "`FormDataObject` as the view model + sample data → bound UI", rendered with the same public primitives as the
> production `FormView` (`FieldEditorFactory` + `GridControl`); see the Master-Detail, Layout and Grid topics. Loading,
> saving and row events against a back end are shown in `apps/Polhem.Northwind`.

## Module architecture (IDemoModule)

Each case is an [`IDemoModule`](Modules/IDemoModule.cs) (`Category` / `Title` / `Description` / `BuildView()` /
`GetSourceText()`), registered in one place, [`DemoModuleRegistry`](Modules/DemoModuleRegistry.cs). The navigation tree
is generated from the registry, grouped into two levels by `Category`.

**View Source**: the `Demo` / `Source` tabs on the right. `Source` shows the module's own `.cs` file:
[`DemoModuleBase`](Modules/DemoModuleBase.cs) reads it in `GetSourceText()` from an embedded resource (the csproj embeds
`Modules/**/*.cs`), so what it shows is the code that actually runs.

## Adding a case

1. Implement `DemoModuleBase` in `Modules/<topic folder>/`, overriding `Category` / `Title` / `Description` /
   `BuildView()`.
2. Add a line to the `Modules` list in [`DemoModuleRegistry`](Modules/DemoModuleRegistry.cs).

The navigation tree and View Source pick it up automatically: `Modules/**/*.cs` is already an EmbeddedResource, and
`GetSourceText()` resolves the resource by the type's full name (**the folder must match the namespace**). Useful
helpers: `DataEditorParts` (single-field object / section card / live values / Compose), `SampleFormData` (Staff +
Phones sample data), `FormLayoutRenderer` (renders a layout with the public primitives).

## Matching library components

| Demo behavior | Library component |
|---------------|-------------------|
| Derived controls keep the Semi styles | [src/Polhem.UI.Avalonia/Controls/Editors/](../../src/Polhem.UI.Avalonia/Controls/Editors/) (`StyleKeyOverride` on each control) |
| Ambient binding (set once on the container) | [FormScope.cs](../../src/Polhem.UI.Avalonia/Controls/Editors/FormScope.cs) |
| FormMode / AllowEditModes drive read-only | `AllowsEdit` / `OnFormModeChanged` in [FieldEditorBinder.cs](../../src/Polhem.UI.Avalonia/Controls/Editors/FieldEditorBinder.cs) |
| Field values refresh live | The `FieldValueChanged` event of [FormDataObject.cs](../../src/Polhem.UI.Avalonia/DataObjects/FormDataObject.cs) |

## Theme / FormMode self-test matrix

Go through the cases one by one visually (the code is verified to build and start; visual consistency needs human
eyes):

| Dimension | Where to switch | What to look for |
|-----------|-----------------|------------------|
| Light / Dark | ToggleSwitch at the top right | In both variants, the background, border and text color of derived controls match the native ones, with no out-of-place color blocks |
| The three FormModes | FormMode States → Interactive switching | View → borders removed and read-only, ButtonEdit icon hidden, grid read-only; Add / Edit → editable |
| AllowEditModes | FormMode States → Controls × the three FormModes | Three columns pinned to View / Add / Edit; each field enabled or disabled according to its `AllowEditModes` |
| View Source | `Demo` / `Source` tabs | The Source of each case shows the module's real `.cs`, consistent with the Demo |

> Theme scope: only `Semi.Avalonia` × Light / Dark. Switching to other themes such as Fluent at run time is left out.
> The purpose of the Demo Center is to be the alignment baseline for the behavior and appearance of the controls;
> switching theme engines is a different subject, and including it would only dilute that signal.

## The alignment baseline for other UI heads

`Polhem.UI.Avalonia` is the pilot of the UI architecture: derived controls and the View layer are settled here first,
then ported to the other UI heads (`Polhem.Web.Blazor.Server`, and WinForms / WPF in the future). The Demo Center is the
template to align with:

- The **reference behavior and appearance** of each control and concept (binding, read-only, required, FormMode,
  AllowEditModes, Layout, Grid) can be seen and compared in one place.
- When a matching control is implemented in another UI head, the behavior of each case here is the acceptance baseline:
  with the same schema, sample data and FormMode, binding and state changes should look the same on every platform.
- Changes to the appearance of a control (such as removing the border of a read-only field) are checked visually here
  first, then carried over to the other platforms.
