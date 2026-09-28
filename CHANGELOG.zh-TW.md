# 版本變更記錄

[English](CHANGELOG.md)

Polhem 套件的重要變更。格式依循 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)，版號依循
[語意化版本](https://semver.org/lang/zh-TW/)。每個版本在這裡以一行列一項變更；理由與背景寫在
[`docs/changelogs/`](docs/changelogs/) 下該版本的明細。

## [Unreleased]

## [1.0.0] - Unreleased

> Polhem 以新名稱延續 [Bee.NET](https://github.com/jeff377/bee-library)。Polhem 1.0.0 是 Bee.NET 最後發佈的 4.33.0
> 改名之後，再加上以下各項變更。應用程式如何遷移，見 [從 Bee.NET 遷移](README.zh-TW.md#從-beenet-遷移)。

📄 完整說明與背景：[docs/changelogs/1.0.0.zh-TW.md](docs/changelogs/1.0.0.zh-TW.md)

### 由 Bee.NET 改名

改名在 repo 開始使用 pull request 之前完成，因此這些項目沒有連結。

- 套件 ID 與命名空間由 `Bee.*` 改名為 `Polhem.*`，版號從 1.0.0 重新起算。套件的切分不變。
- 以 Bee 命名的型別與成員改名：`AddBeeFramework` → `AddPolhemFramework`、`UseBeeFramework` →
  `UsePolhemFramework`、`AddBeeBlazor` → `AddPolhemBlazor`、`BeeBlazorOptions` → `PolhemBlazorOptions`、
  `BeeBlazorProviderMode` → `PolhemBlazorProviderMode`、`BeeApiConnectorFactory` → `PolhemApiConnectorFactory`、
  `BeeAccessTokenProvider` → `PolhemAccessTokenProvider`、`BeeLoginPanel` → `PolhemLoginPanel`，以及擴充方法類別
  `BeeFrameworkServiceCollectionExtensions`、`BeeFrameworkApplicationBuilderExtensions`、
  `BeeBlazorServiceCollectionExtensions` → `Polhem…`。`IBeeContext` / `BeeContext` 與 `BeeStringLocalizer<T>`
  改用不含框架名稱的新名稱，見〈破壞性 API 變更〉。
- 命令列工具 `Bee.Cli` 改為 `Polhem.Cli`，以 `dotnet polhem` 呼叫。
- analyzer 診斷代號由 `BEE` 改為 `POLHEM`，數字不變，例如 `BEE1008` → `POLHEM1008`。`POLHEM4001`–`POLHEM4004`
  保留不用：它們是 Bee.NET 的 `BEE4001`–`BEE4004`，永不重複使用。([#10](https://github.com/polhem-dev/polhem/pull/10))
- 定義檔檢查用的 MSBuild 屬性改名：`BeeDefinitionFilesGlob`、`BeeRequireDefinitionFiles`、`BeeAnalyzeDefinitionFiles`
  → `PolhemDefinitionFilesGlob`、`PolhemRequireDefinitionFiles`、`PolhemAnalyzeDefinitionFiles`。
- 主金鑰的預設環境變數由 `BEE_MASTER_KEY` 改為 `POLHEM_MASTER_KEY`。
- payload 與預設設定中的型別名稱改為 `Polhem.*`。Bee.NET 用戶端與 Polhem 伺服端（或反過來）無法互通。
- 衍生 session 加密金鑰的標籤由 `bee-api-*` 改為 `polhem-api-session-key` 與 `polhem-api-encryption-root-key`。
- Blazor 元件的 CSS class 由 `bee-` 開頭改為 `polhem-` 開頭。
- 記錄欄位宣告型別的 `DataColumn` 擴充屬性由 `Bee.FieldDbType` 改為 `Polhem.FieldDbType`；
  `SerializationErrorData.FilePath` 由 `Bee.FilePath` 改為 `Polhem.FilePath`。
- 套件中繼資料：作者與著作權人改為 Polhem contributors，repository 改為 `polhem-dev/polhem`，並換上新的圖示。
  `Polhem.Cli` 帶有相同的中繼資料、README 與符號套件。([#3](https://github.com/polhem-dev/polhem/pull/3))

### 安全性

- 不再儲存存取權杖：`st_session` 以 SHA-256 衍生的鍵值（`AccessTokenHasher`）為索引，記錄表改存簡短的權杖指紋
  （`token_fingerprint`），取代 `access_token` 欄位。([#5](https://github.com/polhem-dev/polhem/pull/5))
- 密碼以 PBKDF2-SHA256、600,000 次迭代雜湊；較弱的既存雜湊在下次登入成功時換新，沿用自 Bee.NET 的 PBKDF2-SHA1
  格式雜湊不再能通過驗證。([#5](https://github.com/polhem-dev/polhem/pull/5))
- 不存在的帳號也會執行一次誘餌雜湊，登入耗時不會透露帳號是否存在；登入嘗試追蹤器有容量上限。
  ([#5](https://github.com/polhem-dev/polhem/pull/5))
- 記錄範圍（record scope）也檢查更新或刪除實際指向的既存資料列、修改與刪除的明細列，以及儲存後主檔列留下的值。
  ([#4](https://github.com/polhem-dev/polhem/pull/4))
- `GetList` 與 `GetCount` 只接受表單宣告過的過濾與排序欄位，且不接受受保護欄位；`GetLookup` 套用讀取的記錄範圍
  （`FormBusinessObject.LookupAppliesRecordScope` 可退出）。([#4](https://github.com/polhem-dev/polhem/pull/4))
- 遠端 `GetDefine` 只提供明列允許的定義類型。([#4](https://github.com/polhem-dev/polhem/pull/4))
- 業務物件以 `ProgramSettings` 宣告的 ProgId 大小寫建立，不論呼叫端怎麼寫 ProgId，稽核規則都對得上。
  ([#4](https://github.com/polhem-dev/polhem/pull/4))
- Encoded 與 Encrypted 請求依解析出的 action 的參數型別解碼；兩種 codec 對巢狀過濾條件都套用深度上限；action
  只解析到公開、非泛型、單一參數且不是屬性存取子的執行個體方法（`JsonRpcExecutor.IsResolvableAction`，
  `POLHEM3001` 以同一規則檢查）。([#6](https://github.com/polhem-dev/polhem/pull/6))
- `Plain` 本文先讀成 action 的請求型別，再複製到其引數，與 Encoded 相同，因此 Plain 呼叫無法設定契約未宣告的成員。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- BCL 例外的訊息不再傳給遠端呼叫端；每個錯誤碼有固定訊息，原始訊息經 `JsonRpcExecutor.Logger` 記錄。
  ([#6](https://github.com/polhem-dev/polhem/pull/6))
- `CreateSession` 只接受本機呼叫。`CreateApiKey`、`SetApiKeyEnabled`、`SetApiKeyExpiry` 受重放防護。
  ([#5](https://github.com/polhem-dev/polhem/pull/5))
- 資料庫異常記錄只有部署管理員能讀取。([#5](https://github.com/polhem-dev/polhem/pull/5))
- 用戶端拒絕伺服端宣告的 "none" 加密器（除非自身處於除錯模式），也不再沿用伺服端的除錯旗標與型別命名空間。
  ([#6](https://github.com/polhem-dev/polhem/pull/6))
- 主金鑰檔與用戶端的 `apikey.txt` 以僅擁有者可存取的權限寫入；資料庫設定含密碼卻未設 `ConfigEncryptionKey` 時記錄警告。
  ([#5](https://github.com/polhem-dev/polhem/pull/5))
- DDL 會跳脫 SQL Server 的字串預設值，非字串預設值必須是該型別的字面值；連線字串的佔位字以
  `DbConnectionStringBuilder` 解析（`ConnectionStringTemplate`）。([#4](https://github.com/polhem-dev/polhem/pull/4))

### 行為變更

- 不帶 `Authorization` 標頭的請求視為匿名呼叫：只由 `[ApiAccessControl]` 決定，需要 session 的方法回應 JSON-RPC
  `-32001`（Unauthorized），不再回 HTTP 401。用戶端把它轉成 `UnauthorizedAccessException`；`RemoteApiProvider` 在登入前不送 `Authorization` 標頭。
  ([#6](https://github.com/polhem-dev/polhem/pull/6), [#18](https://github.com/polhem-dev/polhem/pull/18))
- 未分頁的 `GetList` 回傳第一頁，上限為 `PagingOptions.MaxPageSize`。([#4](https://github.com/polhem-dev/polhem/pull/4))
- 所有內建定義檔與 UI 文字以英文為基底語言；中文移到隨附的 `zh-TW` 語系資源。標題、列舉、選單、規則訊息與框架文字
  共用同一條回退鏈（`LanguageFallback`）：要求的文化、其上層文化、`CommonConfiguration.DefaultLanguage`，最後是基底文字。
  ([#15](https://github.com/polhem-dev/polhem/pull/15))
- 登入回應帶有使用者的文化，用戶端會採用。`CommonConfiguration.DefaultLang` 與 `BackendConfiguration.DefaultLanguage`
  合併為 `CommonConfiguration.DefaultLanguage`（預設 `zh-TW`）。([#15](https://github.com/polhem-dev/polhem/pull/15))
- 給終端使用者的框架訊息帶有鍵值與參數（`UserMessageException`），依 session 的文化解析；表單規則訊息解析
  `{ProgId}.Rule.{RuleId}.Message`。([#15](https://github.com/polhem-dev/polhem/pull/15))
- 數字與日期依使用者的文化顯示與剖析；wire 維持與文化無關。([#15](https://github.com/polhem-dev/polhem/pull/15))
- `ValueUtilities.CBool` 只把 `1`、`T`、`TRUE`、`Y`、`YES`（不分大小寫）視為 true；中文的「是」「真」不再被辨識。
  ([#15](https://github.com/polhem-dev/polhem/pull/15))
- `BackendComponents` 各項預設為空白，代表框架預設。`CacheProvider` 或元件型別名稱錯誤時，在啟動時失敗並指出是哪個設定。
  ([#8](https://github.com/polhem-dev/polhem/pull/8), [#11](https://github.com/polhem-dev/polhem/pull/11))
- 無法解析且以 `Bee.` 開頭的型別名稱會附上遷移提示（`BeeNameHint`）；找不到 `POLHEM_MASTER_KEY` 而 `BEE_MASTER_KEY`
  有設定時，錯誤訊息會指出這一點。([#10](https://github.com/polhem-dev/polhem/pull/10))
- 用戶端把端點與 API 金鑰存成每位使用者本機應用程式資料資料夾下的 `endpoint.txt` 與 `apikey.txt`
  （`FileEndpointStorage`，現在位於 `Polhem.UI.Core`），不再寫在組件旁的設定檔。
  ([#12](https://github.com/polhem-dev/polhem/pull/12))
- 經 MessagePack 傳輸的 `DataTable` 改寫成一份欄位表加上依位置排列的資料列。JSON 與 Plain 不變。
  ([#12](https://github.com/polhem-dev/polhem/pull/12))
- 初始值不是 CLR 預設值的 wire 成員一律寫出，因此不論哪種 codec，缺少的成員都代表 CLR 預設值。Plain 請求依 JSON
  種類繫結 `object` 型別的過濾與參數值。([#9](https://github.com/polhem-dev/polhem/pull/9))
- 序列化快取中的定義不再改動它：空集合改由唯讀的 `XSpecified` 屬性省略，不再使用每個物件的序列化狀態。
  ([#8](https://github.com/polhem-dev/polhem/pull/8))
- 方法要求 `ApiReplayProtection.UniqueSequence` 而 `RequireWireFrame` 關閉時，啟動時記錄警告，並列出未宣告權限模型的表單。
  ([#4](https://github.com/polhem-dev/polhem/pull/4), [#6](https://github.com/polhem-dev/polhem/pull/6))
- `Short`、`Long`、`Decimal`、`Binary` 欄位的預設值具型別且不為 null。
  ([#14](https://github.com/polhem-dev/polhem/pull/14))
- 建立新列時以 `DefaultValueExpression` 為準：`GetNewData` 與用戶端新增的列會把運算式的值寫過字面值 `DefaultValue` 與依型別的初值。存檔時仍只填空的欄位。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- `ClientInfo.UseDefinitionLoader` 預設開啟，Avalonia 畫面不需設定即顯示在地化標題、租戶的版面與公司的數值格式。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- 以 `AddPolhemFramework` 建立的 host 缺少 `common` 資料庫項目時不會啟動（`IDatabaseSettingsProvider.ValidateRequired`）。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- 未知的 action 回應 JSON-RPC `-32601`，無法讀取的 Plain 本文回應 `-32602`，各有固定訊息。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))

### 破壞性 API 變更

- 不是擴充點的公開類別改為 sealed：資料與定義型別、attribute、例外、快取、沒有掛勾的服務實作、資料庫方言與建構器，
  以及末端的 UI 控制項與 Blazor 元件。業務物件、repository 與集合基底類別、`TextEdit`、`DateEdit`、`ListView`、
  `FormView`、各 connector 與 `AuditRuleBusinessObject` 仍可繼承。`KeyCollectionBase<T>` 改為 abstract。
  ([#13](https://github.com/polhem-dev/polhem/pull/13))
- 改名：`IBeeContext` / `BeeContext` → `IBusinessObjectContext` / `BusinessObjectContext`；
  `BeeStringLocalizer<T>` → `LanguageResourceStringLocalizer<T>`；稽核記錄這一軸的 `LogBusinessObject`、
  `LogListResult`、`LogAggregateResult`、`LogApiConnector`、`LogListResponse`、`LogAggregateResponse`、
  `ILogListResponse`、`ILogAggregateResponse`、`LogActions` → `AuditLog…`；`PermissionAction` →
  `PermissionActions`；`NullAuditLogWriter` → `NullLogWriter`；`UserID` → `UserId`（參數 `userId` / `funcId`
  亦同）；`AuditEntry.AccessToken` → `TokenFingerprint`。
  ([#5](https://github.com/polhem-dev/polhem/pull/5), [#10](https://github.com/polhem-dev/polhem/pull/10),
  [#11](https://github.com/polhem-dev/polhem/pull/11))
- 搬移：`DeploymentAuthorizationService` 移到 `Polhem.Business.Security`、`EmployeeContextResolver` 移到
  `Polhem.Business.Session`、`ElementCapabilityResolver`、`IElementCapabilityResolver`、`FieldCapability` 移到
  `Polhem.Api.Client.Permissions`、`FileEndpointStorage` 移到 `Polhem.UI.Core`。
  ([#11](https://github.com/polhem-dev/polhem/pull/11), [#12](https://github.com/polhem-dev/polhem/pull/12))
- `LocalApiProvider` 與本機 connector 的建構子接收 `IServiceProvider`；移除 `ApiClientInfo.LocalServiceProvider`、
  `ApiClientInfo.ApiEncryptionKey`、`ApiClientInfo.UserTimeZoneId`（改用 `ApiSessionContext`）。
  ([#10](https://github.com/polhem-dev/polhem/pull/10), [#11](https://github.com/polhem-dev/polhem/pull/11))
- 用戶端所有公開的非同步成員與 `JsonRpcExecutor.ExecuteAsync` 都多一個結尾的 `CancellationToken`；connector 的
  action 方法皆為 virtual。([#12](https://github.com/polhem-dev/polhem/pull/12))
- `IReplayWindowStore` 改為單一原子操作 `TryAcceptAsync`，因此可以實作多節點共用的儲存。
  ([#12](https://github.com/polhem-dev/polhem/pull/12))
- 自訂 payload codec 以 `ApiServiceOptions.RegisterPayloadCodec` 加入；未宣告時的預設仍是 MessagePack，不能替換。
  ([#10](https://github.com/polhem-dev/polhem/pull/10))
- 快取中依資料庫而來的型別（`CompanyInfo`、`DepartmentTree`、`ApiKeyInfo` 等）改為 init-only 屬性；session 的公司
  範圍改為單一不可變的 `SessionCompanyScope`。
  ([#8](https://github.com/polhem-dev/polhem/pull/8), [#10](https://github.com/polhem-dev/polhem/pull/10))
- `ICacheDataSourceProvider.GetCompanyAuditRules` 不再有預設實作。([#14](https://github.com/polhem-dev/polhem/pull/14))
- `PolhemLoginPanel` 的標籤參數與 `DynamicGrid.EmptyText` 改為 `string?`；`null` 顯示在地化文字。
  ([#15](https://github.com/polhem-dev/polhem/pull/15))
- wire：`CreateSessionRequest.userID` 改為 `userId`，並移除 `oneTime`；`LoginResponse` 新增 `culture`；稽核記錄的回應
  型別改名。`polhem-connector-js` 隨之更新。
  ([#10](https://github.com/polhem-dev/polhem/pull/10), [#11](https://github.com/polhem-dev/polhem/pull/11),
  [#15](https://github.com/polhem-dev/polhem/pull/15))
- `IFormRuleProcessor` 新增 `ApplyNewRowDefaults`；其他實作必須補上。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))

### 移除

- 追蹤子系統（`Polhem.Base.Tracing`、`SysInfo.TraceListener`）。([#10](https://github.com/polhem-dev/polhem/pull/10))
- 序列化狀態：`IObjectSerialize`、`IObjectSerializeEmpty`、`SerializeState`、`SerializationUtilities`。
  ([#8](https://github.com/polhem-dev/polhem/pull/8))
- 相容性殘留：被忽略的建構子參數與多載、`JsonCodec` 的 `includeTypeName`、`TableSchemaBuilder.Compare` 與舊的結構
  比對路徑（`DbUpgradeAction`）、Hosting 各 factory 的建構子退路、一次性 session 旗標、同步的
  `JsonRpcExecutor.Execute`、`BusinessObject.SessionInfo`、`ClientInfo.ClientSettings`。
  ([#10](https://github.com/polhem-dev/polhem/pull/10), [#12](https://github.com/polhem-dev/polhem/pull/12))
- 沒有呼叫端的型別與成員：`DateInterval`、`IPValidator`、`DataTableComparer`、`Dictionary<T>`、
  `DefaultBoTypeResolver`、`VersionInfo`、`SysInfo.IsToolMode`、`SysInfo.IsSingleFile` 與數個單純的包裝方法。
  ([#10](https://github.com/polhem-dev/polhem/pull/10))
- 改為 internal 的實作型別：Hosting 的背景服務、`NoEncryptionEncryptor`、`NoCompressionCompressor`、payload
  轉換器、`HttpUtilities`、`ILMapper<T>`、`XmlSerializerCache`、`ReplayWindow`、`BackendDefaultTypes`，以及框架
  repository 的實作（請用 `I*Repository` 介面）。
  ([#10](https://github.com/polhem-dev/polhem/pull/10), [#11](https://github.com/polhem-dev/polhem/pull/11),
  [#12](https://github.com/polhem-dev/polhem/pull/12))
- 未使用的設定型別 `ClientSettings`、`EndpointItem`、`EndpointItemCollection`。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))

### 新增

- `dotnet polhem keys protect`。([#10](https://github.com/polhem-dev/polhem/pull/10))
- connector 新增 `GetFormSchemaAsync`、`GetFormLayoutAsync`、`GetLanguageAsync`、`GetCommonConfigurationAsync`。
  ([#12](https://github.com/polhem-dev/polhem/pull/12))
- 在地化：`LanguageFallback`、`MenuLocalizer`、`FrameworkLanguageService`、`PolhemMessages`、`PolhemUIText`、
  `ILocalizableMessage`，以及 Avalonia 與 Blazor UI 文字的英文預設值與 `zh-TW` 翻譯。`FormView`、`ListView`、
  `LookupDialog` 預設透過 `ClientInfo.DefinitionLoader` 在地化定義。
  ([#15](https://github.com/polhem-dev/polhem/pull/15), [#22](https://github.com/polhem-dev/polhem/pull/22))
- `AuthenticationRequiredException`、可替換的服務 `IAuditLogSink`、`PagingOptions.MaxPageSize`、
  `DataRowExtensions.RewriteVersions`、`FormDataGuard.TryGetRowId`。
  ([#4](https://github.com/polhem-dev/polhem/pull/4), [#6](https://github.com/polhem-dev/polhem/pull/6),
  [#8](https://github.com/polhem-dev/polhem/pull/8), [#14](https://github.com/polhem-dev/polhem/pull/14))
- ADR-046 記錄 1.0 的 API 政策。([#12](https://github.com/polhem-dev/polhem/pull/12))

### 效能

- 定義快取命中時，每個項目每秒最多檢查一次來源檔。([#14](https://github.com/polhem-dev/polhem/pull/14))
- Gzip 預設以最快等級壓縮；`AesCbcHmacCryptor` 改用 span API，輸出格式不變。
  ([#14](https://github.com/polhem-dev/polhem/pull/14))
- 每個請求的反射結果會快取；Avalonia 各 head 共用一個運算式求值器。([#14](https://github.com/polhem-dev/polhem/pull/14))
- 稽核批次寫入器以單一交易寫入一批。([#14](https://github.com/polhem-dev/polhem/pull/14))
- 經 MessagePack 傳輸的 `DataTable` 更小、序列化更快（見〈行為變更〉）。
  ([#12](https://github.com/polhem-dev/polhem/pull/12))

### 平台支援

- Polhem 支援不修剪與部分修剪（partial trim）的建置。專案以 NativeAOT 發佈、以 `TrimMode=full` 修剪，或停用
  System.Text.Json 反射時，`POLHEM9004` 會發出警告（`PolhemSuppressTrimSupportWarning=true` 可關閉）。
  ([#17](https://github.com/polhem-dev/polhem/pull/17))
- `Polhem.Expressions` 隨附 ILLink descriptor，行動端預設的修剪會保留運算式用到的成員。
  ([#7](https://github.com/polhem-dev/polhem/pull/7))
- `LookupDialog` 與 `RowEditDialog` 只在桌面開原生視窗，其他平台改用覆蓋層（overlay host），因此 lookup 在 iOS、
  Android 與瀏覽器都能使用。([#7](https://github.com/polhem-dev/polhem/pull/7))
- MessagePack 值格式器不需動態程式碼即可處理 Polhem 列舉與巢狀的 `ParameterCollection`。
  ([#7](https://github.com/polhem-dev/polhem/pull/7))
- HTTP 用戶端在瀏覽器、Android 與 Apple 行動端 head 使用平台預設的處理器。
  ([#17](https://github.com/polhem-dev/polhem/pull/17))

### 修正

- 帶值過濾條件的 Plain `GetList` 在 SQL 參數處失敗；用戶端把 Plain 的 `DataTable` 結果讀成空表。
  ([#9](https://github.com/polhem-dev/polhem/pull/9))
- 快取中的定義正在序列化時，並行的儲存與刪除可能失敗；與失效通知競爭的快取填入可能保留過期值。
  ([#8](https://github.com/polhem-dev/polhem/pull/8))
- 從用戶端儲存語系資源一律失敗。([#17](https://github.com/polhem-dev/polhem/pull/17))
- 隨附的 AuditRule 表單在沒有定義載入器時，模式下拉選單是空的。([#7](https://github.com/polhem-dev/polhem/pull/7))
- Blazor：登入時沒有設定 circuit 的時區，`FormDataObject` 沒有回到 circuit 的內容繼續執行。
  ([#8](https://github.com/polhem-dev/polhem/pull/8))
- `SaveDatabaseSettings` 加密的是傳入執行個體本身的密碼，而不是副本。([#8](https://github.com/polhem-dev/polhem/pull/8))
- `KeyCollectionBase` payload 中的 nil 元素會被拒絕，不再略過。
  ([#9](https://github.com/polhem-dev/polhem/pull/9), [#14](https://github.com/polhem-dev/polhem/pull/14))
- 重建資料表（SQLite 上的任何欄位變更）時，未開啟 `UpgradeOptions.AllowColumnNarrowing` 也會縮小欄位。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))
- 表單資料表未宣告 `DbTableName` 時，關聯欄位的 JOIN 使用空白的資料表名稱；現在改用 `TableName`。
  ([#22](https://github.com/polhem-dev/polhem/pull/22))

### 範例與工具

- 範例的業務資料表放在公司範圍，登入後進入一家示範公司。([#23](https://github.com/polhem-dev/polhem/pull/23))
- Northwind 新增 zh-TW 示範帳號（`demo-tw`），README 的截圖改放在 repo 內。
  ([#23](https://github.com/polhem-dev/polhem/pull/23))
- DefineEditor 在 Windows 與 Linux 的視窗內顯示檔案選單。([#23](https://github.com/polhem-dev/polhem/pull/23))

[Unreleased]: https://github.com/polhem-dev/polhem/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/polhem-dev/polhem/releases/tag/v1.0.0
