# Polhem.UI.Avalonia

[English](README.md)

Avalonia 控制項套件，適用桌面（Windows / macOS / Linux）、瀏覽器（WebAssembly）、iOS 與 Android head。以一組深度綁定定義層的原生控件子類渲染 FormSchema 驅動表單，資料中樞為 `FormDataObject` view-model。

## 架構定位

**層級**：UI（Avalonia head：桌面、瀏覽器、iOS、Android）

支援哪些修剪模式、各 head 需要什麼，見[平台支援](../../docs/zh-TW/getting-started/platform-support.md)。

屬於 `Polhem.UI.*` 家族，建立在 [`Polhem.UI.Core`](../Polhem.UI.Core/README.zh-TW.md) 之上：不參考任何 API client 與傳輸，透過 `IFormApiConnector` 合約（`Polhem.Api.Contracts.Form`）取得資料。head 以 [`Polhem.UI.ApiBridge`](../Polhem.UI.ApiBridge/README.zh-TW.md) 把它接上 Polhem 後端，或以自己的連接器在沒有後端的情況下使用這些控件。理由見 [ADR-054](../../maintainers/adr/adr-054-front-end-decoupled-from-api-client.md)。套件相依見[相依關係圖](../../docs/zh-TW/architecture/dependency-map.md)。

## 目標框架

單一 `net10.0` TFM。下限版本：`Avalonia 12.0.0` + `Avalonia.Controls.DataGrid 12.0.0`；host 可透過 transitive 帶更新的 `Avalonia 12.x`。本套件不內建主題——由 host 自選（Semi.Avalonia、Fluent…），所有控件透過 `StyleKeyOverride` 沿用 host 主題。

## 主要元件

| 元件 | 說明 |
|---|---|
| `ListView` | 表單的唯讀主檔清單；發出檢視 / 編輯 / 新增請求，由 host 開啟單筆畫面。host 只設 `ProgId` 時，依[畫面如何解析服務](#畫面如何解析服務)所述解析 schema 與連接器。 |
| `FormView` | 頂層容器：列表（`GridControl`）+ 主檔／明細表單 + New / Save / Delete 工具列。直接渲染 `FormLayout` 的主檔 sections（每個 `LayoutField` 一個 field editor），其後渲染明細（`FormLayout.Details`）——沒有獨立的 `DynamicForm` 控件；`DetailEditMode` 決定明細編輯模型。host 只設 `ProgId` 時，依[畫面如何解析服務](#畫面如何解析服務)所述解析 `Schema` / `Layout` / `FormConnector`。 |
| `GridControl` | `ContentControl`，內含 `DataGrid`（經 `InnerGrid` 公開），由 `LayoutGrid` 驅動；實作 `IBindTableControl` / `IUIControl`。cell 顯示走 `DataGridTemplateColumn` + `FuncDataTemplate<DataRowView>`（見 ADR-020）。 |
| Field editors（`TextEdit` / `MemoEdit` / `ButtonEdit` / `DateEdit` / `DateTimeEdit` / `YearMonthEdit` / `TimeEdit` / `NumericEdit` / `DropDownEdit` / `CheckEdit`） | 與 `ControlType` 的值一一對應（`Auto` 依欄位挑選）；繼承原生控件並實作 `IBindFieldControl` / `IUIControl`，自動套用 `FormField` metadata（MaxLength、ListItems）。 |
| `FormScope` | 可繼承的 attached properties（`DataObject` / `FormMode`）：容器設一次，子孫編輯器憑 `FieldName` 自動綁定。 |
| `GridEditMode` | grid 的 UI 層編輯模型：`InCell`（逐格編輯，ADR-021 混合策略）或 `EditForm`（唯讀 grid + 彈窗整列編輯）。 |
| `RowEditPanel` / `RowEditDialog` | EditForm 模式的編輯面，由 field editors 組成；經暫存列編輯協定確認或取消。 |
| `ObjectTreeView` | `TreeView` 子類，顯示 `ObjectTreeBuilder`（`Polhem.Definition.ObjectTree`）依 `[TreeNode]` 標註建出的 `ObjectTreeNode` 樹：設定 `RootNode`，可選 `IconSelector`；`IsExpanded` 雙向繫結；`CommandProvider` 以選取節點的命令填入右鍵選單。 |
| `FormDataObject` | view-model：承載 `DataSet`、把 ADO.NET 表事件橋接為 `FieldValueChanged` 與 dirty 追蹤，提供非同步 CRUD 與暫存列編輯協定（`BeginRowEdit` / `CommitRowEdit` / `CancelRowEdit`）。 |

## 使用方式

### 搭配 Polhem 後端

```csharp
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Client;
using Polhem.UI.ApiBridge;
using Polhem.UI.ApiBridge.DependencyInjection;
using Polhem.UI.Core;

// Host 啟動，在建立任何 UI 控件之前。
ClientInfo.SupportedConnectTypes = SupportedConnectTypes.Remote;
// FileEndpointStorage（每位使用者的應用程式資料資料夾）已是 endpoint 與 API 金鑰的預設儲存。
// 瀏覽器 head 在此把 ClientInfo.EndpointStorage 與 ClientInfo.ApiKeyStorage 指派為自己的瀏覽器儲存實作。
// 首次執行時以此值初始化空的儲存；之後以儲存值為準，因此變更它只是改設定、不必重新建置。
ClientInfo.ApplyApiKey("my-app");

// 畫面從這個 provider 解析連接器、定義與使用者情境。
UIServices.Provider = new ServiceCollection().AddPolhemUIApiBridge().BuildServiceProvider();

// false：已儲存的 endpoint 缺漏或無法連線，由 host 顯示自己的連線設定畫面。
bool connected = await ClientInfo.InitializeAsync(SupportedConnectTypes.Remote);
```

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:views="using:Polhem.UI.Avalonia.Views"
             xmlns:ed="using:Polhem.UI.Avalonia.Controls.Editors">
  <StackPanel>
    <!-- 完整表單：一個 ProgId 驅動 schema、layout、連接器與資料。 -->
    <views:FormView ProgId="Employee" />

    <!-- 手寫表單：在容器設一次 scope，編輯器依 FieldName 自動綁定。 -->
    <StackPanel ed:FormScope.DataObject="{Binding Data}">
      <ed:TextEdit FieldName="emp_name" />
      <ed:DateEdit FieldName="hire_date" />
    </StackPanel>
  </StackPanel>
</UserControl>
```

### 不接後端

這些控件不需要 Polhem 後端。在 `UIServices` 中什麼都不註冊，自行在畫面上設定 schema、layout 與連接器即可；
連接器是應用程式以自己的資料實作的 `IFormApiConnector`，`Polhem.UI.Core.Connectors` 中的結果 record
（`FormListResult`、`FormDataResult`、`FormDeleteResult`）省去自己撰寫回應型別：

```csharp
var list = new ListView { Schema = schema, FormConnector = connector };
var record = new FormView { Schema = schema, Layout = layout, FormConnector = connector };
```

若要改從定義檔讀取 schema 與 layout，把 `DefinitionSource` 設為以 `FileDefineStorage` 為基礎的
`StorageDefinitionSource`。完整範例見 demo center 的「Without a Backend」模組：
[`samples/Avalonia.DemoCenter/Modules/WithoutBackend/`](../../samples/Avalonia.DemoCenter/Modules/WithoutBackend/)。

### 畫面如何解析服務

`FormView` 與 `ListView` 依下列順序解析每個服務：自己的屬性（`FormConnector`、`DefinitionSource`），
接著是可覆寫的 `Resolve*` hook（`ResolveFormConnector`、`ResolveSchemaAsync`、`ResolveUserContext` 等），
最後是 `UIServices.Provider` 中註冊的服務（`IFormApiConnectorFactory`、`IFormDefinitionSource`、`IUserContext`）。
什麼都沒註冊就是沒有後端：畫面渲染 host 設定的 `Schema` 與 `Layout`、呼叫 host 設定的 `FormConnector`，
並把使用者視為不受限制。

## 設計備註

- 所有控件子類都覆寫 `StyleKeyOverride` 指向原生基底，host 主題才會持續生效（漏覆寫控件會隱形）。
- `FieldValueChanged` 由 ADO.NET `DataTable` 事件橋接——任何寫入路徑（編輯器、grid cell、直接寫 `DataRow`）都會發布，寫入者不需手動引發。
- DataGrid 的綁定與編輯策略記錄於 [ADR-020](../../maintainers/adr/adr-020-avalonia-datagrid-binding-strategy.md) 與 [ADR-021](../../maintainers/adr/adr-021-avalonia-datagrid-editing-strategy.md)。

## 範例

- [`apps/Polhem.Northwind`](../../apps/Polhem.Northwind/README.zh-TW.md) —— `FormView` 的完整 Connection → Login → CRUD 流程，跑在 Desktop、Browser、iOS 與 Android head。
- [`samples/Avalonia.DemoCenter`](../../samples/Avalonia.DemoCenter/README.zh-TW.md) —— 主題導向控件 demo center（導覽樹 主題→案例、Demo/Source 分頁、主題/FormMode 工具列）：資料繫結、唯讀必填、FormMode、Layout、Grid、原生 vs 繼承比對，以及不接後端的 `ListView` / `FormView`。
