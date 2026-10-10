# Polhem.Api.Client

> API 用戶端連接器，提供統一介面以進行本機（行程內）與遠端（網路）商業邏輯呼叫。

[English](README.md)

## 架構定位

- **層級**：前端 / 用戶端
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/architecture/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。它們確實漂了：`Polhem.Hosting` 抽出後，有四份 README 的下游數個月都沒把它補上。
- 由應用程式消費（Avalonia、Blazor、主控台及其他 head）。

## 目標框架

- `net10.0` -- 存取現代執行階段 API 與效能改進

## 主要功能

### 本機 / 遠端策略

- 連接器透過 [`Polhem.JsonRpc.Client`](https://github.com/polhem-dev/polhem-jsonrpc) 送出，並以 `Polhem.JsonRpc.Payload.Client` 的 `PayloadConnector` 封裝每次呼叫，經由兩種實作其 `IJsonRpcTransport` 的傳輸之一：行程內傳輸把呼叫交給後端的 dispatcher，HTTP 傳輸則以 POST 呼叫遠端端點。
- 策略在建立 `PolhemApiClient` 時決定一次：`PolhemApiClient.CreateLocal` 接受 `IServiceProvider`（以 `AddPolhemFramework` 建立的後端服務提供者）並走行程內，`PolhemApiClient.CreateRemote` 接受端點 URL 與 API 金鑰並走 HTTP。client 發出的每個連接器都依循這個選擇。

```csharp
var client = PolhemApiClient.CreateRemote("https://host/api", apiKey);
await client.System.LoginAsync(userId, password);
await client.System.EnterCompanyAsync(companyId);
var data = await client.Form("Employee").GetDataAsync(rowId);
```

### 連接器

- `SystemApiConnector` -- 系統操作：登入與登出、建立 session、進入公司、`InitializeAsync`（環境初始化）、
  定義讀寫（`GetDefineAsync` / `SaveDefineAsync` 以及 `GetFormSchemaAsync` 等具型別版本）、API 金鑰管理與
  `ExecFuncAsync`。完整清單以類別本身為準。
- `FormApiConnector` -- 綁定單一 `ProgId`：`GetListAsync`、`GetDataAsync`、`GetNewDataAsync`、`SaveAsync`、
  `DeleteAsync`、`GetLookupAsync`、`ExecFuncAsync` 與 `ExecFuncAnonymousAsync`。
- `AuditLogApiConnector` -- 查詢稽核與異常紀錄。
- 所有動作方法都是非同步、帶 `Async` 後綴，並以 `CancellationToken` 作為最後一個參數。
  所有連接器共用 `ApiConnector` 的 payload 管線（編碼、壓縮、加密）。

### 連線驗證

- `ApiConnectValidator.ValidateAsync` 依端點字串判斷 `ConnectType`（Local 或 Remote）、對照呼叫端傳入的 `SupportedConnectTypes`、驗證目標，並可選擇為本機連線產生缺少的設定檔。
- 遠端驗證會先執行 `Ping` 確認連線可用後才回傳。

### 快取定義存取

- `ClientDefineAccess` 透過 `SystemApiConnector` 讀寫定義（`GetFormSchemaAsync`、`GetFormLayoutAsync`、
  `GetSystemSettingsAsync`、`SaveFormSchemaAsync` 等），並快取讀過的內容。它是一組獨立的非同步 API，
  不實作伺服端的 `IDefineAccess`。
- `FormDefinitionLoader` 由這些定義組出 UI 需要的在地化 `FormSchema` 與執行期 `FormLayout`。

### 用戶端狀態

- `PolhemApiClient` 是組合根：一條連到後端的連線（`IsLocal`、`Endpoint`、`ApiKey`、`PayloadOptions`、
  `DefaultLanguage`）、透過它登入的身分（`Session`），以及呼叫它的連接器（`System`、`AuditLog`、
  `Form(progId)`）。成員以類別本身為準。
- `ApiSessionContext`（`PolhemApiClient.Session`）以一個不可變的 `ApiSessionCredentials`（access token、
  傳輸金鑰、使用者時區）持有登入狀態。`SystemApiConnector.LoginAsync` 整組替換，`LogoutAsync` 與
  `PolhemApiClient.SignOut` 將其清除。
- 一個 client 只持有一個身分。在同一行程服務多位使用者的 host（Blazor Server）為每位使用者建立一個 client。

### 各 head 共用的 UI 輔助

- `ElementCapabilityResolver` -- 依使用者權限判斷欄位可見或唯讀。
- `FormDataGuard` / `FormValueBinding` -- CRUD 前置條件，以及 UI 與 `DataRow` 之間的值轉換。

## 主要公開 API

| 類別 / 介面 | 用途 |
|-------------|------|
| `PolhemApiClient` | 進入點：一條連線、其登入身分與連接器 |
| `ApiSessionContext` / `ApiSessionCredentials` | client 的登入狀態（access token、傳輸金鑰、時區） |
| `ApiConnector` | 抽象基底連接器，含 payload 管線 |
| `SystemApiConnector` | 系統層級操作 |
| `FormApiConnector` | 綁定特定 ProgId 的表單層級商業物件呼叫 |
| `AuditLogApiConnector` | 稽核與異常紀錄查詢 |
| `ClientDefineAccess` | 透過 API 的非同步快取定義存取 |
| `FormDefinitionLoader` | 供 UI 使用的在地化表單結構與執行期版面 |
| `ApiConnectValidator` | 驗證端點並判斷連線類型 |
| `ConnectType` | 列舉：`Local`、`Remote` |
| `SupportedConnectTypes` | 旗標列舉：`Local`、`Remote`、`Both` |

## 設計慣例

- **策略模式（Strategy Pattern）** -- 行程內與 HTTP 兩種傳輸實作 `IJsonRpcTransport`；由 client 的工廠方法選定其一，每次呼叫都取得該種傳輸。
- **樣板方法（Template Method）** -- `ApiConnector.ExecuteAsync<T>` 定義固定步驟（轉換 payload、經傳輸送出、還原回應）；子類別提供領域專屬方法。
- **連接器隸屬於 client** -- 每個連接器只有一個接受 `PolhemApiClient` 的建構子（`FormApiConnector` 另外帶綁定的 `progId`），並從 `ApiConnector.Client` 取得連線與憑證。`PolhemApiClient.System` 與 `AuditLog` 是單一實例；`Form(progId)` 每次呼叫都建立新的連接器。
- **Payload 格式** -- 每個動作自行選擇 `PayloadFormat`；session 尚無傳輸金鑰時，`Encrypted` 請求改以 `Encoded` 送出；本機 client 除非開啟 `SysInfo.IsDebugMode`，否則送 `Plain`。`ApiConnector.PayloadCodec` 決定 `Encoded` / `Encrypted` 請求的 body codec（空白即 MessagePack）。

## 目錄結構

- 專案根目錄 -- `PolhemApiClient`、`ApiSessionContext`、`ApiSessionCredentials`、`ApiConnectValidator`、`ClientDefineAccess`、`ConnectType`、
  `SupportedConnectTypes`、`FormDataGuard`、`FormValueBinding`
- `Connectors/` -- `ApiConnector`、`SystemApiConnector`、`FormApiConnector`、`AuditLogApiConnector`
- `Providers/` -- 內部使用的行程內與 HTTP 傳輸
- `Definitions/` -- `FormDefinitionLoader`、`LanguageLayers`、`SnapshotLanguageService`
- `Permissions/` -- `IElementCapabilityResolver`、`ElementCapabilityResolver`、`FieldCapability`
