# Polhem.Api.Server

> Polhem JSON-RPC API 的伺服器端：dispatcher 設定、存取與 payload 篩選器、API 金鑰與授權檢查，以及例外對應。

[English](README.md)

## 架構定位

- **層級**：API 層（伺服器端）。與用戶端共用的 wire 層是
  [`Polhem.Api.Core`](../Polhem.Api.Core/README.zh-TW.md)；見
  [ADR-055](../../maintainers/adr/adr-055-api-core-split-from-server.md)。
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/architecture/dependency-map.md)。
- 應用程式通常不直接參考本套件：`Polhem.Hosting` 參考它，`AddPolhemFramework` 會註冊下列所有東西。
- 本套件會讀取 `Polhem.Api.Core` 的 internal 成員，因此對該套件的相依是完全相同的版本。

## 目標框架

- `net10.0`

## 主要功能

### JSON-RPC 執行

- 協定由 [`Polhem.JsonRpc.Server`](https://github.com/polhem-dev/polhem-jsonrpc) 處理：它的 dispatcher 解析
  `ProgId.Action` 方法識別碼並呼叫目標方法。一個 action 名稱能呼叫到哪些方法，由該套件的
  `JsonRpcMethod.IsResolvableAction` 決定。
- `Dispatch/` 把 Polhem 接上它：`PolhemObjectFactory` 檢查 HTTP 呼叫的 API key 與 `Authorization` header，並從
  `IBusinessObjectFactory` 取得商業物件；`PolhemAccessFilter` 套用 `[ApiAccessControl]`；`PolhemPayloadFilter` 經
  `Polhem.JsonRpc.Payload.Server` 開啟與寫出 payload 外殼；`PolhemParameterBinder` 繫結參數；`PolhemExceptionMapper` 對應錯誤。
  `PolhemJsonRpc.CreateServerOptions` 把它們組起來。
- 錯誤回應 -- 給終端使用者看的框架例外（`UserMessageException` 及相關型別）會帶著訊息回到呼叫端；
  其他例外則依錯誤碼回應固定訊息，實際訊息由 `PolhemExceptionMapper` 寫入 log。錯誤碼即 `Polhem.Api.Core` 的錯誤契約。
- `InProcessTransportFactory` -- 同程序用戶端（`PolhemApiClient.CreateLocal`）呼叫 dispatcher 時使用的 transport；
  它把每次呼叫標記為本機呼叫，並帶上 access token。

### 授權與存取控制

- `IApiAuthorizationValidator` / `ApiAuthorizationValidator` -- 驗證傳入請求的授權上下文。`AddPolhemFramework` 註冊預設值，
  host 註冊自己的實作即可替換。
- `ApiAuthorizationContext` / `ApiAuthorizationResult` -- 授權輸入與結果型別。
- `ApiAccessValidator` -- 透過 `ApiAccessControlAttribute` 強制方法層級保護。
- `ApiCallContext` -- 每次呼叫的中繼資料（Token、保護等級、呼叫者身分）。

## 主要公開 API

| 類別 / 介面 | 用途 |
|-------------|------|
| `PolhemJsonRpc` | 建立框架提供 API 用的 JSON-RPC 伺服器選項 |
| `IApiAuthorizationValidator` | 請求授權；替換它即可改變哪些呼叫需要金鑰或 token |
| `ApiAccessValidator` | 透過 `ApiAccessControlAttribute` 的方法層級保護 |
| `InProcessTransportFactory` | 同程序用戶端的 transport |

## 設計慣例

- **反射式分派** -- dispatcher 依名稱解析並呼叫商業物件方法，將傳輸層與具體 BO 型別解耦。
- **保護等級** -- `ApiAccessControlAttribute` 宣告方法的 `ApiProtectionLevel` 與 `ApiAccessRequirement`；成員與意義見這兩個列舉的 XML 文件（`Polhem.Definition.Security`）。

## 目錄結構

- `Authorization/` -- `IApiAuthorizationValidator`、`ApiAuthorizationValidator`、`ApiAuthorizationContext`、`ApiAuthorizationResult`
- `Dispatch/` -- 把 Polhem 接上 `Polhem.JsonRpc.Server` dispatcher 的元件，以及 `InProcessTransportFactory`
- `Validator/` -- `ApiAccessValidator`、`ApiCallContext`
