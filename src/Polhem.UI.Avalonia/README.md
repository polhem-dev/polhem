# Polhem.UI.Avalonia

[繁體中文](README.zh-TW.md)

Avalonia control library for desktop (Windows / macOS / Linux), browser (WebAssembly), iOS and Android heads. Renders FormSchema-driven forms with a set of native-control subclasses deeply bound to the definition layer, all backed by the `FormDataObject` view-model.

## Architecture Position

**Layer**: UI (Avalonia heads: desktop, browser, iOS, Android)

Which trim modes are supported and what each head needs is described in
[Platform support](../../docs/en/getting-started/platform-support.md).

Belongs to the `Polhem.UI.*` family and builds on [`Polhem.UI.Core`](../Polhem.UI.Core/README.md): it references no API client and no transport, and reaches its data through the `IFormApiConnector` contract (`Polhem.Api.Contracts.Form`). A head connects it to a Polhem backend with [`Polhem.UI.ApiBridge`](../Polhem.UI.ApiBridge/README.md), or uses the controls without a backend over a connector of its own. The reasons are in [ADR-054](../../maintainers/adr/adr-054-front-end-decoupled-from-api-client.md). Its package dependencies are in the [dependency map](../../docs/en/architecture/dependency-map.md).

## Target Framework

Single `net10.0` TFM. Lower-bound pins: `Avalonia 12.0.0` + `Avalonia.Controls.DataGrid 12.0.0`; hosts may bring a newer `Avalonia 12.x` transitively. The library ships no theme — the host picks one (Semi.Avalonia, Fluent, …); every control keeps the host theme through `StyleKeyOverride`.

## Key Components

| Component | Description |
|---|---|
| `ListView` | Read-only master list of a form; raises view / edit / add requests for the host to open the record surface. Resolves its schema and connector as described under [How the views resolve their services](#how-the-views-resolve-their-services) when the host sets only `ProgId`. |
| `FormView` | Top-level container: list (`GridControl`) + master/detail form + New / Save / Delete toolbar. Renders the record through a `FormLayoutPresenter` and binds it to the loaded data; `DetailEditMode` picks the detail editing model. Resolves `Schema` / `Layout` / `FormConnector` as described under [How the views resolve their services](#how-the-views-resolve-their-services) when the host sets only `ProgId`. |
| `FormLayoutPresenter` | Renders the body of a form from a `FormLayout` alone: the master sections (one field editor per `LayoutField`, laid out by `ColumnCount` and the spans) followed by the detail grids (`FormLayout.Details`). Needs no data, schema or backend; see [Previewing a layout](#previewing-a-layout). |
| `GridControl` | `ContentControl` hosting a `DataGrid` (exposed as `InnerGrid`), driven by a `LayoutGrid`; implements `IBindTableControl` / `IUIControl`. Cell rendering goes through `DataGridTemplateColumn` + `FuncDataTemplate<DataRowView>` (see ADR-020). |
| Field editors (`TextEdit` / `MemoEdit` / `ButtonEdit` / `DateEdit` / `DateTimeEdit` / `YearMonthEdit` / `TimeEdit` / `NumericEdit` / `DropDownEdit` / `CheckEdit`) | One per `ControlType` value (`Auto` picks one from the field); native-control subclasses implementing `IBindFieldControl` / `IUIControl`, auto-applying `FormField` metadata (MaxLength, ListItems). |
| `FormScope` | Attached inherited properties (`DataObject` / `FormMode`): set once on a container and every descendant editor with a `FieldName` binds itself. |
| `GridEditMode` | UI-layer editing model for grids: `InCell` (cell editing, ADR-021 hybrid strategy) or `EditForm` (read-only grid + popup row editing). |
| `RowEditPanel` / `RowEditDialog` | EditForm-mode editing surface built from the field editors; commits or cancels through the buffered row-edit protocol. |
| `ObjectTreeView` | `TreeView` subclass that shows an `ObjectTreeNode` tree built by `ObjectTreeBuilder` (`Polhem.Definition.ObjectTree`) from the `[TreeNode]` annotations: set `RootNode`, optionally `IconSelector`; `IsExpanded` binds both ways; `CommandProvider` fills the context menu with the selected node's commands. |
| `FormDataObject` | The view-model: carries the `DataSet`, bridges ADO.NET table events into `FieldValueChanged` / dirty tracking, exposes the async CRUD round-trips and the buffered row-edit protocol (`BeginRowEdit` / `CommitRowEdit` / `CancelRowEdit`). |

## Usage

### With a Polhem backend

```csharp
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Client;
using Polhem.UI.ApiBridge;
using Polhem.UI.ApiBridge.DependencyInjection;
using Polhem.UI.Core;

// Host bootstrap, before any UI control is created.
ClientInfo.SupportedConnectTypes = SupportedConnectTypes.Remote;
// FileEndpointStorage (per-user application data folder) is already the default endpoint and API key
// storage. A browser head assigns ClientInfo.EndpointStorage and ClientInfo.ApiKeyStorage its own
// browser-backed storage here.
// Seeds empty storage on first run; the stored key wins afterwards, so changing it
// is a settings change rather than a rebuild.
ClientInfo.ApplyApiKey("my-app");

// The views resolve their connector, definitions and user context from this provider.
UIServices.Provider = new ServiceCollection().AddPolhemUIApiBridge().BuildServiceProvider();

// false: the stored endpoint is missing or unreachable, so the host shows its own connection setup.
bool connected = await ClientInfo.InitializeAsync(SupportedConnectTypes.Remote);
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

### Without a backend

The controls do not need a Polhem backend. Register nothing in `UIServices`, and set the schema, the layout and a
connector on the views yourself; the connector is your own `IFormApiConnector` over the application's data, and the
result records in `Polhem.UI.Core.Connectors` (`FormListResult`, `FormDataResult`, `FormDeleteResult`) save writing
response types:

```csharp
var list = new ListView { Schema = schema, FormConnector = connector };
var record = new FormView { Schema = schema, Layout = layout, FormConnector = connector };
```

To read the schema and layout from definition files instead, set `DefinitionSource` to a
`StorageDefinitionSource` over a `FileDefineStorage`. A complete example is the "Without a Backend" module of the demo
center, [`samples/Avalonia.DemoCenter/Modules/WithoutBackend/`](../../samples/Avalonia.DemoCenter/Modules/WithoutBackend/).

### Previewing a layout

`FormLayoutPresenter` draws what `FormView` draws for a layout, with no record behind it, so a design tool can show a
layout while it is being edited:

```csharp
var preview = new FormLayoutPresenter { Layout = layout };
FormScope.SetFormMode(preview, SingleFormMode.Edit);
```

Without data, the editors still follow the layout's `ReadOnly` and `AllowEditModes` under the form mode, and the detail
grids show the toolbar their `AllowActions` allow, with the buttons disabled. What comes from the schema or the data
(drop-down items, field values, lookup text, computed fields) is not shown. Changing a property renders again; after
changing the layout in place, call `Refresh()`. `IsCompact` renders the phone-sized form.

### How the views resolve their services

`FormView` and `ListView` resolve each service in this order: their own property (`FormConnector`,
`DefinitionSource`), then the overridable `Resolve*` hook (`ResolveFormConnector`, `ResolveSchemaAsync`,
`ResolveUserContext`, …), then the service registered in `UIServices.Provider` (`IFormApiConnectorFactory`,
`IFormDefinitionSource`, `IUserContext`). With nothing registered there is no backend: the views render the `Schema`
and `Layout` the host set, call the `FormConnector` the host set, and treat the user as unrestricted.

## Design Notes

- Every control subclass overrides `StyleKeyOverride` to its native base type so the host theme keeps applying (a missing override renders the control invisible).
- `FieldValueChanged` is bridged from the ADO.NET `DataTable` events, so a write through any path (editors, grid cells, direct `DataRow` writes) publishes it; a writer does not need to raise it.
- DataGrid binding and editing strategies are recorded in [ADR-020](../../maintainers/adr/adr-020-avalonia-datagrid-binding-strategy.md) and [ADR-021](../../maintainers/adr/adr-021-avalonia-datagrid-editing-strategy.md).

## Samples

- [`apps/Polhem.Northwind`](../../apps/Polhem.Northwind/README.md) — full Connection → Login → CRUD flow over `FormView`, on the Desktop, Browser, iOS and Android heads.
- [`samples/Avalonia.DemoCenter`](../../samples/Avalonia.DemoCenter/README.md) — theme-oriented control demo center (theme → case nav, Demo/Source tabs, theme/FormMode toolbar): data binding, read-only/required, FormMode, layout, grid, native-vs-inherited parity, and `ListView` / `FormView` without a backend.
