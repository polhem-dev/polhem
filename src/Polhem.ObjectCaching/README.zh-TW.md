# Polhem.ObjectCaching

> 執行階段快取層，快取定義資料、資料庫相依資料與 Session 資訊，並提供建立在其上的服務。

[English](README.md)

## 架構定位

- **層級**：基礎設施層（快取）
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/architecture/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。它們確實漂了：`Polhem.Hosting` 抽出後，有四份 README 的下游數個月都沒把它補上。
- 由應用程式消費。

## 目標框架

- `net10.0` -- 存取現代執行階段 API 與效能改進

## 主要功能

### 定義快取（`Define/`）

- 每種定義一個快取：`SystemSettingsCache`、`DatabaseSettingsCache`、`DbCategorySettingsCache`、
  `ProgramSettingsCache`、`MenuSettingsCache`、`PluginSettingsCache`、`TableSchemaCache`、`FormSchemaCache`、
  `FormLayoutCache`、`LanguageResourceCache` 及其他 `*SettingsCache`
- `CacheDefineAccess` -- 透過這些快取讀取定義的 `IDefineAccess` 實作（可搭配客製化疊層），儲存時使對應快取失效

### 資料庫相依快取（`Database/`）

- `SessionInfoCache` -- 已驗證的 Session 資料，以存取權杖為鍵
- `CompanyInfoCache`、`CompanyRolePermissionsCache`、`DepartmentTreeCache`、`CompanyAuditRulesCache`、
  `ApiKeyCache`、`ApiKeyGateCache` -- 透過 `ICacheDataSourceProvider` 載入、透過共用的 cache-notify 資料表失效的資料

### 服務（`Services/`）

- `SessionInfoService`、`CompanyInfoService`、`CompanyAuthorizationService`、`RolePermissionService`、
  `DepartmentTreeService`、`AuditRuleService`、`ApiKeyValidator`、`ApiKeyGateStateProvider` -- 以快取實作的
  Definition 層服務介面

### 快取基礎設施

- `ObjectCache<T>` -- 單一物件快取基底類別，提供樣板方法掛勾（`GetPolicy`、`GetKey`、`CreateInstance`）
- `KeyObjectCache<T>` -- 以字串鍵識別物件的鍵值快取基底類別
- `ICacheProvider` / `MemoryCacheProvider` -- 可插拔的快取儲存提供者
- `CacheItemPolicy` / `CacheTimeKind` -- 過期設定。預設政策是在 `GetPolicy` 中設定的滑動過期，
  個別快取覆寫它即可調整
- `CacheInfo` -- 以靜態方式存取目前的 `ICacheProvider`，以及輪詢器發布的 cache-notify 版本（`ICacheNotifyVersionStore`）

### 多租戶客製化疊層

- `ICacheContainerProvider` / `CacheContainerProvider` -- 依 `CustomizeId` lazy 建立唯讀覆蓋層快取容器（`CachePrefix=customizeId`，以 `CustomizeOnlyStorage` 為後端），重用既有快取類別不需修改
- `CustomizeDefineReader` -- `ICustomizeDefineReader` 實作，從 per-租戶覆蓋層容器讀取 Language / FormLayout / ProgramSettings / MenuSettings / PluginSettings；無覆蓋檔時回 `null`（見 [ADR-016](../../maintainers/adr/adr-016-multitenant-customization-overlay.md)）
- `CustomizeDefineWriter` -- 對應的 `ICustomizeDefineWriter`

## 主要公開 API

| 類別 / 介面 | 用途 |
|-------------|------|
| `ICacheContainer` | 以 DI 注入的合約，公開每個快取實例（`SystemSettingsCache`、`FormSchemaCache`、`SessionInfoCache` 等） |
| `CacheContainerService` | `ICacheContainer` 的實作，由 `AddPolhemFramework` 以 Singleton 註冊 |
| `ObjectCache<T>` | 單一物件快取基底類別 |
| `KeyObjectCache<T>` | 鍵值快取基底類別 |
| `ICacheProvider` | 快取儲存提供者介面 |
| `CacheDefineAccess` | 透過快取讀取定義的 `IDefineAccess` 實作（可選客製化疊層） |
| `ICacheContainerProvider` / `CacheContainerProvider` | 依 `CustomizeId` 的覆蓋層快取容器提供者 |
| `CustomizeDefineReader` | 租戶客製化覆蓋讀取器（`ICustomizeDefineReader`） |
| `CacheItemPolicy` | 過期與淘汰設定 |
| `CacheInfo` | 以靜態方式存取快取提供者與 cache-notify 版本 |

## 設計慣例

- **DI 注入** -- 消費端以建構子注入 `ICacheContainer`；`CacheContainerService` 實作由 `AddPolhemFramework` 以 Singleton 註冊，呼叫端透過注入的合約取得各快取類別，而非靜態 facade。
- **樣板方法模式（Template Method Pattern）** -- `ObjectCache<T>` 子類別覆寫 `GetPolicy`、`GetKey`、`CreateInstance` 以定義快取行為，無需修改基底擷取邏輯。
- **鍵值正規化** -- `MemoryCacheProvider` 以 `ToLowerInvariant()` 將鍵轉為小寫，因此查找不分大小寫。
- **快取實例為共用** -- 每個 session 拿到的是同一個實例，因此載入後不可修改；需要 per-session 版本的呼叫端先複製。見[開發限制](../../docs/zh-TW/architecture/development-constraints.md)。
- **底層儲存** -- `MemoryCacheProvider` 包裝 `Microsoft.Extensions.Caching.Memory.IMemoryCache`；公開的 `CacheItemPolicy` 於內部對映為 `MemoryCacheEntryOptions`。
- 啟用 **Nullable Reference Types**（`<Nullable>enable</Nullable>`）。

## 目錄結構

- `Define/` -- 定義快取
- `Database/` -- Session 與資料庫相依快取
- `Services/` -- 以快取實作的服務
- `Providers/` -- `ICacheProvider`、`MemoryCacheProvider`
- 專案根目錄 -- `ICacheContainer`、`CacheContainerService`、`ObjectCache<T>`、`KeyObjectCache<T>`、`CacheItemPolicy`、
  `CacheTimeKind`、`CacheInfo`、`CacheDefineAccess`、`CacheContainerProvider`、`CustomizeDefineReader`、`CustomizeDefineWriter`
