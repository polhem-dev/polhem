# Blazor.Mud.DemoCenter

[English](README.md)

`Polhem.Web.Blazor.Mud` 控制項的展示專案，依**主題**編排：左側是導覽樹（主題 → 案例），右側是 `Demo` / `Source` 分頁，上方是淺色／深色切換。與 [`Avalonia.DemoCenter`](../Avalonia.DemoCenter/README.zh-TW.md) 一樣**預設深色**。

每個 DemoCenter 對應一個 UI 框架。各 DemoCenter 的主題、案例標題、模組檔名和範例資料都相同，方便逐案對照同一個控制項的行為。案例直接使用 `Polhem.Web.Blazor.Mud` 與 MudBlazor，不和其他 UI 框架的 DemoCenter 共用程式碼。

DemoCenter 只涵蓋**控制項層**：沒有後端、不用登入，資料全在記憶體。端到端應用見 [`apps/Polhem.Northwind`](../../apps/Polhem.Northwind/README.zh-TW.md)。

## 執行

```bash
dotnet run --project samples/Blazor.Mud.DemoCenter/Blazor.Mud.DemoCenter.csproj
```

網址是 <http://localhost:5055>。每個案例有自己的網址：`/case/<模組名稱>`。

## 主題與案例

順序與說明文字在 [`DemoModuleRegistry`](Modules/DemoModuleRegistry.cs)。下列模組都與 `Avalonia.DemoCenter/Modules` 中的對應模組同名。

| 主題 | 案例 | 與 Avalonia 的差異 |
|------|------|-------------------|
| Control Types | `ControlGalleryModule` | 另外展示 `NumericEdit`、`TimeEdit`、`DateTimeEdit` 和計算欄位 |
| | `FieldControlComparisonModule` | 對照 MudBlazor 的輸入控制項 |
| | `TableControlComparisonModule` | 對照 `MudDataGrid` |
| Data Binding | `AmbientBindingModule`、`ExplicitBindingModule`、`DataObjectEventsModule` | — |
| | `TwoWaySyncModule` | 另外示範從程式碼寫入欄位 |
| Read-only & Required | `ReadOnlyFieldModule` | — |
| | `RequiredReadOnlyMarkModule` | 唯讀欄頭加括號、必填欄頭顯示提示，不用文字顏色區分 |
| FormMode States | `InteractiveFormModeModule`、`FormModeStatesModule`、`GridFormModeModule` | — |
| Lookup | `LookupPickerModule` | 另外以記憶體資料來源執行真正的 `LookupDialog` |
| Layout | `AutoFormLayoutModule`、`MultiColumnLayoutModule` | — |
| Grid | `InCellEditModule` | 儲存格取得焦點時開啟編輯器 |
| | `EditFormModule`、`ListModeModule`、`NumberFormatModule` | — |
| | `AmbientGridModule` | 欄位來自 layout：Blazor 的 grid 不會從資料表產生欄位 |
| | `MultiCurrencyModule`、`MultiUnitModule` | 合計在模組內計算：`AmountColumnSummary` 位於 `Polhem.UI.Avalonia` |
| Master-Detail | `MasterDetailModule` | 使用含計算金額的訂單，可切換明細編輯模式與 form mode |
| Permission Capability | `PermissionCapabilityModule` | 以 `LayoutCapabilityApplier` 套用 grid 的權限 |
| Without a Backend | `WithoutBackendModule` | 以 `GridControl` 和 `FormLayoutPresenter` 組成，見下方說明 |

### 沒有對應的項目

- **Property Grid**（`PropertyGridModule`）：`Polhem.Web.Blazor.Mud` 沒有 `PropertyGridControl`。
- **以自有資料執行 `FormPage`**：`FormPage` 和 `FormDataObject` 的載入、儲存方法都經過 `PolhemApiClient`，所以需要 Polhem 後端；Avalonia 的 `ListView`、`FormView` 則接受 `IFormApiConnector`。`WithoutBackendModule` 示範改用公開控制項能做到的部分，`FormPage` 本身見 `apps/Polhem.Northwind`。

## 新增案例

1. 新增 `Modules/<主題資料夾>/<名稱>Module.razor`，名稱與另一個 DemoCenter 的對應模組相同。
2. 在 [`DemoModuleRegistry`](Modules/DemoModuleRegistry.cs) 加一行。

導覽樹和 `Source` 分頁會自動帶入：專案內嵌 `Modules/**/*.razor`，資源名稱取自元件的完整名稱，所以**資料夾必須與命名空間一致**。
