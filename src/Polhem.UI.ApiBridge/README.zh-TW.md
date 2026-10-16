# Polhem.UI.ApiBridge

> 把單一使用者的 Polhem 前端接上 Polhem 後端：用戶端連線狀態、endpoint 與 API 金鑰持久化，以及 `Polhem.UI.Core` 服務的 API 實作。

[English](README.md)

## 架構定位

- **層級**：UI 層（共享前端層與 API client 之間的轉接層）
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/architecture/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。
- 它同時參考 [`Polhem.UI.Core`](../Polhem.UI.Core/README.zh-TW.md) 與 [`Polhem.Api.Client`](../Polhem.Api.Client/README.zh-TW.md)，因此相依方向是從 bridge 指向兩側：API client 不參考 UI，UI 也不參考 API client。理由見 [ADR-054](../../maintainers/adr/adr-054-front-end-decoupled-from-api-client.md)。
- **僅適用單一使用者的 head**（桌面、行動裝置、瀏覽器 WebAssembly）。在同一行程服務多位使用者的 host（例如 Blazor Server）不得使用它：`ClientInfo` 在每個行程只持有一位登入使用者。

## 目標框架

- `net10.0` -- 使用現代執行階段 API 與效能改進

## 概觀

建立在 `Polhem.UI.Core` 上的前端（例如 `Polhem.UI.Avalonia` 的控制項）透過合約解析它的連接器、定義與使用者情境。
本套件提供從 Polhem 後端讀取這些內容的實作，並持有這些實作讀取的 per-process 連線狀態（`ClientInfo`）。
它不含 UI 框架型別，因此任何單一使用者的前端都可原封不動地重用。

## 註冊

head 在啟動時註冊一次 bridge，並把 provider 交給控制項：

```csharp
using Microsoft.Extensions.DependencyInjection;
using Polhem.UI.ApiBridge.DependencyInjection;
using Polhem.UI.Core;

UIServices.Provider = new ServiceCollection().AddPolhemUIApiBridge().BuildServiceProvider();
```

`AddPolhemUIApiBridge()` 註冊：

| 服務（合約） | 實作 | 讀取來源 |
|---|---|---|
| `IFormApiConnectorFactory`（`Polhem.Api.Contracts.Form`） | `Connectors/FormApiConnectorFactory` | `ClientInfo.ApiClient` |
| `IFormDefinitionSource`（`Polhem.UI.Core.Definitions`） | `Definitions/ApiFormDefinitionSource` | `ClientInfo.DefineAccess`、`ClientInfo.DefinitionLoader` |
| `IUserContext`（`Polhem.UI.Core`） | `ClientUserContext` | `ClientInfo.UserInfo`、`Capabilities`、`Company` |

每個實作在每次呼叫時才讀 `ClientInfo`，因此這次註冊在連線、登入與進入公司各步驟之間都有效；
endpoint 或使用者改變時不需要重新註冊。[`apps/Polhem.Northwind`](../../apps/Polhem.Northwind/README.zh-TW.md)
在 `Polhem.Northwind.UI/App.axaml.cs` 就是這樣接線的。

## 主要型別

### 連線狀態

- `ClientInfo` -- 用戶端的靜態連線狀態。持有 head 唯一的 `ApiClient`（`PolhemApiClient`；per-process 身分
  模型：重新登入會清掉快取的 `DefineAccess` 與能力快照，換 endpoint 則會替換 client 並登出），公開其
  `AccessToken`、`ConnectType` 與 `SystemApiConnector`、延遲建立 `DefineAccess`（`ClientDefineAccess`）、透過
  `CreateFormApiConnector(progId)` 與 `CreateAuditLogApiConnector()` 產生連接器，並套用登入 / EnterCompany 結果
  （`ApplyLoginResult`、`ApplyEnterCompanyResult`、`ClearCompanyContext`）。`ResetDefineCache` 在切換租戶後
  丟棄快取的定義資料。
- `ClientInfo.InitializeAsync(SupportedConnectTypes)` -- 讀取已儲存的 endpoint 與命令列後連線。endpoint 缺漏、
  格式錯誤或無法連線時回傳 `false`；host 接著顯示自己的連線設定畫面，再以使用者輸入的值呼叫
  `InitializeAsync(endpoint)`（或 `SetEndpointAsync`）。
- `ClientInfo.LocalServiceProvider` -- 在行程內執行後端的 head 的後端服務提供者（以 `AddPolhemFramework`
  建立）；行程內的 `ApiClient` 會派遣到它。
- `ClientInfo.UseDefinitionLoader` / `DefinitionLoader` -- 透過 `FormDefinitionLoader` 取得在地化的表單定義；
  有 loader 時 `ApiFormDefinitionSource` 使用它，否則使用儲存原樣的定義。

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

### 以 API 實作的服務

- `Connectors/FormApiConnectorFactory` -- 從目前的 client 建立某個程式的 `FormApiConnector`。它以委派取得 client，
  因為換 endpoint 會替換 client。
- `Definitions/ApiFormDefinitionSource` -- 從後端讀取表單結構、版面，以及幣別與單位主檔；有定義 loader 時，
  表單結構會依目前的 UI culture 在地化，版面也帶有租戶的客製。
- `ClientUserContext` -- 登入使用者的 `IUserContext`，從 `ClientInfo` 讀取。
- `DependencyInjection/PolhemUIApiBridgeServiceCollectionExtensions.AddPolhemUIApiBridge()` -- 註冊上述服務。

## 設計慣例

- **Per-process 身分模型** -- `ClientInfo` 為靜態類別，整個行程持有一位登入使用者。對桌面、行動裝置或瀏覽器 head
  是正確的；對多使用者 host 則是錯的，因為每個 session 都會寫同一組欄位。
- **與 UI 框架無關** -- 不引入任何 UI 框架型別，因此同一個 bridge 可服務所有單一使用者的前端。
- **可插拔的 endpoint / API 金鑰儲存** -- host 以適合平台的實作替換 `ClientInfo.EndpointStorage` 與
  `ClientInfo.ApiKeyStorage`。
- **非同步友善的初始化** -- `InitializeAsync` / `SetEndpointAsync` 以非阻塞方式驗證 endpoint 並初始化
  連接器，因此在單執行緒 runtime（browser WASM）上也安全。
- 啟用 **Nullable Reference Types**（`<Nullable>enable</Nullable>`）。

## 目錄結構

- 專案根目錄 -- `ClientInfo`、`ClientUserContext`、`IEndpointStorage`、`IApiKeyStorage`、`FileEndpointStorage`
- `Connectors/` -- `FormApiConnectorFactory`
- `Definitions/` -- `ApiFormDefinitionSource`
- `DependencyInjection/` -- `PolhemUIApiBridgeServiceCollectionExtensions`
