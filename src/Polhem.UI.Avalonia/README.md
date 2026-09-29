# Polhem.UI.Avalonia

[繁體中文](README.zh-TW.md)

Avalonia control library for desktop (Windows / macOS / Linux), browser (WebAssembly), iOS and Android heads. Renders FormSchema-driven forms with a set of native-control subclasses deeply bound to the definition layer, all backed by the `FormDataObject` view-model.

## Architecture Position

**Layer**: UI (Avalonia heads: desktop, browser, iOS, Android)

Which trim modes are supported and what each head needs is described in
[Platform support](../../docs/en/getting-started/platform-support.md).

Belongs to the `Polhem.UI.*` family: connects to the backend through the static `ClientInfo` (`Polhem.UI.Core`) with a per-process token model. Its package dependencies are in the [dependency map](../../docs/en/architecture/dependency-map.md).

## Target Framework

Single `net10.0` TFM. Lower-bound pins: `Avalonia 12.0.0` + `Avalonia.Controls.DataGrid 12.0.0`; hosts may bring a newer `Avalonia 12.0.x` transitively. The library ships no theme — the host picks one (Semi.Avalonia, Fluent, …); every control keeps the host theme through `StyleKeyOverride`.

## Key Components

| Component | Description |
|---|---|
| `ListView` | Read-only master list of a form; raises view / edit / add requests for the host to open the record surface. Resolves its schema and connector from `ClientInfo` when the host sets only `ProgId`. |
| `FormView` | Top-level container: list (`GridControl`) + master/detail form + New / Save / Delete toolbar. Renders the master sections of a `FormLayout` (one field editor per `LayoutField`) followed by the detail grids (`FormLayout.Details`) directly — there is no separate `DynamicForm` control; `DetailEditMode` picks the detail editing model. Resolves `Schema` / `FormConnector` / `AccessToken` from `ClientInfo` when the host sets only `ProgId`. |
| `GridControl` | `ContentControl` hosting a `DataGrid` (exposed as `InnerGrid`), driven by a `LayoutGrid`; implements `IBindTableControl` / `IUIControl`. Cell rendering goes through `DataGridTemplateColumn` + `FuncDataTemplate<DataRowView>` (see ADR-020). |
| Field editors (`TextEdit` / `MemoEdit` / `ButtonEdit` / `DateEdit` / `DateTimeEdit` / `YearMonthEdit` / `TimeEdit` / `NumericEdit` / `DropDownEdit` / `CheckEdit`) | One per `ControlType` value (`Auto` picks one from the field); native-control subclasses implementing `IBindFieldControl` / `IUIControl`, auto-applying `FormField` metadata (MaxLength, ListItems). |
| `FormScope` | Attached inherited properties (`DataObject` / `FormMode`): set once on a container and every descendant editor with a `FieldName` binds itself. |
| `GridEditMode` | UI-layer editing model for grids: `InCell` (cell editing, ADR-021 hybrid strategy) or `EditForm` (read-only grid + popup row editing). |
| `RowEditPanel` / `RowEditDialog` | EditForm-mode editing surface built from the field editors; commits or cancels through the buffered row-edit protocol. |
| `FormDataObject` | The view-model: carries the `DataSet`, bridges ADO.NET table events into `FieldValueChanged` / dirty tracking, exposes the async CRUD round-trips and the buffered row-edit protocol (`BeginRowEdit` / `CommitRowEdit` / `CancelRowEdit`). |

## Usage

```csharp
using Polhem.Api.Client;
using Polhem.UI.Core;

// Host bootstrap, before any UI control is created.
ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Remote;
// FileEndpointStorage (per-user application data folder) is already the default endpoint and API key
// storage. A browser head assigns ClientInfo.EndpointStorage and ClientInfo.ApiKeyStorage its own
// browser-backed storage here.
// Seeds empty storage on first run; the stored key wins afterwards, so changing it
// is a settings change rather than a rebuild.
ClientInfo.ApplyApiKey("my-app");
```

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:views="using:Polhem.UI.Avalonia.Views"
             xmlns:ed="using:Polhem.UI.Avalonia.Controls.Editors">
  <StackPanel>
    <!-- Full form: one ProgId drives schema, layout, connector and data. -->
    <views:FormView ProgId="Employee" />

    <!-- Hand-written form: set the ambient scope once, editors bind by FieldName. -->
    <StackPanel ed:FormScope.DataObject="{Binding Data}">
      <ed:TextEdit FieldName="emp_name" />
      <ed:DateEdit FieldName="hire_date" />
    </StackPanel>
  </StackPanel>
</UserControl>
```

## Design Notes

- Every control subclass overrides `StyleKeyOverride` to its native base type so the host theme keeps applying (a missing override renders the control invisible).
- `FieldValueChanged` is bridged from the ADO.NET `DataTable` events, so a write through any path (editors, grid cells, direct `DataRow` writes) publishes it; a writer does not need to raise it.
- DataGrid binding and editing strategies are recorded in [ADR-020](../../maintainers/adr/adr-020-avalonia-datagrid-binding-strategy.md) and [ADR-021](../../maintainers/adr/adr-021-avalonia-datagrid-editing-strategy.md).

## Samples

- [`apps/Polhem.Northwind`](../../apps/Polhem.Northwind/README.md) — full Connection → Login → CRUD flow over `FormView`, on the Desktop, Browser, iOS and Android heads.
- [`samples/Avalonia.DemoCenter`](../../samples/Avalonia.DemoCenter/README.md) — theme-oriented control demo center (theme → case nav, Demo/Source tabs, theme/FormMode toolbar): data binding, read-only/required, FormMode, layout, grid, and native-vs-inherited parity.
