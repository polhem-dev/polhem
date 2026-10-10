# Polhem.UI.Core

> `Polhem.UI.*` native 前端家族（目前為 Avalonia）共享的用戶端基礎層：連線狀態、連接器建立、endpoint 與 API 金鑰持久化。

[English](README.md)

## 架構定位

- **層級**：UI 層（共享用戶端基礎）
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/architecture/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。它們確實漂了：`Polhem.Hosting` 抽出後，有四份 README 的下游數個月都沒把它補上。
- Blazor 家族**不**消費 `Polhem.UI.Core`，見 [ADR-013](../../maintainers/adr/adr-013-frontend-api-connection-strategy.md)。

## 目標框架

- `net10.0` -- 使用現代執行階段 API 與效能改進

## 概觀

`Polhem.UI.Core` 是所有 `Polhem.UI.*` 前端共用、與 UI 框架無關的基礎層。它持有 per-process 的用戶端連線
狀態（`ClientInfo`），並抽象化服務 endpoint 與 API 金鑰的持久化位置（`IEndpointStorage`、`IApiKeyStorage`）。
它不含任何 UI 框架型別。

`Polhem.Web.Blazor.Server` 在同一行程服務多位使用者，因此把連線狀態放在各 circuit，而不是 `ClientInfo`。
兩個家族真正共用的部分在下一層的 `Polhem.Api.Client`：連接器、`ClientDefineAccess`、`FormDefinitionLoader`
與權限能力解析器。

## 主要型別

### 連線狀態

- `ClientInfo` -- 用戶端的靜態連線狀態。持有 head 唯一的 `ApiClient`（`PolhemApiClient`；per-process 身分
  模型：重新登入會清掉快取的 `DefineAccess` 與能力快照，換 endpoint 則會替換 client 並登出），公開其
  `AccessToken`、`ConnectType` 與 `SystemApiConnector`、延遲建立 `DefineAccess`（`ClientDefineAccess`）、透過
  `CreateFormApiConnector(progId)` 與 `CreateAuditLogApiConnector()` 產生連接器、經 `InitializeAsync` /
  `SetEndpointAsync` 解析 endpoint（本機或遠端，受 `SupportedConnectTypes` 限制），並套用登入 /
  EnterCompany 結果（`ApplyLoginResult`、`ApplyEnterCompanyResult`、`ClearCompanyContext`）。
  `ResetDefineCache` 在切換租戶後丟棄快取的定義資料。
- `ClientInfo.LocalServiceProvider` -- 在行程內執行後端的 head 的後端服務提供者（以 `AddPolhemFramework`
  建立）；行程內的 `ApiClient` 會派遣到它。
- `ClientInfo.UseDefinitionLoader` / `DefinitionLoader` -- 透過 `FormDefinitionLoader` 取得在地化的表單定義。

### Endpoint 與 API 金鑰持久化

- `IEndpointStorage` -- 已設定服務 endpoint 的持久化合約（`LoadEndpoint` / `SetEndpoint` / `SaveEndpoint`）。
- `IApiKeyStorage` -- `X-Api-Key` 值的同一組合約（`LoadApiKey` / `SetApiKey` / `SaveApiKey`）。
- `FileEndpointStorage` -- `ClientInfo.EndpointStorage` 與 `ClientInfo.ApiKeyStorage` 兩者的預設：在每位使用者
  的本機應用程式資料目錄下、每個應用程式一個資料夾，每個值一個單行文字檔。各平台的資料夾位置列在它的
  XML 文件中。Browser WASM 沒有持久的檔案系統，因此瀏覽器 host 會把兩個屬性指派為以瀏覽器儲存為後端的實作。
- `ClientInfo.ApplyApiKey(defaultApiKey)` -- 套用已儲存的金鑰；儲存為空時以應用程式出貨時帶的值初始化。
  如此出貨常數只是首次執行的預設值而非寫死的金鑰：之後以儲存值為準，不需重新編譯即可變更。
  `ClientInfo.SetApiKey` 會持久化新金鑰，並套用到後續呼叫。

> 用戶端持有的 API 金鑰在密碼學意義上**不是機密** —— 它可以從出貨的應用程式中取回。它識別的是
> *哪個應用程式*在呼叫；驗證*使用者*仍是存取權杖（access token）的職責。

### Host 服務

- `IUIViewService` -- 由 host UI 框架提供的檢視服務（例如 endpoint 缺漏或無法連線時，以
  `ShowApiConnectAsync` 提示使用者設定連線）。

### 權限能力

- `ClientInfo.Capabilities` -- 進入公司時取得的 per-model 權限快照。把它轉成元素層級決策的
  `ElementCapabilityResolver` 位於 `Polhem.Api.Client`，兩個 UI 家族都使用它。

> 用戶端能力解析**僅為 UX 降級**。後端仍是權威的安全邊界。

## 設計慣例

- **Per-process token 模型** -- `ClientInfo` 為靜態類別，整個 process 持有一個 access token；變更 token 會
  使快取的連接器、定義存取器與能力快照失效。
- **與 UI 框架無關** -- 不引入任何 UI 框架型別，因此同一套連線邏輯可服務所有 `Polhem.UI.*` 前端。
- **可插拔的 endpoint / API 金鑰儲存** -- host 以適合平台的實作替換 `ClientInfo.EndpointStorage` 與
  `ClientInfo.ApiKeyStorage`。
- **非同步友善的初始化** -- `InitializeAsync` / `SetEndpointAsync` 以非阻塞方式驗證 endpoint 並初始化
  連接器，因此在單執行緒 runtime（browser WASM）上也安全。
- 啟用 **Nullable Reference Types**（`<Nullable>enable</Nullable>`）。

## 目錄結構

- `ClientInfo.cs` -- 用戶端連線狀態與連接器建立
- `IEndpointStorage.cs` / `IApiKeyStorage.cs` -- 持久化合約
- `FileEndpointStorage.cs` -- 兩者預設的檔案式實作
- `IUIViewService.cs` -- host 提供的檢視服務
