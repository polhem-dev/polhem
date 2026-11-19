# Polhem.UI.Core

> 所有 Polhem 前端共用的前端層：UI 使用的服務合約、各 head 共用的表單值規則，以及權限能力解析器。不含傳輸。

[English](README.md)

## 架構定位

- **層級**：UI 層（共享前端基礎）
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/architecture/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。它們確實漂了：`Polhem.Hosting` 抽出後，有四份 README 的下游數個月都沒把它補上。
- 所有前端都參考它，Blazor 共用層（`Polhem.Web.Blazor`）也不例外。它只透過 `Polhem.Api.Contracts` 的合約接觸後端，既不參考 `Polhem.Api.Client`，也不參考 `Polhem.Api.Core`。理由見 [ADR-054](../../maintainers/adr/adr-054-front-end-decoupled-from-api-client.md)。
- 把單一使用者的 head 接上 Polhem 後端是 [`Polhem.UI.ApiBridge`](../Polhem.UI.ApiBridge/README.zh-TW.md) 的職責，它以 API client 實作下列合約。

## 目標框架

- `net10.0` -- 使用現代執行階段 API 與效能改進

## 概觀

`Polhem.UI.Core` 是所有前端共用、與 UI 框架無關的基礎層。它以合約宣告 UI 需要從外部取得的東西（表單的定義、
登入使用者的情境、每個程式一個連接器），並持有在每個 head 都必須一致的表單值規則。它不含任何 UI 框架型別，
也不含傳輸：建立在它之上的 UI 可以透過 `Polhem.UI.ApiBridge` 對接 Polhem 後端，也可以由應用程式自行實作
`IFormApiConnector`，對接自己的資料。

## 主要型別

### 供 markup 建立的控制項使用的服務定位

- `UIServices` -- 由 XAML loader 建立的控制項用來解析服務的行程層級 `IServiceProvider`（`Provider`）；未設定
  provider 或服務未註冊時，`Get<T>()` 回傳 `null`。單一使用者的 head 在啟動時設定一次；什麼都沒註冊時，
  控制項在沒有後端的情況下運作。在同一行程服務多位使用者的 host 不得使用它。

### 前端服務合約

- `IUserContext` -- 前端眼中的登入使用者：`User`、`Capabilities`（依權限模型）、`Company`，以及部署的
  `DefaultLanguage`。
- `Definitions/IFormDefinitionSource` -- 提供建立表單畫面所需的表單結構、版面，以及幣別與單位主檔。由實作決定語系，
  且回傳的每個實例都屬於呼叫端。
- `Definitions/StorageDefinitionSource` -- 以 `IDefineStorage`（例如讀取本機資料夾的 `FileDefineStorage`）為基礎的
  `IFormDefinitionSource`，供沒有 Polhem 後端的前端使用。它照儲存原樣回傳定義，不套用語系資源，也不套用租戶客製。
- 連接器合約本身 `IFormApiConnector` / `IFormApiConnectorFactory` 位於 `Polhem.Api.Contracts`
  （命名空間 `Polhem.Api.Contracts.Form`），因此 API client 不必參考 UI 就能實作它。

### 自行實作連接器時使用的結果型別

- `Connectors/FormListResult`、`FormDataResult`、`FormDeleteResult` -- 實作 `Polhem.Api.Contracts.Form` 回應介面的
  簡單 record，供不接 Polhem 後端、而是以自己的資料實作 `IFormApiConnector` 的應用程式使用。

### 各 head 共用的表單值規則

- `FormValueBinding` -- 在表單 `DataSet` 中的值與 UI 繫結交換的字串之間轉換，並建立表單起始用的空 `DataSet`。
- `LiveComputationDegradation` -- 計算欄位的即時重算為何停止：運算式失敗的欄位，以及失敗本身。各 head 的
  `FormLiveComputation` 會提供它。重算本身的運算引擎（`Polhem.Core.Expressions.IExpressionEvaluator`）與使用者時區
  都由呼叫端提供，因此本套件既不帶運算引擎，也不持有整個 process 共用的狀態。
- `Criteria/QueryCriteria` -- 使用者逐條加入的查詢條件（`CriteriaField`、`CriteriaRow`、`RelativeDate`），與後端方法無關；
  `GetListFilterConverter` 把它轉成清單請求的 `FilterNode`。請參閱[清單查詢條件](../../docs/zh-TW/guides/list-query-conditions.md)。
- `Criteria/ColumnFilterCriteria` -- 表單清單的欄頭篩選，存成清單 `QueryCriteria` 的條件列，所以查詢窗格看得到。
  原本另外存放的 `Lists/ListFilterState` 已標為過時，框架不再讀取。

### 表單資料物件

- `DataObjects/FormDataObject` -- 各 head 的表單檢視與欄位編輯器綁定的資料物件。由 `FormSchema` 衍生記憶體內
  `DataSet`（主檔列 + 明細資料表），提供 `GetField` / `SetField`（主檔列或指定的資料列）雙向綁定，值變更時觸發
  `FieldValueChanged` 與 `DataSetReplaced`，並透過 `IFormApiConnector` 執行 `LoadAsync` / `SaveAsync` /
  `DeleteAsync` / `NewAsync`。新資料列會依 schema 填入預設值（新的 `sys_rowid`、與主檔的連結、依型別的非 null 值）
  並觸發 `RowAdded`；`BeginRowEdit` / `CommitRowEdit` / `CancelRowEdit` 暫存單一資料列的編輯，提交前只觸發
  `RowEditFieldChanged`，提交時才發布。使用者修改標示 `ValueChangeCallback` 的欄位時，會回呼 business object，把伺服端
  傳回的值寫入同一列，並觸發 `FieldValueChangeApplied` 或 `FieldValueChangeFailed`。
- `DataObjects/FieldValueChangeFilter` -- 綁定的欄位編輯器要對哪些變更做出反應，以及資料集被替換時，綁在單一列上的
  編輯器不重讀該列。
- `Navigation/MenuRows` -- 把選單定義展平成 shell 選單顯示的標題列與表單列。
- `Editing/IFieldEditor`、`Editing/GridEditMode` -- 每個欄位編輯器實作的綁定契約，以及 grid 的編輯模型，各 head 相同。
- 建構它的 head 傳入登入使用者的時區（`timeZoneProvider`），新資料列的日期預設值依此決定；未傳入時為 UTC。
  本套件中除了 `UIServices` 本身，沒有任何程式讀取 `UIServices`，因此它可用在同一個 process 服務多位使用者的 host；
  這點改變時 `UICoreServiceLocatorGateTests`（`tests/Polhem.UI.Core.UnitTests`）會失敗。

### 各 head 共用的欄位編輯器規則

`Polhem.UI.Avalonia` 與 `Polhem.Web.Blazor` 的欄位編輯器與 grid 共用同一套規則，讓各 head 行為一致：欄位可否輸入、
數值與日期如何格式化與剖析、grid 儲存格顯示什麼、查詢選取結果如何寫回，以及關聯欄位何時提供開啟所指記錄。這些規則
（下方的 `TimeFieldFormat` 除外）、表單 CRUD 的前置條件、即時重算引擎與卡片清單狀態，都是框架 UI 套件之間的 internal
型別（見專案檔的 `InternalsVisibleTo`）；應用程式透過各 head 的編輯器、grid 與表單檢視使用它們。

- `TimeFieldFormat` -- 時間欄位編輯器寫回的值。

### 權限能力

- `Permissions/IElementCapabilityResolver`、`ElementCapabilityResolver`、`FieldCapability` -- 把使用者依模型的能力
  轉成元素層級的決策（欄位可見或唯讀）。
- `Permissions/LayoutCapabilityApplier` -- 把這些決策直接套到表單版面或 grid 上，隱藏敏感欄位或設為唯讀；Avalonia 的
  view 與 Blazor 的 `FormPage` 都使用它。

> 用戶端能力解析**僅為 UX 降級**。後端仍是權威的安全邊界。

## 設計慣例

- **不含傳輸** -- 套件只參考 `Polhem.Api.Contracts`。參考 `Polhem.Api.Client`、`Polhem.Api.Core`、
  `Polhem.Api.Server` 或 `Polhem.UI.ApiBridge` 時，`POLHEM9001`（`src/Directory.Build.targets`）會讓建置失敗。
- **與 UI 框架無關** -- 不含 UI 框架型別，因此同一套合約與規則可服務所有前端。
- **畫面的解析順序** -- 控制項先用自己的屬性，再用可覆寫的 `Resolve*` hook，最後才是 `UIServices.Provider`；
  三者皆無時，就以 host 設定的內容運作。
- **行程層級狀態不進入多使用者 host** -- `UIServices` 是靜態的。Blazor 套件使用本套件中 `UIServices` 以外的一切；它若參考 `UIServices` 或 `Polhem.UI.ApiBridge`，`UIServicesUsageGateTests`
  （`tests/Polhem.Web.Blazor.Server.UnitTests`）會失敗；本套件中除了 `UIServices` 本身有任何程式呼叫
  `UIServices`，`UICoreServiceLocatorGateTests`（`tests/Polhem.UI.Core.UnitTests`）會失敗。
- 啟用 **Nullable Reference Types**（`<Nullable>enable</Nullable>`）。

## 目錄結構

- 專案根目錄 -- `UIServices`、`IUserContext`、`FormValueBinding`、`LiveComputationDegradation`
- `DataObjects/` -- `FormDataObject`、`FieldValueChangedEventArgs`、`FieldValueChangeFilter`、`RowChangedEventArgs`
- `Navigation/` -- `MenuRows`
- `Editing/` -- `IFieldEditor`、`GridEditMode`
- `Definitions/` -- `IFormDefinitionSource`、`StorageDefinitionSource`
- `Fields/` -- `TimeFieldFormat`、`RelationTarget`、`FieldValueOrigin`、`FieldValueChangeAppliedEventArgs`、
  `FieldValueChangeFailedEventArgs`
- `Connectors/` -- `FormListResult`、`FormDataResult`、`FormDeleteResult`
- `Criteria/` -- `QueryCriteria`、`CriteriaRow`、`CriteriaField`、`CriteriaOperator`、`RelativeDate`、`GetListFilterConverter`、`ColumnFilterCriteria`
- `Lists/` -- `ListFilterState`、`ListPagingState`、`ColumnFilterKind`
- `Permissions/` -- `IElementCapabilityResolver`、`ElementCapabilityResolver`、`FieldCapability`、`LayoutCapabilityApplier`
