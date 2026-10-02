# 版本變更記錄

[English](CHANGELOG.md)

Polhem 套件的重要變更。格式依循 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)，版號依循
[語意化版本](https://semver.org/lang/zh-TW/)。每個版本在這裡以一行列一項變更；理由與背景寫在
[`docs/zh-TW/changelogs/`](docs/zh-TW/changelogs/) 下該版本的明細。

## [Unreleased]

> JSON-RPC 改建在 [`Polhem.JsonRpc`](https://github.com/polhem-dev/polhem-jsonrpc) 套件上，並移除
> `Polhem.Api.AspNetCore`。這項移除會讓參考它的 host 編譯失敗，卻在次版號發佈：繼 1.1.0 之後，1.x 內的第二次例外。
> 線路上的參數與結果外殼沒有改變；內部錯誤碼與回應的 `method` 成員改為符合 JSON-RPC 2.0，因此非 .NET 的用戶端
> 要與伺服器一起升級。理由，以及本版視為框架內部管線的型別，見
> [ADR-049](maintainers/adr/adr-049-jsonrpc-packages-in-1-2.md)（英文）。

### 破壞性 API 變更

- 移除 `Polhem.Api.AspNetCore` 套件，連同 `ApiServiceController` 與 `UsePolhemFramework()`。host 改用
  `Polhem.JsonRpc.AspNetCore` 提供 API。
- 移除 `IJsonRpcProvider`。`RemoteApiProvider` 與 `LocalApiProvider` 改實作套件的 `IJsonRpcTransport`，
  `ApiConnector.Provider` 也改為該型別。
- 移除 `Polhem.Api.Core.JsonRpc` 的 `JsonRpcExecutor`，以及訊息型別 `JsonRpcRequest`、`JsonRpcResponse`、`JsonRpcError`。

以 HTTP 提供 API 的 host 升級方式：

```diff
- <PackageReference Include="Polhem.Api.AspNetCore" Version="1.1.0" />
+ <PackageReference Include="Polhem.JsonRpc.AspNetCore" Version="…" />
```

```diff
  builder.Services.AddPolhemFramework(configuration, paths);
- builder.Services.AddControllers();
+ builder.Services.AddJsonRpcServer();
+ builder.Services.AddPolhemApiKeyGateCheck();
  var app = builder.Build();
- app.UsePolhemFramework();
- app.MapControllers();
+ app.MapJsonRpc("/api");
```

刪除衍生自 `ApiServiceController` 的 controller。覆寫過其成員的檢查改寫成 filter，以
`AddJsonRpcServer(options => options.Filters.Add(...))` 加入。

### 新增

- `Polhem.Hosting` 的 `AddPolhemApiKeyGateCheck()`：尚未發行 API key 時於啟動時記錄 log，供以 HTTP 提供 API 的 host 使用。

### 行為變更

- 內部錯誤改回錯誤碼 -32603（原為 -32000），`JsonRpcErrorCode.InternalError` 也改為此值。回應不再帶 `method` 成員。
  [polhem-connector-js](https://github.com/polhem-dev/polhem-connector-js) 用戶端需要對應 1.2.0 的版本。
- API key 或 `Authorization` header 被拒時，回 HTTP 200 與 JSON-RPC 錯誤，不再回 401；因此 .NET 用戶端丟出的是錯誤合約重建的
  例外，而不是 `HttpRequestException`。
- 方法名稱格式錯誤時回 `MethodNotFound`（-32601），不再回 `UserMessage`。找不到的方法以固定訊息回應，debug 模式也一樣，
  且不寫異常紀錄。
- in-process 呼叫與遠端呼叫一樣會序列化參數。
- log category：被遮蔽的失敗記在 `Polhem.Api.Core.Dispatch.PolhemExceptionMapper`，API key 啟動檢查記在
  `Polhem.Hosting.ApiKeys.ApiKeyGateWarningService`。

## [1.1.0] - 2026-09-30

> `Polhem.Base` 改名為 `Polhem.Core`。依語意化版本，這應等到 2.0.0；它以一次性例外在 1.1.0 發佈，因為 1.0.0 沒有
> 已知的使用者，而且所有 1.0.0 套件都已下架（unlist）。從 1.1.0 起，1.x 依語意化版本演進，不再有例外。理由見
> [ADR-048](maintainers/adr/adr-048-rename-base-to-core-in-1-1.md)（英文）。

📄 完整說明與背景：[docs/zh-TW/changelogs/1.1.0.md](docs/zh-TW/changelogs/1.1.0.md)

### 破壞性 API 變更

- `Polhem.Base` 改名為 `Polhem.Core`：套件 ID 與所有命名空間。([#42](https://github.com/polhem-dev/polhem/pull/42))

從 1.0.0 升級（套件參考只在直接參考 `Polhem.Base` 時才需要改）：

```diff
- <PackageReference Include="Polhem.Base" Version="1.0.0" />
+ <PackageReference Include="Polhem.Core" Version="1.1.0" />
- using Polhem.Base.Serialization;
+ using Polhem.Core.Serialization;
```

### 行為變更

- 內建的 JSON-RPC 型別命名空間白名單列的是 `Polhem.Core`，不再是 `Polhem.Base`。([#42](https://github.com/polhem-dev/polhem/pull/42))

### 範例與工具

- 移除 `Web.Js.Demo` 範例；瀏覽器端改用
  [polhem-connector-js](https://github.com/polhem-dev/polhem-connector-js)。([#41](https://github.com/polhem-dev/polhem/pull/41))

### 文件

- 使用者文件在 `docs/<lang>/` 下依主題分資料夾，維護者文件與 ADR 移到 `maintainers/`。([#38](https://github.com/polhem-dev/polhem/pull/38)、[#39](https://github.com/polhem-dev/polhem/pull/39)、[#40](https://github.com/polhem-dev/polhem/pull/40))

## [1.0.0] - 2026-09-28

> Polhem 以新名稱延續 [Bee.NET](https://github.com/jeff377/bee-library)。Polhem 1.0.0 是 Bee.NET 最後發佈的 4.33.0
> 改名之後，再加上以下各項變更。應用程式如何遷移，見 [從 Bee.NET 遷移](README.zh-TW.md#從-beenet-遷移)。

📄 完整說明與背景：[docs/zh-TW/changelogs/1.0.0.md](docs/zh-TW/changelogs/1.0.0.md)

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
- 呼叫端無法靠改變 ProgId 的大小寫避開稽核規則：業務物件以 `ProgramSettings` 宣告的大小寫建立，稽核規則以不分大小寫
  的方式查找，稽核紀錄寫入 FormSchema 的 ProgId 寫法。([#4](https://github.com/polhem-dev/polhem/pull/4),
  [#26](https://github.com/polhem-dev/polhem/pull/26))
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
- 找不到定義檔時，錯誤訊息只寫檔名，不含伺服端上的路徑（`FileUtilities.EnsureFileExists`）。
  ([#26](https://github.com/polhem-dev/polhem/pull/26))

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
- 用戶端把端點與 API 金鑰存成每位使用者本機應用程式資料資料夾下的 `endpoint.txt` 與 `apikey.txt`
  （`FileEndpointStorage`，現在位於 `Polhem.UI.Core`），不再寫在組件旁的設定檔。
  ([#12](https://github.com/polhem-dev/polhem/pull/12))
- 經 MessagePack 傳輸的 `DataTable` 改寫成一份欄位表加上依位置排列的資料列。JSON 與 Plain 不變。
  ([#12](https://github.com/polhem-dev/polhem/pull/12))
- 初始值不是 CLR 預設值的 wire 成員一律寫出，因此不論哪種 codec，缺少的成員都代表 CLR 預設值。Plain 請求依 JSON
  種類繫結 `object` 型別的過濾與參數值。([#9](https://github.com/polhem-dev/polhem/pull/9))
- 序列化快取中的定義不再改動它：空集合改由唯讀的 `XSpecified` 屬性省略，不再使用每個物件的序列化狀態。
  ([#8](https://github.com/polhem-dev/polhem/pull/8))
- 方法要求 `ApiReplayProtection.UniqueSequence` 而 `RequireWireFrame` 關閉時，啟動時記錄警告，並列出未宣告權限模型的表單，
  包括 `ProgramSettings` 沒有列出的已儲存表單（`IDefineStorage.GetFormSchemaIds`）。
  ([#4](https://github.com/polhem-dev/polhem/pull/4), [#6](https://github.com/polhem-dev/polhem/pull/6),
  [#26](https://github.com/polhem-dev/polhem/pull/26))
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
- 儲存時強制檢查 `FormField.Required`：新增或修改的資料列（主檔或明細）有必填欄位是空的，會以在地化訊息拒絕；Avalonia
  的 `FormView` 與 Blazor 的 `FormPage` 在送出儲存前列出所有這類欄位。請檢查各 FormSchema 的 `Required` 旗標：補上資料，
  或取消該旗標。([#26](https://github.com/polhem-dev/polhem/pull/26), [#27](https://github.com/polhem-dev/polhem/pull/27))
- `ProgramSettings` 沒有列出的表單，其稽核紀錄改寫入 FormSchema 的 ProgId 寫法，而不是呼叫端的寫法；之前寫入的紀錄維持
  原樣，因此比對較舊的 `prog_id` 時請不分大小寫。([#26](https://github.com/polhem-dev/polhem/pull/26))
- `BackendComponents` 指定的自訂 `DefineAccess` 或 `DefineStorage` 改以 `ActivatorUtilities` 建立，建構子可以接收任何已註冊
  的服務，不再限於固定幾種簽章。([#26](https://github.com/polhem-dev/polhem/pull/26))
- Blazor 的 `FormPage` 預設透過定義載入器在地化定義（`PolhemBlazorOptions.UseDefinitionLoader`）；設為 `false` 則照原樣
  呈現定義。([#27](https://github.com/polhem-dev/polhem/pull/27))
- 巢狀 `<Categories>` 版面的 `ProgramSettings.xml` 不再被拒絕，而是載入成空的註冊表。Bee.NET 4.33.0 會拒絕這種檔案，
  所以只有從未在該版執行過的部署受影響：請先以 Bee.NET 的 `dotnet bee defines split-menu` 轉換該檔。
  ([#28](https://github.com/polhem-dev/polhem/pull/28))
- 隨附的 `AuditRule`、`Department`、`Employee` 版面依其 schema 重新產生（下拉選單、核取方塊與 lookup，取代文字框）。
  materialize 會略過已存在的檔案，因此已有這些版面的部署會保留舊版，直到自行覆寫或修改。
  ([#29](https://github.com/polhem-dev/polhem/pull/29))
- 產生的版面與 `Auto` 控制項類型，為 `DateTime` 欄位改用新的 `DateTimeEdit`，不再是 `DateEdit`。已儲存的版面維持
  `DateEdit`，它只編輯日期、會丟掉時間；時間有意義的欄位請改為 `DateTimeEdit`。
  ([#33](https://github.com/polhem-dev/polhem/pull/33))

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
- 用戶端與 UI 套件所有公開的非同步成員，以及 `JsonRpcExecutor.ExecuteAsync`，都多一個結尾的 `CancellationToken`；
  connector 的 action 方法皆為 virtual。`IUIViewService` 的實作，以及覆寫 `FormView`、`ListView` 受保護的
  `Resolve*Async` 掛勾者，簽章隨之改變。
  ([#12](https://github.com/polhem-dev/polhem/pull/12), [#27](https://github.com/polhem-dev/polhem/pull/27))
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
- 可調整的上限與預設值由 `const` 改為 `static readonly`：`ApiKeyFormat.MinSysIdLength`、`ApiKeyFormat.MaxSysIdLength`、
  `LoginAttemptTracker.DefaultLockoutMinutes`、`DefaultMaxFailedAttempts`、`DefaultMaxTrackedAccounts`、
  `CurrencySettings.FallbackRounding`、`UnitSettings.FallbackDecimals`、`ApiKeyCache.AbsoluteMinutes`、`NegativeMinutes`、
  `RowEditPanel.CompactWidthThreshold`、`FormView.DefaultCompactWidthThreshold`。在常數運算式中使用它們的程式碼必須修改。
  ([#26](https://github.com/polhem-dev/polhem/pull/26))
- `SystemApiConnector` 與 `AuditLogApiConnector` 的 `ExecuteAsync<T>` 改為 protected；`FormApiConnector.ExecuteAsync<T>`
  維持公開，供呼叫表單自己的 action。([#26](https://github.com/polhem-dev/polhem/pull/26))

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
- `StringHashSet` 與 `SysInfo.Version` 的公開 setter。([#26](https://github.com/polhem-dev/polhem/pull/26))
- `ProgramSettingsFormat` 與 `dotnet polhem defines split-menu`，它們用來轉換較早 Bee.NET 版本的巢狀 `ProgramSettings`
  版面。([#28](https://github.com/polhem-dev/polhem/pull/28))

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
- 伺服端與 UI head 共用的必填欄位規則 `RequiredFieldCheck` 與 `MissingRequiredField`，以及訊息
  `PolhemMessages.SaveFieldRequired`、`SaveDetailFieldRequired`、`PolhemUIText.RequiredFieldsEmpty`。
  ([#26](https://github.com/polhem-dev/polhem/pull/26), [#27](https://github.com/polhem-dev/polhem/pull/27))
- `IDefineStorage.GetFormSchemaIds`、`FileUtilities.EnsureFileExists`。([#26](https://github.com/polhem-dev/polhem/pull/26))
- `PolhemBlazorOptions.UseDefinitionLoader`、`PolhemApiConnectorFactory.CreateDefinitionLoader`。
  ([#27](https://github.com/polhem-dev/polhem/pull/27))
- Avalonia 的 `FormDataObject.RowEditFieldChanged`。([#30](https://github.com/polhem-dev/polhem/pull/30))
- `ControlType.DateTimeEdit`，搭配 Avalonia 的 `DateTimeEdit` 編輯器與 Blazor 的 `datetime-local` 輸入框；
  `FormValueBinding.TryGetListItemText`、接收 `FormTable` 的 `GridControl.Bind` 多載，以及 `DynamicGrid.FormTable`。
  ([#33](https://github.com/polhem-dev/polhem/pull/33))

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
- 超過兩個變數的運算式不再讓 iOS 與 Mac Catalyst 上的 App 終止：`DynamicExpressoEvaluator` 把每個運算式編譯成單一個以
  物件陣列為參數的委派，不需要動態程式碼。([#30](https://github.com/polhem-dev/polhem/pull/30))
- macOS 與 Linux 上的桌面 head 可以用 Local 模式連線：`FileUtilities.IsLocalPath` 接受目前作業系統上的完整路徑，
  不再只認 Windows 磁碟機與 UNC 路徑。([#29](https://github.com/polhem-dev/polhem/pull/29))
- 手機上的 lookup 與明細列編輯覆蓋層符合螢幕大小，避開安全區域與螢幕鍵盤，按鈕也留在捲動內容之外。
  ([#33](https://github.com/polhem-dev/polhem/pull/33))

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
- macOS 與 iOS 回報的文化是 `zh-Hant-TW`，原本找不到 `zh-TW` 資源；`LanguageFallback` 在 `zh-Hant` 之後接 `zh-TW`，
  在 `zh-Hans` 之後接 `zh-CN`。([#26](https://github.com/polhem-dev/polhem/pull/26))
- 明細列屬於此次儲存未包含的記錄而被拒絕時，訊息沒有在地化（`PolhemMessages.PermissionDetailOutOfScope`）。
  ([#26](https://github.com/polhem-dev/polhem/pull/26))
- `FormView` 把權限能力套用在主機指定的 `Layout` 本身，而不是副本。([#27](https://github.com/polhem-dev/polhem/pull/27))
- Avalonia 手機寬度的卡片清單在日期上顯示時間部分，也不理會數值格式；現在與表格以相同方式格式化。
  ([#29](https://github.com/polhem-dev/polhem/pull/29))
- 照 Blazor Server 的 README 以 Remote 模式執行會得到 401；README 與 `UseRemoteProvider` 現在寫明必須設定
  `ApiClientInfo.ApiKey`。([#29](https://github.com/polhem-dev/polhem/pull/29))
- Avalonia 的明細列編輯覆蓋層要到確認該列才重算計算欄位；現在編輯時即重算，按取消則還原。
  ([#30](https://github.com/polhem-dev/polhem/pull/30))
- 清單（Avalonia 與 Blazor 的表格、卡片清單、lookup 與明細表格）顯示下拉欄位儲存的值；現在顯示在地化的清單項目文字，
  布林值在卡片清單顯示核取方塊，其他地方顯示在地化的是／否。([#33](https://github.com/polhem-dev/polhem/pull/33))
- Blazor 的 `DateEdit` 遇到帶時間的值時顯示空白；現在顯示日期。([#33](https://github.com/polhem-dev/polhem/pull/33))

### 範例與工具

- 範例的業務資料表放在公司範圍，登入後進入一家示範公司。([#23](https://github.com/polhem-dev/polhem/pull/23))
- Northwind 新增 zh-TW 示範帳號（`demo-tw`），README 的截圖改放在 repo 內。
  ([#23](https://github.com/polhem-dev/polhem/pull/23))
- DefineEditor 在 Windows 與 Linux 的視窗內顯示檔案選單。([#23](https://github.com/polhem-dev/polhem/pull/23))
- 範例的 `Employee` 與 `Department` 表單改名為 `Staff`（`ft_staff`、`ft_staff_phone`）與 `Team`（`ft_team`），不再取代
  框架同名的保留表單。([#25](https://github.com/polhem-dev/polhem/pull/25))
- Northwind 隨附訂單規則的 `zh-TW` 訊息。([#25](https://github.com/polhem-dev/polhem/pull/25))

[Unreleased]: https://github.com/polhem-dev/polhem/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/polhem-dev/polhem/releases/tag/v1.1.0
[1.0.0]: https://github.com/polhem-dev/polhem/releases/tag/v1.0.0
