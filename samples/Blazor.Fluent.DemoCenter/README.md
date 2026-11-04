# Blazor.Fluent.DemoCenter

[繁體中文](README.zh-TW.md)

A showcase of the `Polhem.Web.Blazor.Fluent` controls, organized by **topic**: a navigation tree on the left (topic → case), `Demo` / `Source` tabs on the right, and a Light / Dark switch at the top. **Dark by default**, as in [`Avalonia.DemoCenter`](../Avalonia.DemoCenter/README.md).

Each demo center covers one UI framework. They all use the same topics, case titles, module file names and sample data, so a control's behavior can be compared case by case. The cases use `Polhem.Web.Blazor.Fluent` and Fluent UI (`Microsoft.FluentUI.AspNetCore.Components`) directly; nothing is shared with another UI framework's demo center.

The demo center covers **the control layer only**: no backend, no sign-in, all data in memory. For an end-to-end application see [`apps/Polhem.Northwind`](../../apps/Polhem.Northwind/README.md).

## Run it

```bash
dotnet run --project samples/Blazor.Fluent.DemoCenter/Blazor.Fluent.DemoCenter.csproj
```

It listens on <http://localhost:5056>. Each case has its own address, `/case/<module name>`.

## Topics and cases

The order and the descriptions are in [`DemoModuleRegistry`](Modules/DemoModuleRegistry.cs). Every module below has the same name as its counterpart in `Avalonia.DemoCenter/Modules`.

| Topic | Case | Differs from Avalonia |
|-------|------|-----------------------|
| Control Types | `ControlGalleryModule` | Also shows `NumericEdit`, `TimeEdit`, `DateTimeEdit` and a computed field |
| | `FieldControlComparisonModule` | Compares with the Fluent UI inputs. The native button-edit shows a text button: the sample does not reference the Fluent UI icon package |
| | `TableControlComparisonModule` | Compares with `FluentDataGrid` |
| Data Binding | `AmbientBindingModule`, `ExplicitBindingModule`, `DataObjectEventsModule` | — |
| | `TwoWaySyncModule` | Also writes the field from code |
| Read-only & Required | `ReadOnlyFieldModule` | A read-only date or drop-down shows as a read-only text input |
| | `RequiredReadOnlyMarkModule` | A read-only header is parenthesised and a required one shows the cue, instead of colored text |
| FormMode States | `InteractiveFormModeModule`, `FormModeStatesModule`, `GridFormModeModule` | — |
| Lookup | `LookupPickerModule` | Also runs the real `LookupDialog` over an in-memory source |
| Layout | `AutoFormLayoutModule`, `MultiColumnLayoutModule` | — |
| Grid | `InCellEditModule` | A cell opens its editor when it takes the focus |
| | `EditFormModule`, `ListModeModule`, `NumberFormatModule` | — |
| | `AmbientGridModule` | Takes its columns from a layout: the Blazor grid does not generate columns from the table |
| | `MultiCurrencyModule`, `MultiUnitModule` | Compute the totals in the module: `AmountColumnSummary` is in `Polhem.UI.Avalonia` |
| Master-Detail | `MasterDetailModule` | Uses an order with a computed line amount, and switches the detail editing model and the form mode |
| Permission Capability | `PermissionCapabilityModule` | Applies the grid's capability with `LayoutCapabilityApplier` |
| Without a Backend | `WithoutBackendModule` | Assembled from `GridControl` and `FormLayoutPresenter`; see below |

### Not available

- **Property Grid** (`PropertyGridModule`): `Polhem.Web.Blazor.Fluent` has no `PropertyGridControl`.
- **`FormPage` over your own data**: `FormPage` and the load and save methods of `FormDataObject` go through `PolhemApiClient`, so they need a Polhem backend. The Avalonia `ListView` and `FormView` take an `IFormApiConnector`. `WithoutBackendModule` shows what the public controls can do instead; `FormPage` itself runs in `apps/Polhem.Northwind`.

## Adding a case

1. Add `Modules/<topic folder>/<Name>Module.razor`, named as its counterpart in the other demo center.
2. Add a line to [`DemoModuleRegistry`](Modules/DemoModuleRegistry.cs).

The navigation tree and the `Source` tab pick it up: the project embeds `Modules/**/*.razor`, and the resource name follows the component's full name, so **the folder must match the namespace**.
