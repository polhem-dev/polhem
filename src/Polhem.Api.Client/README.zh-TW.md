# Polhem.Api.Client

> API 用戶端連接器，提供統一介面以進行本機（行程內）與遠端（網路）商業邏輯呼叫。

[English](README.md)

## 架構定位

- **層級**：前端 / 用戶端
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。它們確實漂了：`Polhem.Hosting` 抽出後，有四份 README 的下游數個月都沒把它補上。
- 由應用程式消費（Avalonia、Blazor、主控台及其他 head）。

## 目標框架

- `net10.0` -- 存取現代執行階段 API 與效能改進

## 主要功能

### 本機 / 遠端策略

- `IJsonRpcProvider` 抽象化傳輸層；`LocalApiProvider` 透過 `JsonRpcExecutor` 在行程內呼叫商業邏輯，`RemoteApiProvider` 則以 HTTP POST 呼叫遠端端點。
- 策略由連接器建構子決定：接受 `IServiceProvider`（以 `AddPolhemFramework` 建立的後端服務提供者）的建構子走行程內，接受端點 URL 的建構子走 HTTP。

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

- `ApiConnectValidator.ValidateAsync` 依端點字串判斷 `ConnectType`（Local 或 Remote）、驗證目標，並可選擇為本機連線產生缺少的設定檔。
- 遠端驗證會先執行 `Ping` 確認連線可用後才回傳。

### 快取定義存取

- `ClientDefineAccess` 透過 `SystemApiConnector` 讀寫定義（`GetFormSchemaAsync`、`GetFormLayoutAsync`、
  `GetSystemSettingsAsync`、`SaveFormSchemaAsync` 等），並快取讀過的內容。它是一組獨立的非同步 API，
  不實作伺服端的 `IDefineAccess`。
- `FormDefinitionLoader` 由這些定義組出 UI 需要的在地化 `FormSchema` 與執行期 `FormLayout`。

### 用戶端狀態

- `ApiClientInfo` 持有行程層級的設定：`ConnectType`、`Endpoint`、`ApiKey`、`DefaultLanguage` 與
  `SupportedConnectTypes`。
- `ApiSessionContext` 持有單一登入使用者的狀態（登入時建立的傳輸金鑰與使用者時區）。單一使用者的 head
  共用 `ApiSessionContext.Ambient`；在同一行程服務多位使用者的 host（Blazor Server）則每個 session
  傳一個給連接器建構子。

### 各 head 共用的 UI 輔助

- `ElementCapabilityResolver` -- 依使用者權限判斷欄位可見或唯讀。
- `FormDataGuard` / `FormValueBinding` -- CRUD 前置條件，以及 UI 與 `DataRow` 之間的值轉換。

## 主要公開 API

| 類別 / 介面 | 用途 |
|-------------|------|
| `ApiClientInfo` | 行程層級的用戶端設定（連線類型、端點、API 金鑰、預設語言） |
| `ApiSessionContext` | 每位使用者的用戶端狀態（傳輸金鑰、時區） |
| `ApiConnector` | 抽象基底連接器，含 payload 管線 |
| `SystemApiConnector` | 系統層級操作 |
| `FormApiConnector` | 綁定特定 ProgId 的表單層級商業物件呼叫 |
| `AuditLogApiConnector` | 稽核與異常紀錄查詢 |
| `IJsonRpcProvider` | JSON-RPC 傳輸策略介面 |
| `LocalApiProvider` | 透過 `JsonRpcExecutor` 的行程內提供者 |
| `RemoteApiProvider` | 以 HTTP 為基礎的提供者，附帶 API 金鑰與 Bearer Token 標頭 |
| `ClientDefineAccess` | 透過 API 的非同步快取定義存取 |
| `FormDefinitionLoader` | 供 UI 使用的在地化表單結構與執行期版面 |
| `ApiConnectValidator` | 驗證端點並判斷連線類型 |
| `ConnectType` | 列舉：`Local`、`Remote` |
| `SupportedConnectTypes` | 旗標列舉：`Local`、`Remote`、`Both` |

## 設計慣例

- **策略模式（Strategy Pattern）** -- `IJsonRpcProvider` 搭配 `LocalApiProvider` 與 `RemoteApiProvider` 實作；連接器在建構時選定策略。
- **樣板方法（Template Method）** -- `ApiConnector.ExecuteAsync<T>` 定義固定步驟（建立請求、轉換 payload、呼叫提供者、還原回應）；子類別提供領域專屬方法。
- **雙建構子模式** -- 每個連接器提供本機與遠端兩種建構子，對應兩種提供者：`SystemApiConnector(IServiceProvider services, Guid accessToken)` / `(string endpoint, Guid accessToken)`。`FormApiConnector` 另外帶綁定的 `progId`。每個建構子都有再多帶一個 `ApiSessionContext` 的多載。
- **Payload 格式** -- 每個動作自行選擇 `PayloadFormat`；session 尚無傳輸金鑰時，`Encrypted` 請求改以 `Encoded` 送出；本機提供者除非開啟 `SysInfo.IsDebugMode`，否則送 `Plain`。`ApiConnector.PayloadCodec` 決定 `Encoded` / `Encrypted` 請求的 body codec（空白即 MessagePack）。

## 目錄結構

- 專案根目錄 -- `ApiClientInfo`、`ApiSessionContext`、`ApiConnectValidator`、`ClientDefineAccess`、`ConnectType`、
  `SupportedConnectTypes`、`FormDataGuard`、`FormValueBinding`
- `Connectors/` -- `ApiConnector`、`SystemApiConnector`、`FormApiConnector`、`AuditLogApiConnector`
- `Providers/` -- `IJsonRpcProvider`、`LocalApiProvider`、`RemoteApiProvider`
- `Definitions/` -- `FormDefinitionLoader`、`LanguageLayers`、`SnapshotLanguageService`
- `Permissions/` -- `IElementCapabilityResolver`、`ElementCapabilityResolver`、`FieldCapability`
