<!-- source: en/guides/migrating-from-bee-net.md blob: 59f68633948694420e049340066eb0d57e1fad4f -->
# 從 Bee.NET 遷移

[English](../../en/guides/migrating-from-bee-net.md) · [← 文件索引](../README.md)

Polhem 以新名稱延續 [Bee.NET](https://github.com/jeff377/bee-library) 框架（`Bee.*` 套件，最後發佈的版本是 4.33.0）。Polhem 1.0.0 是
Bee.NET 4.33.0 改名之後，再加上 [CHANGELOG](../../../CHANGELOG.zh-TW.md) 所列、首發前審查帶來的變更。所有帶 `Bee` 的名稱都已
改名，舊名稱不再被辨識：沒有相容層。本文說明升級時要改的地方。

## 套件、命名空間與型別

- 每個 `Bee.<名稱>` 套件改為 `Polhem.<名稱>`，套件的切分不變：`Bee.Hosting` 改為 `Polhem.Hosting`，[README 的組件表格](../../../README.zh-TW.md#-組件說明)中的
  每個套件依此類推。命名空間照同一規則：`Bee.Definition.Forms` 改為 `Polhem.Definition.Forms`。例外是
  `Bee.Api.AspNetCore`：它的後繼 `Polhem.Api.AspNetCore` 已在 1.2.0 移除，host 改用 `Polhem.JsonRpc.AspNetCore`
  提供 API（見 [CHANGELOG](../../../CHANGELOG.zh-TW.md)）。
- 唯一的例外是 `Bee.Base`，改為 `Polhem.Core`，套件與命名空間皆然：`Bee.Base.Serialization` 改為
  `Polhem.Core.Serialization`。Polhem 1.0.0 仍稱它為 `Polhem.Base`；改名的原因見 [CHANGELOG](../../../CHANGELOG.zh-TW.md)
  的 1.1.0 條目。
- 型別或成員名稱中的 `Bee` 改為 `Polhem`：`AddBeeFramework` 改為 `AddPolhemFramework`、`BeeLoginPanel` 改為
  `PolhemLoginPanel`。`UseBeeFramework` 自 1.2.0 起沒有對應成員，改呼叫 `services.AddPolhemApiKeyGateCheck()`。
- 命令列工具 `Bee.Cli`（`dotnet bee`）改為 `Polhem.Cli`（`dotnet polhem`）。先移除舊工具，再以
  `dotnet tool install -g Polhem.Cli` 安裝新工具。

審查也改名、搬移或移除了一些公開型別。以下是 Bee.NET 應用程式最可能用到的：

| Bee.NET | Polhem |
|---------|--------|
| `IBeeContext`、`BeeContext` | `IBusinessObjectContext`、`BusinessObjectContext` |
| `BeeStringLocalizer<T>` | `LanguageResourceStringLocalizer<T>` |
| `LogBusinessObject`、`LogListResult`、`LogAggregateResult`、`LogApiConnector`、`LogActions`、`LogListResponse`、`LogAggregateResponse` | `AuditLogBusinessObject`、`AuditLogListResult`、`AuditLogAggregateResult`、`AuditLogApiConnector`、`AuditLogActions`、`AuditLogListResponse`、`AuditLogAggregateResponse` |
| `PermissionAction` | `PermissionActions` |
| `NullAuditLogWriter` | `NullLogWriter` |
| `UserID`（`SessionUser`、`CreateSessionArgs`） | `UserId` |
| `AuditEntry.AccessToken` | `AuditEntry.TokenFingerprint` |
| `ApiClientInfo.ApiEncryptionKey`、`ApiClientInfo.UserTimeZoneId` | `ApiSessionContext` 上的同名成員 |
| `ApiClientInfo.LocalServiceProvider` | 把 `IServiceProvider` 傳給 `LocalApiProvider` 或本機 connector 的建構子 |
| `Bee.UI.Avalonia.Storage.FileEndpointStorage` | `Polhem.UI.Core.FileEndpointStorage` |
| `Bee.UI.Core.Permissions` 中的 `ElementCapabilityResolver`、`IElementCapabilityResolver`、`FieldCapability` | `Polhem.Api.Client.Permissions` 中的同名型別 |
| `Bee.ObjectCaching.Services` 中的 `DeploymentAuthorizationService`、`EmployeeContextResolver` | `Polhem.Business.Security.DeploymentAuthorizationService`、`Polhem.Business.Session.EmployeeContextResolver` |
| `JsonRpcExecutor.Execute`、`ApiServiceController` | 1.2.0 起移除：改由 `Polhem.JsonRpc.Server` 的 dispatcher 處理，以 `app.MapJsonRpc("/api")` 發布 |
| `BusinessObject.SessionInfo` | 在業務物件內使用 `SessionInfoService.Get(AccessToken)` |
| `Bee.Base.Tracing` | 已移除 |

不是擴充點的類別改為 sealed，框架 repository 的實作改為 internal（請用 `I*Repository` 介面），用戶端公開的非同步成員
多一個結尾的 `CancellationToken`。所有變更都列在 CHANGELOG。

程式碼中仍使用這些名稱的地方，編譯器都會報出來。

## 編譯器不會檢查的名稱

以下都是字串。沿用舊名稱時建置照樣成功，問題要到執行時才出現，或根本不會出現。

| 項目 | Bee.NET | Polhem | 沿用舊名稱時 |
|------|---------|--------|--------------|
| 定義檔中的型別名稱：`ProgramSettings.xml` 的 `BusinessObject` 與 `Repository`、`SystemSettings.xml` 中 `BackendConfiguration/Components` 底下的各元素，以及自家程式碼中的型別名稱 | `Bee.Business.AuditLog.LogBusinessObject, Bee.Business` | `Polhem.Business.AuditLog.AuditLogBusinessObject, Polhem.Business` | 執行時找不到型別 |
| 主金鑰的預設環境變數 | `BEE_MASTER_KEY` | `POLHEM_MASTER_KEY` | 只影響沿用預設變數名稱的 `SystemSettings.xml`：啟動時找不到主金鑰。`MasterKeySource` 的 `Value` 寫明變數名稱時，照樣使用該名稱；`dotnet bee defines materialize` 寫出的預設檔寫的是 `BEE_MASTER_KEY` |
| `.editorconfig`、`#pragma warning`、`NoWarn` 與 `[SuppressMessage]` 中的 analyzer 診斷代號 | `BEE1001` | `POLHEM1001`（數字不變） | 設定被靜默忽略 |
| 定義檔檢查用的 MSBuild 屬性 | `BeeDefinitionFilesGlob`、`BeeRequireDefinitionFiles`、`BeeAnalyzeDefinitionFiles` | `PolhemDefinitionFilesGlob`、`PolhemRequireDefinitionFiles`、`PolhemAnalyzeDefinitionFiles` | 設定被靜默忽略，改用預設值 |
| Blazor 元件的 CSS class | `bee-dynamic-form`、`bee-dynamic-grid`、`bee-form-page`、`bee-login-panel` | `polhem-dynamic-form`、`polhem-dynamic-grid`、`polhem-form-page`、`polhem-login-panel` | 自訂的樣式規則不再套用 |
| logging category，例如 `Logging:LogLevel` 底下的篩選 | `Bee.Api.AspNetCore` | `Polhem.Hosting.ApiKeys.ApiKeyGateWarningService` | 篩選不再符合 |
| `SerializationErrorData.FilePath` 這個 `Exception.Data` 的 key | `Bee.FilePath` | `Polhem.FilePath` | 讀取該 key 的程式碼找不到值 |

在自家 repo 的根目錄執行下列指令即可找出這些地方：

```bash
grep -rnE "Bee\.|Bee[A-Z]|BEE_|BEE[0-9]{4}|dotnet[- ]bee|bee-(dynamic|form|login)" --include="*.cs" --include="*.razor" --include="*.css" --include="*.xml" --include="*.json" --include="*.csproj" --include="*.props" --include="*.targets" --include=".editorconfig" --include="*.yml" --include="*.yaml" --include="*.sh" --include="*.js" --include="*.ts" --include="Dockerfile" .
```

## 設定

- **Components**：`SystemSettings.xml` 中 `BackendConfiguration/Components` 底下的項目可以留白，留白代表框架預設。
  只是重複 Bee.NET 預設值的項目，請清空，不要改名。
- **預設語言**：`CommonConfiguration/DefaultLanguage`（預設 `zh-TW`）是沒有自己 `st_user.culture` 的使用者所用的
  文化，也是語言回退的最後一站。它取代 `CommonConfiguration/DefaultLang` 與 `BackendConfiguration/DefaultLanguage`，
  後兩者不再讀取。

## 切換時會發生的事

- **已登入的使用者要重新登入。** `st_session` 現在存的是各存取權杖的雜湊，衍生 session 金鑰的標籤也已改名
  （`bee-api-*` 改為 `polhem-api-*`），所以 Bee.NET 建立的 session 在 Polhem 上都無法使用。Bee.NET 留在
  `st_session` 的資料列帶著它的權杖，而且再也對不上任何權杖；請刪除這些資料列。
- **舊格式的密碼需要重設。** `st_user.password` 不以 `v2.` 開頭的值是 PBKDF2-SHA1 雜湊，已無法通過驗證。以 `v2.` 開頭
  的雜湊照常可用，並在下次登入成功時以更多迭代次數重新雜湊。
- **記錄表不再保存存取權杖。** `st_log_access`、`st_log_anomaly_api`、`st_log_change`、`st_log_login` 改記錄
  `token_fingerprint`，不再記錄 `access_token`。結構升級會新增這個欄位，但從不刪除欄位，所以舊的 `access_token` 欄位
  連同 Bee.NET 寫入的權杖都還在。請自行清空或刪除該欄位。
- **用戶端與伺服端要一起升級。** payload 帶有型別名稱，例如 `Polhem.Definition.Collections.Parameter, Polhem.Definition`，
  而伺服端只接受允許的命名空間中的型別。Bee.NET 用戶端送出的是 `Bee.*` 名稱，Polhem 伺服端會拒絕，反之亦然。
- **匿名與未驗證的呼叫回應方式不同。** 不帶 `Authorization` 標頭的請求視為匿名呼叫，不再回 HTTP 401；需要 session
  的方法回應 JSON-RPC 錯誤 `-32001`。原本檢查 HTTP 401 的用戶端改為檢查錯誤碼。`CreateSession` 只接受本機呼叫；
  重放防護開啟時，`CreateApiKey`、`SetApiKeyEnabled`、`SetApiKeyExpiry` 需要 frame sequence。
- **未分頁的 `GetList` 只回傳一頁**，上限為 `PagingOptions.MaxPageSize`。
- **內建文字改為英文，並附 `zh-TW` 翻譯。** 標題、UI 文字與訊息依使用者的文化呈現，而登入回應現在會帶回這個文化：
  `UserInfo.Culture` 在登入前是空字串，不再是 `zh-TW`。`PolhemLoginPanel` 的各標籤與 `DynamicGrid.EmptyText` 改為
  `string?`，`null` 顯示在地化文字。
- **`CBool` 不再把中文詞視為 true。** 只有 `1`、`T`、`TRUE`、`Y`、`YES`（不分大小寫）為 true。
- **用戶端把端點存到新的位置。** 端點與 API 金鑰存成每位使用者本機應用程式資料資料夾下、以應用程式命名的子資料夾中的
  `endpoint.txt` 與 `apikey.txt`（`FileEndpointStorage`）。組件旁的 `{ExeName}.Settings.xml` 不再讀取，使用者要重新
  輸入端點與 API 金鑰。
- **Bee.NET 寫入的稽核紀錄保留舊標記。** `st_log_change` 的 `changes_xml` 欄以 `msprop:Bee.FieldDbType` 記錄每個欄位
  宣告的型別，Polhem 讀的是 `Polhem.FieldDbType`。舊紀錄讀出的值不變，只有 `GetDeclaredFieldDbType()` 對其欄位回傳
  `null`。
- **DefineEditor 以全新的設定啟動。** 它在使用者應用程式資料資料夾下的設定資料夾，由 `Bee.DefineEditor` 改為
  `Polhem.DefineEditor`。要保留最近開啟的檔案與偏好設定，請複製舊資料夾。
- **資料表名稱不變。** 框架資料表維持 `st_` 開頭的名稱。除了上述記錄表的欄位，沒有任何結構變更需要資料遷移。
