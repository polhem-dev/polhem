<!-- source: en/getting-started/framework-capabilities.md blob: a2e129cf92eb482d3220139fbd985170c53e874b -->
# 框架機制清單

[English](../../en/getting-started/framework-capabilities.md) · [← 文件索引](../README.md)

> 單頁列出 Polhem 開箱即提供的機制，依領域分組。每一列一句話 —— 足以判斷「這件事框架是不是已經做掉了」，以及要往哪裡讀下去。
>
> 本頁回答的是「**提供了什麼**」，不解釋任何單一機制「**怎麼用**」；每一節都連向負責說明它的文件。

---

## 1. 定義層

`DefinePath` 下的定義檔驅動整個應用。見[定義檔全景](../definitions/definition-files-overview.md)與[架構總覽](../architecture/architecture-overview.md)。

| 機制 | 提供什麼 |
|------|---------|
| **FormSchema** | 定義中樞。一份 schema 同時驅動 UI、產生的 SQL 與驗證規則，一般 CRUD 不需要程式碼 |
| **TableSchema** | 實體資料表：欄位、型別、長度、可空性、索引 |
| **FormLayout** | 表單在畫面上如何排版，排列 FormSchema 宣告的欄位 |
| **`IDefineAccess`** | 伺服端讀寫每一種定義的統一介面。用戶端則以 `ClientDefineAccess` 經 API 讀取定義 |
| **ProgramSettings** | 型別註冊表，把每個 `progId` 對映到其商業物件與 Repository。僅供 server 端 |
| **MenuSettings** | 導覽選單：分組、排序、標題與可見性，每個項目指向一個 `progId` |
| **啟動設定** | 先讀 SystemSettings（`SystemSettingsLoader`），因為其中的金鑰設定用來解密 DatabaseSettings 裡的資料庫密碼；DbCategorySettings 登記每張資料表屬於哪個資料庫類別 |
| **FormRule** | 宣告式的存檔前 / 刪除前驗證，直接寫在 FormSchema 內 |
| **運算式引擎** | DynamicExpresso 求值計算欄與規則；`IExpressionEvaluator` 讓引擎可替換。見[運算式與規則](../definitions/expression-rules.md) |
| **主從結構** | 子表以 `sys_master_rowid` 串接，單次 `Save` 一併寫入 |
| **Lookup 關聯** | 欄位宣告關聯目標與欄位對映後，開窗查詢與帶值即自動成立 |
| **PluginSettings** | 每個 `progId` 掛哪些業務 plugin，各自宣告所掛的那一個時點，依宣告順序執行 |

## 2. 資料存取

見 [FormSchema 驅動的資料庫存取](../definitions/formschema-data-access.md)、[資料庫設定指引](../database/database-settings-guide.md)與[資料庫方言差異](../database/database-dialect-differences.md)。

| 機制 | 提供什麼 |
|------|---------|
| **DbAccess** | 資料存取核心：同步與非同步執行、批次、DataTable 更新 |
| **DbCommandSpec** | `{0}` 佔位符語句由框架負責參數化，呼叫端不拼接 SQL |
| **FormSchema 驅動 SQL** | 執行期依 FormSchema 產生 SQL —— 沒有 ORM，也沒有產生的 entity 類別 |
| **資料庫方言** | SQL Server、PostgreSQL、MySQL、Oracle、SQLite，各有自己的 DDL 與參數規則 |
| **分類路由** | `common` / `company` / `log` 三種 scope 決定資料表落在哪個實體資料庫 |
| **資料庫密碼加密** | DatabaseSettings 中的密碼以 `ConfigEncryptionKey` 加密儲存（`enc:`），該金鑰本身受主金鑰保護；連線字串的其餘部分維持明文 |
| **Schema 升級** | diff → plan → execute 管線，自動判定 ALTER 或重建，支援乾跑。見 [Schema 升級](../database/database-schema-upgrade.md) |
| **分頁 / 排序 / 篩選** | `PagingInfo`、`SortField`，以及支援 AND / OR 巢狀群組的 `FilterNode` 條件樹 |
| **數值捨入政策** | round-then-sum：計算出的數值欄位逐列依 `NumberKind` 捨入，因此由捨入後各列加總的合計會與各列對得上 |
| **連線範圍** | `DbConnectionScope` 管理一個工作單元內的連線與交易生命週期 |
| **異常偵測** | `DbAccess` 把執行失敗的命令，以及超過設定門檻（執行時間、讀取列數、異動列數）的命令寫入資料庫異常日誌 |
| **Repository 雙軌** | CRUD 由 FormSchema 驅動的 `DataFormRepository` 承接；報表與批次交給自寫 Repository |

## 3. 商業邏輯層

見[端到端開發指引](../guides/development-cookbook.md)與 [API ↔ BO 契約設計](../api/api-bo-contract-design.md)。

| 機制 | 提供什麼 |
|------|---------|
| **BO 軸** | System（框架層）、Form（每個 `progId` 一個實體）、AuditLog（稽核查詢） |
| **`FormBusinessObject`** | 預設 CRUD 表面：`GetList`、`GetData`、`GetNewData`、`Save`、`Delete`、`GetLookup` |
| **`IBusinessObjectFactory`** | 依 `progId` 解析商業物件，未註冊時退回框架預設 |
| **ExecFunc** | 依名稱呼叫 host 自訂方法的通用 dispatch，另有匿名版供註冊之類的流程 |
| **`FormBusinessPlugin`** | 存檔與刪除管線上的掛載點；一個類別掛一個時點，依宣告順序串成鏈 |
| **GlobalEvents** | 全程序事件；目前只有 `DatabaseSettingsChanged`，在資料庫設定重新載入時觸發，由連線管理員監聽 |

## 4. API 與傳輸

見 [JSON-RPC 前端整合指引](../api/jsonrpc-frontend-integration.md)與 [API 方法參考](../api/api-method-reference.md)。

| 機制 | 提供什麼 |
|------|---------|
| **JSON-RPC 2.0** | 單一 POST 端點，`method` 欄位為 `progId.action` |
| **PayloadFormat** | Plain、Encoded、Encrypted 三種 payload 模式，由呼叫端逐次選擇；方法的 `[ApiAccessControl]` 保護等級決定它接受的最低模式 |
| **Payload 管線** | 序列化 → 壓縮 → 加密（請求宣告的 codec，未宣告時為 MessagePack；Gzip；AES-CBC-HMAC），順序固定，回程反向 |
| **Connector** | `SystemApiConnector`、`FormApiConnector`、`AuditLogApiConnector` 是 client 端的呼叫入口 |
| **連線型態** | 同一份 client 程式可對 in-process 後端或遠端 HTTP 後端執行，呼叫端不需改動 |
| **三層契約** | 契約介面、wire DTO、BO Args / Result，三者皆可由 action 名推導 |
| **Wire 契約註冊** | wire 型別顯式註冊 MessagePack formatter，因此在沒有動態碼的 runtime（iOS AOT）上同樣可用 |
| **wire 邊界時區轉換** | 轉換發生在 payload 邊界，儲存端維持 UTC。見[時區處理](../database/datetime-timezone.md) |
| **JS 前端表面** | 非 .NET 前端以 Plain JSON 呼叫同一組方法。`GetFormSchema`、`GetFormLayout`、`GetLanguage` 對所有用戶端都以 XML 字串回傳儲存的定義 |

## 5. Session 與認證

| 機制 | 提供什麼 |
|------|---------|
| **Session 與 access token** | GUID token、具到期時間；`st_session` 以 token 的雜湊（`AccessTokenHasher`）為鍵保存 session 狀態，從不儲存 token 本身 |
| **Login** | 驗證憑證，回傳 access token 與該 session 的 API 加密金鑰 |
| **CreateSession** | 為指定使用者發行 token 而**不驗憑證**，供受信任的背景作業使用；僅限本機呼叫 |
| **登入失敗追蹤** | 預設的 `LoginAttemptTracker` 在多次登入失敗後暫時鎖定帳號；host 可註冊自己的 `ILoginAttemptTracker` |
| **API 金鑰** | 識別的是呼叫的應用程式而非使用者：只存雜湊、明文僅回傳一次、可停用或設到期。見 [API 金鑰管理](../security/api-key-management.md) |
| **部署層管理員** | 管理整個部署資產的管理員旗標，與任何公司權限各自授予、互不蘊含 |

## 6. 安全與加密

| 機制 | 提供什麼 |
|------|---------|
| **`[ApiAccessControl]`** | 逐方法宣告保護等級與認證需求，另有僅限本機的層級 |
| **主金鑰 provider** | 從檔案或環境變數讀取主金鑰（`MasterKeySource`），用來解密 `SystemSettings` 儲存的金鑰，例如 `ConfigEncryptionKey` |
| **AES-CBC-HMAC** | AES-256-CBC 搭配 HMAC-SHA256，每次加密使用新的隨機 IV，驗證採常數時間比較 |
| **密碼雜湊** | `PasswordHasher` 負責憑證的儲存與驗證 |
| **API 加密金鑰 provider** | 靜態、動態、衍生三種策略，決定 client 拿到哪一把 payload 金鑰 |
| **敏感欄位** | `SensitiveCategory` 讓 UI 隱藏或鎖定使用者無權限的欄位（僅 UX，伺服端不強制）；`ProtectedFields` 列出密碼雜湊等欄位，FormSchema 驅動的資料路徑拒絕讀取、寫入、篩選或排序它們 |
| **檔案完整性** | `FileHashValidator` 以雜湊驗證交付的檔案 |

## 7. 權限與授權

見[權限與授權指南](../security/permission-authorization.md)。

| 機制 | 提供什麼 |
|------|---------|
| **兩層模型** | 動作 gate 判定操作是否允許；record scope 判定看得到哪些列 |
| **PermissionModels** | 權限模型、其動作與 record scope 策略的 registry |
| **角色與授權三表** | `st_role`、`st_role_grant`、`st_user_role` 承載角色與授權 |
| **`FormField.ScopeRole`** | 標示範圍欄位；讀取自動過濾，寫入端在 server 以權威 re-query 確認 |
| **欄位能力解析** | `ElementCapabilityResolver` 把權限換算成每個欄位的可讀 / 可寫 / 隱藏狀態供 UI 套用 |

## 8. 多租戶與客製化

見[租戶客製化](../definitions/customization.md)。

| 機制 | 提供什麼 |
|------|---------|
| **公司範圍** | 每家公司各有自己的資料庫與設定；`EnterCompany` / `LeaveCompany` 切換 session 的範圍 |
| **客製覆寫層** | FormLayout、LanguageResource、ProgramSettings、MenuSettings、PluginSettings 可逐租戶覆寫，不必分岔套裝定義 |
| **部門樹** | 以 typed 樹狀物件提供的 per-company 組織階層 |
| **員工脈絡** | 目前使用者背後的員工與部門資料，於 `EnterCompany` 時解析並保存在 session 上，供 record scope 過濾使用 |

## 9. 在地化與格式

| 機制 | 提供什麼 |
|------|---------|
| **LanguageResource** | 每個（語言 × namespace）一檔，承載標題與訊息文字 |
| **`FormSchemaLocalizer`** | 在用戶端（`FormDefinitionLoader`）依 UI 語言在地化表單名稱與欄位標題；伺服端依儲存原樣提供定義 |
| **LanguageEnum** | 下拉清單背後的在地化列舉項目 |
| **`LanguageResourceStringLocalizer`** | 以語系資源為來源的 `IStringLocalizer`；Avalonia 與 Blazor 的內建 UI 文字都經由它取得，應用程式碼也可使用 |
| **`MenuLocalizer`** | 在地化選單標題 |
| **使用者訊息** | 給終端使用者的框架訊息帶有鍵（`UserMessageException`），依 session 的語系解析 |
| **時區** | 一律以 UTC 儲存、在邊界轉換，並有 per-user 時區設定。見[時區處理](../database/datetime-timezone.md) |
| **幣別與單位主檔** | `CurrencySettings` 與 `UnitSettings` 定義各幣別、各計量單位的小數位 |
| **數字格式解析** | 各數值類別的小數位，依幣別、單位或公司解析，於運算與顯示時套用 |
| **現金捨入** | 把單據應付金額捨入到幣別的現金捨入單位，公司可逐幣別覆寫 |

## 10. 快取

| 機制 | 提供什麼 |
|------|---------|
| **ObjectCache / KeyObjectCache** | 單一物件與帶 key 兩種快取，涵蓋定義資料與資料庫相依資料 |
| **`CacheDefineAccess`** | 定義檔的 process-wide 快取；取出的實例為共用 reference，不可在 runtime 異動 |
| **cache-notify** | 經 `st_cache_notify` 廣播失效，所有行程立即生效而不必等過期 |
| **Single flight** | 同一 key 的並發載入合併為一次，避免快取擊穿 |
| **`ICacheDataSourceProvider`** | 供「來源是資料庫查詢而非定義檔」的快取接上自載入邏輯 |

## 11. 稽核、診斷與工具

| 機制 | 提供什麼 |
|------|---------|
| **稽核流** | 登入事件、資料存取、資料變更（前後值 diffgram）、異常（API 與資料庫） |
| **AuditLog 商業物件** | 稽核流的查詢 API，同時提供明細列表與彙總 |
| **Polhem.Analyzers** | 隨套件發佈的建置期診斷，專案建置時即回報違反慣例之處。見 [Analyzer 規則](../reference/analyzer-rules.md) |
| **UI 控件家族** | Avalonia（schema 驅動的原生控件子類、Grid、lookup 開窗）與 Blazor Server 雙軌 |
| **`ClientDefineAccess`** | client 經 API 讀取定義並快取，完全不碰檔案系統 |
| **Client 儲存接縫** | `IEndpointStorage` 與 `IApiKeyStorage` 讓各 head 以自己平台允許的方式持久化端點與金鑰 |
