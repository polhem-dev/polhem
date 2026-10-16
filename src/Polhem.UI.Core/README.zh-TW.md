# Polhem.UI.Core

> 所有 Polhem 前端共用的前端層：UI 使用的服務合約、各 head 共用的表單值規則，以及權限能力解析器。不含傳輸。

[English](README.md)

## 架構定位

- **層級**：UI 層（共享前端基礎）
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/architecture/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。它們確實漂了：`Polhem.Hosting` 抽出後，有四份 README 的下游數個月都沒把它補上。
- 所有前端都參考它，Blazor head（`Polhem.Web.Blazor.Server`）也不例外。它只透過 `Polhem.Api.Contracts` 的合約接觸後端，既不參考 `Polhem.Api.Client`，也不參考 `Polhem.Api.Core`。理由見 [ADR-054](../../maintainers/adr/adr-054-front-end-decoupled-from-api-client.md)。
- 把單一使用者的 head 接上 Polhem 後端是 [`Polhem.UI.ApiBridge`](../Polhem.UI.ApiBridge/README.zh-TW.md) 的職責，它以 API client 實作下列合約。

## 目標框架

- `net10.0` -- 使用現代執行階段 API 與效能改進

## 概觀

`Polhem.UI.Core` 是所有前端共用、與 UI 框架無關的基礎層。它以合約宣告 UI 需要從外部取得的東西（表單的定義、
登入使用者的情境、每個程式一個連接器），並持有在每個 head 都必須一致的表單值規則。它不含任何 UI 框架型別，
也不含傳輸：建立在它之上的 UI 可以透過 `Polhem.UI.ApiBridge` 對接 Polhem 後端，也可以由應用程式自行實作
`IFormApiConnector`，對接自己的資料。

## 主要型別

### 供 markup 建立的控制項使用的服務定位

- `UIServices` -- 由 XAML loader 建立的控制項用來解析服務的行程層級 `IServiceProvider`（`Provider`）；未設定
  provider 或服務未註冊時，`Get<T>()` 回傳 `null`。單一使用者的 head 在啟動時設定一次；什麼都沒註冊時，
  控制項在沒有後端的情況下運作。在同一行程服務多位使用者的 host 不得使用它。

### 前端服務合約

- `IUserContext` -- 前端眼中的登入使用者：`User`、`Capabilities`（依權限模型）、`Company`，以及部署的
  `DefaultLanguage`。
- `Definitions/IFormDefinitionSource` -- 提供建立表單畫面所需的表單結構、版面，以及幣別與單位主檔。由實作決定語系，
  且回傳的每個實例都屬於呼叫端。
- `Definitions/StorageDefinitionSource` -- 以 `IDefineStorage`（例如讀取本機資料夾的 `FileDefineStorage`）為基礎的
  `IFormDefinitionSource`，供沒有 Polhem 後端的前端使用。它照儲存原樣回傳定義，不套用語系資源，也不套用租戶客製。
- 連接器合約本身 `IFormApiConnector` / `IFormApiConnectorFactory` 位於 `Polhem.Api.Contracts`
  （命名空間 `Polhem.Api.Contracts.Form`），因此 API client 不必參考 UI 就能實作它。

### 自行實作連接器時使用的結果型別

- `Connectors/FormListResult`、`FormDataResult`、`FormDeleteResult` -- 實作 `Polhem.Api.Contracts.Form` 回應介面的
  簡單 record，供不接 Polhem 後端、而是以自己的資料實作 `IFormApiConnector` 的應用程式使用。

### 各 head 共用的表單值規則

- `FormValueBinding` -- 在表單 `DataSet` 中的值與 UI 繫結交換的字串之間轉換，並建立表單起始用的空 `DataSet`。
- `FormDataGuard` -- 表單的 CRUD 呼叫送到連接器前檢查的前置條件；每種失敗在所有 head 都只有一種措辭。

### 權限能力

- `Permissions/IElementCapabilityResolver`、`ElementCapabilityResolver`、`FieldCapability` -- 把使用者依模型的能力
  轉成元素層級的決策（欄位可見或唯讀）。

> 用戶端能力解析**僅為 UX 降級**。後端仍是權威的安全邊界。

## 設計慣例

- **不含傳輸** -- 套件只參考 `Polhem.Api.Contracts`。參考 `Polhem.Api.Client`、`Polhem.Api.Core` 或
  `Polhem.UI.ApiBridge` 時，`POLHEM9001`（`src/Directory.Build.targets`）會讓建置失敗。
- **與 UI 框架無關** -- 不含 UI 框架型別，因此同一套合約與規則可服務所有前端。
- **畫面的解析順序** -- 控制項先用自己的屬性，再用可覆寫的 `Resolve*` hook，最後才是 `UIServices.Provider`；
  三者皆無時，就以 host 設定的內容運作。
- **行程層級狀態不進入多使用者 host** -- `UIServices` 是靜態的。Blazor head 參考本套件只為了值規則；
  它若參考 `UIServices` 或 `Polhem.UI.ApiBridge`，`UIServicesUsageGateTests`
  （`tests/Polhem.Web.Blazor.Server.UnitTests`）會失敗。
- 啟用 **Nullable Reference Types**（`<Nullable>enable</Nullable>`）。

## 目錄結構

- 專案根目錄 -- `UIServices`、`IUserContext`、`FormValueBinding`、`FormDataGuard`
- `Definitions/` -- `IFormDefinitionSource`、`StorageDefinitionSource`
- `Connectors/` -- `FormListResult`、`FormDataResult`、`FormDeleteResult`
- `Permissions/` -- `IElementCapabilityResolver`、`ElementCapabilityResolver`、`FieldCapability`
