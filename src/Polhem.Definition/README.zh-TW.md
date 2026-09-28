# Polhem.Definition

[English](README.md)

定義驅動架構的核心型別庫，以結構化定義描述表單、資料庫、設定與畫面佈局。

## 架構定位

**層級**：基礎設施層

Polhem.Definition 位於 Polhem 框架的最底層，提供所有上層共用的型別系統。它定義了定義驅動架構的「語言」——每一個表單、資料庫表、UI 佈局與系統設定，都透過此處宣告的型別來表達。

此套件不包含商業邏輯：介面、POCO、列舉與屬性標籤。此處的 API 異動會向上波及整個技術堆疊，因此 API 表面採保守演進策略。

但它**目前並非零 I/O**，這點在推敲分層時有差別。`Storage/`（檔案式定義儲存）、
`Security/MasterKeyProvider`、`PathOptions` / `CustomizeOnlyPathOptions` 與 `Defaults`
會讀寫磁碟上的檔案。要把它們搬走屬於資料遷移而非重構 —— 部署的 `SystemSettings.xml` 可以用
assembly-qualified name 指定儲存型別（`BackendComponents.DefineStorage`），搬家必須配套舊型別名的
相容對映 —— 在那之前它們留在這裡。

- **層級**：最底層 —— 所有上層共用的型別系統。
- **相依**：由建置期閘門 **POLHEM9001** 鎖定在一份明確的允許清單上。加在這裡的任何東西都會被框架的
  每一個消費者繼承，因此放寬清單是一個刻意的決策，記錄於
  [ADR-038](../../docs/adr/adr-038-definition-dependency-boundary.zh-TW.md)。目前的相依圖見
  [相依關係圖](../../docs/zh-TW/dependency-map.md) —— 本檔不複寫一份，因為第二份就是第二個要維護對的東西。

## 目標框架

| 框架 | 用途 |
|------|------|
| `net10.0` | 使用最新執行時期的最佳化與 API |

## 主要功能

- **FormSchema 作為定義中樞** — 單一 FormSchema 同時驅動 UI 渲染（FormLayout）、資料庫投影（TableSchema）與驗證規則，消除跨層規格不一致的問題。
- **結構化篩選與排序模型** — `FilterCondition` 與 `FilterGroup` 組成樹狀查詢模型，並提供工廠方法（`Equal`、`Contains`、`Between`、`In` 等），實現型別安全的查詢建構。
- **可序列化但不帶傳輸相依** — 型別只帶 XML 標註（對應磁碟上的定義檔），沒有別的。它與 API wire 的綁定以手寫 formatter 的形式住在 `Polhem.Api.Core`，因此定義層不會相依任何傳輸格式（[ADR-036](../../docs/adr/adr-036-wire-serialization-externalized.zh-TW.md)）。
- **DI 注入的執行時期服務** — `IDefineAccess`、`ISessionInfoService`、`IDatabaseSettingsProvider`、`IApiEncryptionKeyProvider`、`IAccessTokenValidator` 等介面在此宣告，於 host 啟動時由 `AddPolhemFramework` 註冊到 DI 容器，使 Definition 層與具體實作解耦。
- **安全合約** — `IAccessTokenValidator`、`IApiEncryptionKeyProvider` 等介面定義安全邊界，不強制綁定實作細節。
- **DefineType 驅動的 CRUD** — `DefineType` 列舉與 `DefineTypeExtensions.ToClrType()` 擴充方法將定義類別對應至 CLR 型別，透過 `IDefineAccess` 與 `IDefineStorage` 實現泛型載入/儲存。
- **集中式設定模型** — `SystemSettings`、`DatabaseSettings`、`ProgramSettings` 與 `MenuSettings` 提供具型別的組態介面，取代零散的鍵值查詢。`ProgramSettings` 是框架的型別註冊表:每個 progId 一筆攤平項目,綁定其商業物件(`ProgramItem.BusinessObject`)與 Repository(`ProgramItem.Repository`),任一留空則回退框架預設。導覽選單由 `MenuSettings` 承接,註冊表不再兼任(見 [ADR-034](../../docs/adr/adr-034-progid-type-registry.zh-TW.md))。
- **多租戶客製化覆蓋層** — `ICustomizeDefineReader` + `CustomizeOnlyStorage` 在 base 定義之上提供 per-租戶唯讀覆蓋層，僅服務 Language / FormLayout / ProgramSettings / MenuSettings / PluginSettings（`CustomizeOnlyPathOptions`），由 `SessionInfo.CustomizeId` 驅動。疊加層在快取的 base 定義旁讀取覆蓋內容，而不是寫進 base；以 key / progId / 整檔粒度擇一、不合併（見 [ADR-016](../../docs/adr/adr-016-multitenant-customization-overlay.zh-TW.md)）。

## 主要公開 API

| 型別 | 角色 |
|------|------|
| `FormSchema` | 定義中樞——描述表單的資料表、欄位與中繼資料 |
| `TableSchema` / `DbField` | 資料庫投影——欄位型別、索引、約束條件 |
| `FormLayout` / `LayoutSection` / `LayoutField` | UI 投影——欄位排列與分組 |
| `FilterCondition` / `FilterGroup` | 可組合的查詢篩選樹 |
| `SortField` / `SortFieldCollection` | 查詢排序描述 |
| `SystemSettings` / `DatabaseSettings` / `ProgramSettings` | 組態定義型別 |
| `IDatabaseSettingsProvider` | DI 服務，提供當前 `DatabaseSettings` 快照與查找輔助 |
| `SessionInfo` / `SessionUser` | Session 與使用者上下文 |
| `IDefineAccess` / `IDefineStorage` | 定義載入/儲存合約 |
| `ICustomizeDefineReader` | 租戶客製化覆蓋讀取器（Language / FormLayout / ProgramSettings / MenuSettings / PluginSettings） |
| `CustomizeOnlyStorage` / `CustomizeOnlyPathOptions` | 客製化層的嚴格只讀儲存（`{CustomizePath}/{customizeId}/...`，無檔回 null） |
| `IBusinessObjectFactory` | 商業物件建立的工廠合約 |
| `DefineTypeExtensions.ToClrType()` | DefineType 至 CLR 型別的解析擴充方法 |
| `DefineType` | 列舉所有定義種類（FormSchema、TableSchema、Settings 等） |

## 設計慣例

- **只用 XML 標註** — 可序列化屬性帶 `[XmlElement]` / `[XmlAttribute]`，成員若不該上 JSON wire 則加 `[JsonIgnore]`。兩者都是 BCL 詞彙。**不要加 MessagePack 標註**：那會把傳輸套件放進每一個消費者的相依表面，正是 POLHEM9001 要擋的事。
- **以 XML 註冊表選擇可替換服務** — `BackendComponents`（位於 `SystemSettings.xml`）可為每個可替換介面（`IDefineAccess`、`ISessionInfoService` 等）指定具體型別名稱。`AddPolhemFramework`（位於 `Polhem.Hosting`）在啟動時讀取，將設定的型別註冊到 DI 容器；留空代表使用框架預設。
- **FilterCondition 的工廠方法** — 偏好使用 `FilterCondition.Equal(...)` 而非 `new FilterCondition { ... }`，以提升可讀性與一致性。
- **DefineType 列舉作為分派鍵** — `DefineTypeExtensions.ToClrType()` 將列舉值對應至 CLR 型別，實現泛型定義 CRUD，無需硬編碼型別參考。
- **XML 文件註解使用英文** — 公開 API 以英文撰寫文件，供 IntelliSense 顯示。缺少 XML 註解的公開成員會使建置失敗（`GenerateDocumentationFile` 加 `TreatWarningsAsErrors` 下的 `CS1591`）。
- **啟用 Nullable Reference Types** — 專案啟用 NRT（`<Nullable>enable</Nullable>`）並將警告視為錯誤，在編譯時期強制 null 安全性。

## 目錄結構

| 資料夾 | 主要型別 |
|--------|----------|
| `Attributes/` | `ApiAccessControlAttribute` |
| `Collections/` | `ListItem`、`Parameter`、`Property` 及其集合 |
| `Customization/` | `CustomizeOverlay`（租戶客製化疊層） |
| `Database/` | `TableSchema`、`DbField`、`DbTableIndex`、`DatabaseType`、`FieldType`、`DbCategoryIds` |
| `Defaults/` | 隨框架出貨的定義檔（內嵌資源，見 `Defaults`） |
| `Filters/` | `FilterCondition`、`FilterGroup`、`FilterNode`、`ComparisonOperator`、`LogicalOperator` |
| `Forms/` | `FormSchema`、`FormTable`、`FormField`、`FormRule` |
| `Identity/` | `SessionInfo`、`SessionUser`、`UserInfo`、`CompanyInfo`、`EmployeeContext`、`ISessionInfoService` |
| `Language/` | `ILanguageService`、`LanguageResource`、`FormSchemaLocalizer`、`MenuLocalizer`、`LanguageResourceStringLocalizer` |
| `Layouts/` | `FormLayout`、`LayoutSection`、`LayoutField`、`LayoutGrid`、`LayoutColumn`、`ControlType` |
| `Logging/` | `IAuditLogWriter`、`AuditEntry` 及其子型別、`AuditRule`、`LogOptions` |
| `Organization/` | `DepartmentTree`、`IDepartmentTreeService` |
| `Paging/` | `PagingInfo`、`PagingOptions` |
| `Security/` | `IAccessTokenValidator`、`IApiEncryptionKeyProvider`、`MasterKeyProvider`、`ApiAccessRequirement`、`ApiProtectionLevel`、API 金鑰相關型別 |
| `Settings/` | `SystemSettings`、`DatabaseSettings`、`DbCategorySettings`、`ProgramSettings`、`MenuSettings`、`PluginSettings` 等 `*Settings` |
| `Sorting/` | `SortField`、`SortFieldCollection`、`SortDirection` |
| `Storage/` | `IDefineAccess`、`IDefineStorage`、`FileDefineStorage`、`ICustomizeDefineReader`、`CustomizeOnlyStorage` |
| 專案根目錄 | 跨切面基礎設施：`DefineType`、`DefineTypeExtensions`、`PathOptions`、`CustomizeOnlyPathOptions`、`IDatabaseSettingsProvider`、`IBusinessObjectFactory`、`ICacheDataSourceProvider`、`SysFields`、`SysProgIds`、`SystemActions` |

命名空間佈局遵循 [ADR-008](../../docs/adr/adr-008-polhem-db-namespace-layout.zh-TW.md) 的設計原則：
語法／模型／工廠分離；具體內容依領域歸類（`Database`、`Filters`、`Forms`、`Layouts` 等）；根層只保留跨切面基礎設施（系統常數、全域 service locator 介面、框架級 enum）。
