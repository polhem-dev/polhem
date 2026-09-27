<!-- source: en/development-constraints.md blob: 7025445ab69bbc6be54c739c9004b9e79f5572f0 -->
# 開發限制與反模式

[English](../en/development-constraints.md) · [← 文件索引](README.md)

> 本文件列出框架的設計限制與禁止事項，供 AI Coding 工具參考，避免產生違反框架慣例的程式碼。
> 授權行為請參閱[權限與授權](permission-authorization.md)；帳號安全限制在本文件下方。

## 初始化順序限制

框架透過標準的 `IServiceCollection` DI 容器註冊；框架服務以 ctor 注入解析，不再使用靜態入口點。Host 啟動必須依以下步驟進行：

1. `var paths = new PathOptions { DefinePath = "..." }` — 指向定義檔目錄
2. `var settings = SystemSettingsLoader.Load(paths)` — 讀取 `SystemSettings.xml`（boot-time only；runtime 快取存取走 DI 注入的 `IDefineAccess`）
3. `SysInfo.Initialize(settings.CommonConfiguration)` — process-wide 的 debug 旗標與允許的型別命名空間（提供 API 的宿主另外執行 `ApiServiceOptions.Initialize(settings.CommonConfiguration.ApiPayloadOptions, settings.CommonConfiguration.IsDebugMode)`，設定 payload 的壓縮器與加密器；遠端用戶端則在 `SystemApiConnector.InitializeAsync` 中採用伺服端的設定）
4. `services.AddPolhemFramework(settings.BackendConfiguration, paths)` — 註冊框架服務（擴充方法來自 `Polhem.Hosting`）
5. 建立 service provider，接著：
   - **ASP.NET Core 宿主**在建好的應用程式上呼叫 `app.UsePolhemFramework()`。它不註冊任何 middleware，而是執行宿主端的啟動檢查（目前為：API 金鑰閘門是否生效，見 [API 金鑰管理](api-key-management.md)）。
   - 在行程內執行後端的**非 web 宿主**，把產出的 `IServiceProvider` 交給用戶端：`Polhem.Api.Client` 的 connector 以建構子參數接收它，原生 UI head 則指派給 `ClientInfo.LocalServiceProvider`（`Polhem.UI.Core`）。

完整參考見[端到端開發指引 § 框架初始化順序](development-cookbook.md#框架初始化順序)。

### 違反後果

- 在 `AddPolhemFramework` 之前解析框架服務 → DI 容器拋 `InvalidOperationException`（服務未註冊）
- `SystemSettingsLoader.Load` 指向不存在的 `SystemSettings.xml` → 拋 `FileNotFoundException`
- 以資料庫 id 建構 `DbAccess` → 建構子需要 `IDbConnectionManager`（傳 `null` 會拋 `ArgumentNullException`）；請改透過 DI 注入的 `IDbAccessFactory.Create(databaseId)` 取得實例。（另有一個 ctor 接已開啟的 `DbConnection` 加其 `DatabaseType`，不需連線管理員——供自行持有連線的呼叫端使用，例如在既有交易內寫入的程式碼）

### 參考範例

`tests/Polhem.Tests.Shared/TestProcessBootstrap.cs` 展示測試 process 的正確初始化順序。

## 快取資料初始化後不可異動

框架初始化完成後，**所有伺服端 cache 內的物件一律為唯讀，執行期間不可被
異動**。每個 session 從 process-wide 的 `ICacheContainer` 拿到的是同一份
in-memory 實例；對單一 session 做的調整會洩漏到其他所有 session，並行的
mutation 會競態。

這條規則的成立理由是「cache 為共用」，與資料從哪裡載入無關。因此下面兩類
都適用 —— 從定義檔載入的定義資料，以及從資料庫載入的快照。

### 適用範圍：定義檔快取

任何透過 `IDefineAccess.GetX(...)` 取得的物件（由同名 `ICacheContainer` slot
back-up）：

- `FormSchema`、`FormLayout`、`TableSchema`
- `SystemSettings`、`DatabaseSettings`、`ProgramSettings`、`DbCategorySettings`
- `MenuSettings`、`PluginSettings`、`PermissionModels`、`CurrencySettings`、
  `UnitSettings`
- `LanguageResource`

### 適用範圍：資料庫相依快取

這類經 `ICacheDataSourceProvider` 載入（而非 `IDefineAccess`），失效走共用的
cache-notify 表（而非 `SaveX` 呼叫）—— 但它們同樣存在於 process-wide 的
`ICacheContainer`、同樣被所有 session 共用，所以同一條禁令一體適用。取用管道
是 `ICacheContainer` slot 或包裝它的服務（`ICompanyInfoService`、
`IRolePermissionService`、`IDepartmentTreeService`、`IAuditRuleService`、
`IApiKeyValidator`）：

| 快取型別 | `ICacheContainer` slot | cache key |
|---------|------------------------|-----------|
| `CompanyInfo` | `CompanyInfo` | 公司 id |
| `CompanyRolePermissions` | `CompanyRolePermissions` | 公司 id |
| `DepartmentTree` | `DepartmentTree` | 公司 id |
| `CompanyAuditRules` | `CompanyAuditRules` | 公司 id |
| `ApiKeyInfo` | `ApiKey` | 金鑰 `sys_id` |
| `ApiKeyGateState` | `ApiKeyGate` | `ApiKeyGateState.CacheKey` |

其中 `CompanyAuditRules` 與 `CompanyRolePermissions` 由結構本身保證 —— 不公開
任何 setter，索引在建構子內一次建好。另外四個使用 init-only 屬性，建構後無法
重新指派值，但其中部分型別持有的集合（`CompanyInfo.NumberFormats`、
`DepartmentTree.Roots` 及其節點的子節點）是一般的可變集合。這條規則在它們身上
編譯器管不到；請把 cache 交給你的實例一律視為凍結。

### 適用範圍：例外

- `SessionInfo` 是刻意保留的例外 —— 它本來就是 per-session 實體、非共用資料，
  cache key 即 access token。
- `DatabaseSettings` 由框架自己就地修改：`IDefineAccess.GetDatabaseSettings()`
  在讀取時解密 cached 實例上 `enc:` 開頭的密碼。這之所以安全，只因為解密是冪等的，
  且每個欄位都以一次參考指派寫入（推理寫在 `CacheDefineAccess.GetDatabaseSettings`
  上）。這不是可以照抄的模式。`SaveDatabaseSettings` 加密的是副本，所以你傳入的
  實例不會變成密文。

### 禁止樣式

| 樣式 | 為何不可 |
|------|---------|
| `cachedSchema.DisplayName = "..."` | mutate 共用實例 → 跨 session 洩漏 / race |
| 把 per-session 狀態塞進 cached 物件的 `Tag` / 擴充屬性 | `Tag` 也是 process-shared |
| 在 cached 實例的集合中新增、移除或替換子節點（`schema.Tables`、`schema.MasterTable.Fields` 等） | 同樣 race 面 |

### 正確作法

- **需要 per-session 視圖（如本地化 schema）？** 先 clone、再 mutate 副本：
  ```csharp
  // `languageService` 為注入的 ILanguageService。
  var customised = cachedSchema.Clone();
  new FormSchemaLocalizer(languageService).Localize(customised, sessionLang);
  return customised;
  ```
- **定義資料的持久化變更**走 `IDefineAccess.SaveX(...)`：
  1. 寫入後端 storage
  2. invalidate cache slot，下一次 `GetX` 從 storage rebuild
- **資料庫相依資料的持久化變更**走所屬 repository 加上一筆 cache-notify 記錄，
  由 poller 在每個 process 失效該 slot。這類**沒有** `SaveX`；只寫了資料列卻
  漏掉 notify 記錄，會讓所有 process 繼續拿舊快照。
- **需要 deep copy？** 有 `Clone()` 的型別就用它（`FormSchema`、`FormTable`、
  `FormField`、`FormLayout`、`TableSchema`、`DatabaseSettings` 及其子項目）。
  其他定義型別（`SystemSettings`、`ProgramSettings`、`DbCategorySettings`、
  `MenuSettings`、`PluginSettings`、`PermissionModels`、`CurrencySettings`、
  `UnitSettings`、`LanguageResource`）沒有 `Clone()`；以 `XmlCodec` 序列化再
  反序列化，可得到 XML 所承載內容的獨立副本。序列化只讀取來源、不會改動它，
  框架透過 API 回應定義讀取時也是這麼做。
  **資料庫相依的那幾個型別沒有 `Clone()`** —— 它們是拿來讀的快照，不是拿來
  客製的。需要 per-session 變體時，把要用的值複製進自己的物件，不要為此補一個
  `Clone()` 再去 mutate。

### 為什麼這條重要

Polhem 設計用於多租戶 ASP.NET Core / Blazor Server host：單一 process 同時
服務眾多 session，每個 session 可能有不同語系與租戶 context。Cache 是
singleton，一個請求在讀、另一個請求在寫同一個 cached 實例時，沒有任何東西會
鎖住它。**「快取資料載入後不可異動」**這條 invariant 是讓所有 session 能安全
共用 cache 實例、無需協調的單一基礎規則。

資料庫相依快取只會把賭注放大、不會縮小：它們持有的是授權狀態。被 mutate 的
`CompanyRolePermissions` 或 `DepartmentTree` 不只是讓某個 session 看到錯的
標題 —— 它會在其他 session 上放行或擋掉存取。

## 跨層禁止事項

| 禁止行為 | 原因 | 正確做法 |
|----------|------|----------|
| API 層直接引用 Repository 層（指 `Polhem.Api.Core`、`Polhem.Api.AspNetCore`；**不含**組合根 `Polhem.Hosting`，接線各層本就是它的職責） | 違反分層架構 | 透過 Business Object 間接存取 |
| Business Object 直接建立 `DbConnection` | 繞過連線管理與日誌 | 把查詢放進 repository（見下一列）；由 repository 使用 `DbAccess` |
| BO 引用 `Polhem.Db`（`Polhem.Business.csproj` 無 `Polhem.Db` 的 `ProjectReference`） | BO 是業務邏輯的薄殼，資料存取屬於 Repository | FormSchema-driven CRUD → `IDataFormRepository`；自訂查詢 → 自訂 bo repo 配合 `IDbAccessFactory` |
| BO 寫死 `databaseId` 字串或直接讀 `SessionInfo.CompanyId` / `CompanyInfo` | 將 BO 與路由實作耦合；部署設定變更時會壞 | 使用 `BusinessObject.ResolveDatabaseId(DbScope)`（自訂 bo repo）或 `CreateDataFormRepository(progId)`（FormSchema CRUD）；helper 內部委派給 `IRepositoryDatabaseRouter`，這是單一真相來源 |
| Client 端從 DI 容器解析 Repository 服務 | 僅限 Server 端使用 | 透過 `ApiConnector` 呼叫 API |
| 跳過 Payload Pipeline 順序 | 破壞加解密一致性 | 維持 Serialize → Compress → Encrypt |
| 在 BO 中直接回傳 API 型別 | BO 不應依賴 API 序列化格式 | 回傳 BO 型別，由 `ApiOutputConverter` 依命名慣例自動對應 |

## ExecFunc 開發限制

### 方法簽章規則

ExecFunc handler 方法必須遵守以下規則：

- **必須** 是 `public` 方法（反射呼叫需要）
- **必須** 非泛型（`GetMethod()` 不支援泛型解析）
- **固定簽章**：`void MethodName(ExecFuncArgs args, ExecFuncResult result)`
- **FuncId 對應方法名稱**，大小寫敏感
- **必須**宣告 `[ExecFuncAccessControl]`。分派採 fail-closed：沒有標記的方法會以 `UnauthorizedAccessException` 拒絕，完全無法呼叫（`ExecFuncHandlerExtensions.InvokeExecFunc`）。analyzer 規則 POLHEM3003 會以建置警告回報遺漏，讓問題在用戶端第一次呼叫前就浮現。

### 存取控制宣告

```csharp
// 匿名存取
[ExecFuncAccessControl(ApiAccessRequirement.Anonymous)]
public void PublicMethod(ExecFuncArgs args, ExecFuncResult result) { }

// 需要登入（建構子的預設值；只寫 [ExecFuncAccessControl] 意思相同）
[ExecFuncAccessControl(ApiAccessRequirement.Authenticated)]
public void SecureMethod(ExecFuncArgs args, ExecFuncResult result) { }

// 遠端呼叫一律拒絕；只有行程內呼叫能執行
[ExecFuncAccessControl(ApiAccessRequirement.Authenticated, LocalOnly = true)]
public void MaintenanceMethod(ExecFuncArgs args, ExecFuncResult result) { }
```

## 例外處理規則

### Client 可見的例外類型

`JsonRpcExecutor` 透過 [`JsonRpcErrorContract`](../../src/Polhem.Api.Core/JsonRpc/JsonRpcErrorContract.cs) 把例外對映到 JSON-RPC 錯誤碼，伺服端與呼叫端都讀這一份宣告。設計理由見 [ADR-043](../adr/adr-043-error-contract-single-registry.zh-TW.md)。送到呼叫端的內容分為三類：

- **框架自有的例外會把訊息帶給呼叫端。** `UserMessageException`（凡是要給終端使用者看的訊息，**優先選用**）與 `JsonRpcException` 以 `JsonRpcErrorCode.UserMessage`（`-32099`）傳送；`AuthenticationRequiredException`、`CompanyNotEnteredException`、`CompanyAccessDeniedException`、`ForbiddenException` 與 `ReplayRejectedException` 各自使用專屬的錯誤碼。用戶端若假設「所有 user-facing 失敗都是 `-32099`」，會誤判這些例外。
- **BCL 例外保留錯誤碼、不保留訊息。** `UnauthorizedAccessException`、`ArgumentException`、`InvalidOperationException`、`NotSupportedException` 與 `FormatException`（含各自的子類別）以 `-32099` 傳送，但訊息換成固定的通用文字，例如「The request is not valid.」；真正的訊息記錄在伺服端（`JsonRpcExecutor.Logger`）。這些型別正是 BCL、資料庫驅動程式與基礎設施拋出時會在文字中夾帶表名、參數名與伺服器細節的例外，所以一律不給遠端呼叫者看。
- **其他所有例外**遮蔽為 `"Internal server error"`，錯誤碼為 `JsonRpcErrorCode.InternalError`（`-32000`），真正的訊息記錄在伺服端。

debug 模式開啟時（`SysInfo.IsDebugMode`，由 `CommonConfiguration.IsDebugMode` 設定），改為透傳原始訊息，取代固定或遮蔽的訊息；錯誤碼不變。兩種模式都不含堆疊追蹤。

### 使用時機

| 例外型別 | 使用情境 |
|----------|----------|
| `UserMessageException` | **優先選用**：任何要顯示給使用者看的訊息（業務規則違反、驗證失敗、流程中斷） |
| `ForbiddenException` | 呼叫者缺少權限（`-32004`）。由框架的權限閘門拋出。 |
| `ArgumentException`、`InvalidOperationException`、`NotSupportedException`、`FormatException` | 取其 BCL 本意：呼叫端傳錯參數、物件狀態不對、功能不適用、文字無法剖析。遠端呼叫者只會看到固定訊息，因此不要拿它們告訴使用者任何事。 |
| `UnauthorizedAccessException` | 伺服端拒絕存取。遠端呼叫者只會看到固定訊息「Access denied.」；需要呼叫者重新登入時請拋 `AuthenticationRequiredException`。 |
| `JsonRpcException` | API 框架自身的協定錯誤（HTTP status / JSON-RPC error code） |

### 可翻譯的訊息

`UserMessageException` 與框架其他 user-facing 例外都實作 `ILocalizableMessage`：除了 `Message` 中的英文文字，還可以帶語系鍵與訊息參數。

```csharp
throw new UserMessageException("MyApp.Order.CreditLimit", "Order {0} exceeds the credit limit.", orderNo);
```

鍵是完整的語系鍵 `{namespace}.{subKey}`，以第一個點切分；翻譯放在部署語系資源中的該命名空間下。回應離開伺服端之前，executor 會透過 `ILanguageService` 依 session 的文化、沿語系回退鏈查找這個鍵，再以參數格式化譯文。沒有任何文化翻譯這個鍵，或譯文中的佔位符是參數填不滿的，就送出英文文字。伺服端的 `Message` 維持英文，日誌照樣讀得到。框架自己的訊息使用 `PolhemMessages` 下的鍵；表單規則的訊息使用 `{ProgId}.Rule.{RuleId}.Message`。

### Client 端的對應行為

`ApiConnector.FinalizeResponse` 依 `JsonRpcError.Code` 重建例外：

- 有宣告例外型別的 code → 重建為該型別，帶原訊息、無前綴，呼叫端可依型別分支。`-32099` 一律重建為 `UserMessageException`，不論伺服端拋的是哪個型別；`-32001` 重建為 `AuthenticationRequiredException`，它衍生自 `UnauthorizedAccessException`。
- 其餘 code → 拋出 `InvalidOperationException($"API error: {code} - {message}")`，保留協定層除錯資訊

Client 端建議的 catch 順序：

```csharp
try
{
    var response = await connector.SaveAsync(dataSet);
}
catch (UserMessageException ex)
{
    // -32099：業務訊息，或被遮蔽的 BCL 失敗所帶的固定文字
    ShowMessage(ex.Message);
}
catch (ForbiddenException ex)
{
    // -32004：使用者缺少權限
    ShowMessage(ex.Message);
}
catch (AuthenticationRequiredException)
{
    // -32001：access token 缺少、無效或已過期
    ReturnToSignIn();
}
catch (InvalidOperationException ex)
{
    // 其他錯誤碼："API error: {code} - {message}"
    LogError(ex);
}
```

### 擴充方式

- 需要分類錯誤（例如用戶端要另外處理的「查無資料」）：繼承 `UserMessageException`，它沒有 sealed。子類別以 `-32099` 連同訊息傳送，用戶端會重建為 `UserMessageException`，所以在用戶端請依訊息鍵或文字分支，而不是依子類別。
- `JsonRpcError.Data` 目前不承載結構化資料：executor 不填它，ASP.NET Core 傳輸層只在宿主執行於 Development 環境時，把逃出 executor 的失敗的真正訊息放在這裡。

### 設計意圖

- 防止內部實作細節洩漏給 Client
- 為業務訊息建立獨立通道，與「真程式錯誤」在型別上明確區隔，方便 logging／監控分流

## FormSchema 設計限制

- cached 的 FormSchema 在執行時期為**唯讀**（見[快取資料初始化後不可異動](#快取資料初始化後不可異動)），不會動態新增欄位
- `IFormCommandBuilder`（位於 `Polhem.Db.Dml`）為 CRUD 命令建構契約，各 DB provider 各自實作（`SqlFormCommandBuilder` / `PgFormCommandBuilder` / `MySqlFormCommandBuilder` / `OracleFormCommandBuilder` / `SqliteFormCommandBuilder`），無共同基底類別
- `TableSchemaGenerator` 與 `FormLayoutGenerator` 由 FormSchema 產生 TableSchema 與 FormLayout，不會與既有檔案合併。在 TableSchema 手動調整的精度、索引或預設值，重新產生時就會遺失；第一次產生之後，請直接編輯 TableSchema
- `FormTable.DbTableName`：可選欄位；若為空，命令建構器使用 `FormTable.TableName` 作為實體表名。被其他表單以 `RelationProgId` 參照的表單應在主表設定它：開窗 JOIN 讀取關聯主表的 `DbTableName` 時沒有這個退回。命名應遵循 [`資料庫命名規範`](database-naming-conventions.md)（lowercase + snake_case）

## 型別安全限制

### wire 型別白名單

大多數 wire 成員有宣告型別，由註冊的 formatter 讀取（見下一節）。Encoded 與 Encrypted 請求本體會解碼成方法名稱所解析到的動作的參數型別；用戶端一併送來的型別名稱只用於一致性檢查。

`object` 型別成員中的值 —— 篩選值、`ParameterCollection` 項目、未定型欄的儲存格 —— 則在 wire 上自帶型別名稱，MessagePack 與 JSON codec 皆然。這個名稱在型別解析之前就先比對白名單，寫出端也套用同樣的檢查，所以讀取端會拒絕的值在寫出時就失敗，而不是在另一個行程裡失敗。白名單（`src/Polhem.Api.Core/MessagePack/WireTypeWhitelist.cs`）接受：

- 固定的 BCL 集合：`Boolean`、`Byte`、`SByte`、`Int16`、`UInt16`、`Int32`、`UInt32`、`Int64`、`UInt64`、`Single`、`Double`、`Decimal`、`String`、`DateTime`、`DateTimeOffset`、`TimeSpan`、`DateOnly`、`Guid`、`Byte[]`、`DBNull`、`System.Data.DataTable`、`Object` 與 `Object[]`（此處省略 `System.` 前綴）
- 元素型別為允許型別的一維陣列，例如 `int[]`、`string[]` 或 `Guid[]`；多維陣列一律拒絕
- `SysInfo.AllowedTypeNamespaces` 命名空間中的型別：框架自己的（`Polhem.Base`、`Polhem.Definition`、`Polhem.Api.Contracts`、`Polhem.Api.Core`、`Polhem.Business`），加上 `CommonConfiguration.AllowedTypeNamespaces` 中以 `|` 分隔的清單。泛型型別的泛型參數也會一併檢查
- 名稱中的組件部分必須是固定集合背後的 runtime 組件之一，或名稱等於某個允許命名空間、或以它開頭的組件。部署端自有型別所在的組件若命名在允許命名空間之外，也必須把該名稱加進去

列舉與 `ParameterCollection` 的值在所有平台都能用。允許命名空間中其他型別的值經 MessagePack 序列化需要動態程式碼，所以在沒有動態程式碼的平台（iOS、Mac Catalyst）會以指名該型別的 `NotSupportedException` 失敗。

### wire 型別必須註冊 formatter

凡走 MessagePack wire 的型別一律顯式註冊——contractless resolver 只是桌面端的便利退路，
不是承載機制：.NET for iOS 關閉動態碼，未註冊的型別在那裡直接失敗。新增訊息合約、
新增其可達的定義層型別、或引入新的封閉泛型具現（`List<T>`、`Dictionary<K,V>`、`T?`、列舉）
時都必須補上註冊。漂移測試會走同一條型別閉包，漏補時 `WireContractDriftTests` 失敗（是測試失敗，不是建置失敗）。
詳見 [ADR-037](../adr/adr-037-wire-explicit-registration.zh-TW.md)。

### API 契約命名慣例（強制）

API Request/Response 與 BO Args/Result 型別必須遵守命名慣例，`ApiOutputConverter` 才能自動將 BO 回傳值對應到 API 型別（詳見 [ADR-007](../adr/adr-007-convention-based-type-resolution.zh-TW.md)）：

| 層級 | 輸入 | 輸出 |
|------|------|------|
| BO（`Polhem.Business`） | `{Action}Args` | `{Action}Result` |
| API（`Polhem.Api.Core`） | `{Action}Request` | `{Action}Response` |
| Contract（`Polhem.Api.Contracts`） | `I{Action}Request` | `I{Action}Response` |

- 偏離命名慣例的型別將無法自動轉換，BO 回傳值會直接流至用戶端造成型別錯誤
- 回應映射**不需任何手動註冊**，由上表的命名慣例解析。當年需要 `Register` 的那個註冊表已移除，它所白名單的 Typeless 序列化也已移除 —— 見 [ADR-007](../adr/adr-007-convention-based-type-resolution.zh-TW.md) 與 [ADR-037](../adr/adr-037-wire-explicit-registration.zh-TW.md)

## 帳號安全限制

- `LoginAttemptTracker` 預設規則：15 分鐘內登入失敗 5 次，鎖定帳號 15 分鐘（`DefaultMaxFailedAttempts`、`DefaultLockoutMinutes`）
- 鎖定期間，`SystemBusinessObject.Login` 在檢查密碼之前就拒絕嘗試
- 成功登入會重置失敗計數器
- 計數依行程分開保存：多節點時，每個節點各自計數

## Session 持久化限制

登入時會寫入重建種子至 `st_session`，`SessionInfoCache` 在快取失效時據以重建。由此衍生三項限制：

- **種子不是快照。** 它只存無法再推導的值——使用者、到期、公司。資料列以 access token 的
  SHA-256 雜湊查找，token 本身不儲存。角色、客製代碼、record scope 一律於每次重建重算。**不要把可推導的狀態加進 `SessionUser`**：存進去的值就不再
  跟隨來源變動，登入後被撤銷的權限會殘留在該份副本裡。
- **重建需搭配「能取回 session 金鑰」的 provider。** `DerivedApiEncryptionKeyProvider`（預設）與
  `StaticApiEncryptionKeyProvider` 可以；`DynamicApiEncryptionKeyProvider` 不行，因為它的金鑰
  只存在於 session 內。使用 dynamic provider 時，session 一律不重建——快取失效即請使用者重新登入，
  勝過給出一個「看似有效但每個加密呼叫都失敗」的 session。
- **自訂登入流程必須走框架的建構路徑。** 只建構 `SessionInfo` 並呼叫 `SessionInfoService.Set`
  的程式碼，會產生一個背後沒有列的 session：行程一重啟就消失，在其他節點上根本不存在。

## API 重放防護限制

啟用 `ApiServiceOptions.RequireWireFrame`（預設關閉）後，Encoded 與 Encrypted 的請求會在 payload
內夾帶一段 wire frame（時間戳 + 序號）。伺服端會拒絕時間戳與伺服器時間相差超過
`ApiServiceOptions.WireFrameTimestampTolerance`（預設五分鐘）的 frame；對於 `[ApiAccessControl]`
宣告 `ReplayProtection = ApiReplayProtection.UniqueSequence` 的方法，也會拒絕該 session 已用過的
序號。兩種拒絕都是 `ReplayRejectedException`（`-32005`）。設計背景見
[ADR-042](../adr/adr-042-api-replay-protection.zh-TW.md)。由此衍生以下限制：

- **兩端必須設成同一個值。** frame 的有無是部署層級的事實，不由封包自述——伺服器若「偵測」
  frame 在不在，攻擊者只要把 frame 拿掉就能關閉防護。因此兩端設定不一致必然失敗，這是刻意的。
  啟用順序：**兩端先升套件，再同時開啟兩端開關**。
- **`UniqueSequence` 需要開關。** `RequireWireFrame` 關閉時什麼都不檢查；以 `AddPolhemFramework`
  建立的宿主會在這種狀態下記錄啟動警告，列出宣告了 `UniqueSequence` 的方法。
- **Plain 路徑不受保護。** 明文沒有攻擊者無法偽造的綁定，任何防重放欄位他都能改寫（改成當下
  時間、改成更大的序號），那就是一個全新的合法請求。`ApiProtectionLevel.Public` 的方法
  （含 `Save` / `Delete` / `ExecFunc`）仍允許以 Plain 呼叫，該路徑不帶 frame、不受檢查。
  `Encoded` 帶 frame 但無 HMAC，攔截到的 Encoded 呼叫可以換上新序號重新封裝。只有在 Encrypted
  payload 內，payload 的 HMAC 才涵蓋 frame，所以 `UniqueSequence` 只保護 Encrypted 呼叫。
- **預設的序號窗口依行程分開。** 接受或拒絕由 `ApiServiceOptions.ReplayWindowStore`（一個
  `IReplayWindowStore`）決定。預設的 `MemoryReplayWindowStore` 存在行程記憶體中：多個節點位於
  負載平衡器之後、且沒有依 token 黏著時，攔截到的請求可以在每個節點各重放一次。無法接受這點的
  部署，請以共用儲存體（快取或資料庫）實作 `IReplayWindowStore.TryAcceptAsync`，並把檢查與記錄
  做成單一的原子操作。
- **逾時重送會失敗，而非重試成功。** 序號解的是「拒絕重放」，冪等鍵解的是「安全重試」，
  兩者不可互相取代。框架本身沒有自動重試，但應用層自己包的重試迴圈、以及使用者手動
  「重新送出」都會踩到；需要安全重試的場景請自行實作冪等鍵。
- **匿名呼叫不做序號檢查。** 序號是 per session 的，登入前沒有 session 可計數。
  `ExecFuncAnonymous` 若有副作用，其冪等由應用層自負。

## 資料庫 Schema 限制

框架的 schema 定義（`TableSchema`）與升級機制（`TableUpgradeOrchestrator`）**刻意不支援**下列資料庫層元素：

- **Foreign Key 約束**
- **Trigger**
- **View**

### 設計原則

Referential integrity、business rules 與衍生資料由**程式端（Business Object 層）**處理，schema 定義僅描述資料表結構（欄位、索引、主鍵）。

### 設計理由

- 資料庫層相依會讓跨 provider 支援與 schema 升級成本爆炸
- 實務 ERP 場景下，BO 層已能完整表達業務規則，不需下推至 DB
- 升級流程（新增／刪除欄位、改型別）不必處理 FK 暫存／trigger 重建／view 刷新等級聯議題

### 若真的需要 FK / Trigger / View

不透過框架，改由專案自訂的 migration 腳本手動維護。升級管線不會產生對應 DDL，也不保證相容。
