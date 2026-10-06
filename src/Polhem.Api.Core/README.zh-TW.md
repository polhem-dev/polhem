# Polhem.Api.Core

> 核心 API 框架，負責 JSON-RPC 執行、Payload 加密管線、授權驗證與型別對應。

[English](README.md)

## 架構定位

- **層級**：API 層（核心引擎）
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/architecture/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。它們確實漂了：`Polhem.Hosting` 抽出後，有四份 README 的下游數個月都沒把它補上。

## 目標框架

- `net10.0` -- 存取現代執行階段 API 與效能改進

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
  其他例外則依錯誤碼回應固定訊息，實際訊息由 `PolhemExceptionMapper` 寫入 log。

### Payload 安全管線

外殼、「序列化 -> 壓縮 -> 加密」管線、重放 frame 與重放紀錄都在
[`Polhem.JsonRpc.Payload`](https://github.com/polhem-dev/polhem-jsonrpc)。本套件提供 Polhem 這一側：

- `PolhemPayload` -- 以框架的方式組出套件的 `PayloadOptions`：MessagePack 為預設 codec、框架的 JSON 拼法與型別名稱，
  以及部署的 `ApiPayloadOptions` 指名的壓縮器與加密器（加密器 `none` 只限 debug 模式）。伺服器以 `AddPolhemPayload`
  （`Polhem.Hosting`）註冊；用戶端存放在 `ApiClientInfo.PayloadOptions`（`Polhem.Api.Client`）。
- `MessagePackPayloadCodec` -- 以框架 formatter 實作的 `messagepack` body codec。`json` codec 是套件的，使用框架的選項；
  其他 codec 以 `PayloadOptions.RegisterCodec` 登錄。
- `PayloadCodecNames` -- payload 宣告的 codec 名稱。

### Body codec 協商

`Encoded` 或 `Encrypted` payload 的 body codec 不是部署設定：每個請求在 payload 封套中宣告，伺服器以同一個
codec 回應。未宣告的請求以 MessagePack 解讀，這正是協商機制之前的所有用戶端送出的格式。用戶端由
`ApiConnector.PayloadCodec`（`Polhem.Api.Client`）選擇。見 [ADR-044](../../maintainers/adr/adr-044-payload-codec-negotiation.md)。

### 防重放（選用，預設關閉）

- frame（放在外殼內、body 之前的時間戳記與序號）與重放紀錄屬於 payload 套件：`PayloadFrame`、`IPayloadReplayStore`
  與記憶體內的預設實作 `MemoryPayloadReplayStore`。多節點部署註冊一個以共用儲存實作的 `IPayloadReplayStore`；Polhem
  以 access token 作為序號唯一的範圍。
- `PayloadOptions.RequireFrame` -- 總開關，伺服器經 `AddPolhemPayload`、.NET 用戶端經 `ApiClientInfo.PayloadOptions`
  設定；**用戶端與伺服端必須設為相同值**。
- `ApiReplayProtection` -- `ApiAccessControlAttribute` 的第三個維度，逐方法宣告是否檢查序號。以
  `AddPolhemFramework` 建立的 host 在方法宣告了它、但 frame 關閉時，會記錄一則啟動警告。

序號檢查適用於 `Encrypted` payload，此時 payload HMAC 涵蓋 frame。`Plain` 呼叫不帶 frame、不做檢查；
`Encoded` 的 frame 沒有經過驗證，因此需要防重放的方法請宣告為 `Encrypted` 保護等級。推行順序與細節見
[ADR-042](../../maintainers/adr/adr-042-api-replay-protection.md)。

### 授權與存取控制

- `IApiAuthorizationValidator` / `ApiAuthorizationValidator` -- 驗證傳入請求的授權上下文。`AddPolhemFramework` 註冊預設值，
  host 註冊自己的實作即可替換。
- `ApiAuthorizationContext` / `ApiAuthorizationResult` -- 授權輸入與結果型別。
- `ApiAccessValidator` -- 透過 `ApiAccessControlAttribute` 強制方法層級保護。
- `ApiCallContext` -- 每次呼叫的中繼資料（Token、保護等級、呼叫者身分）。

### 型別對應

- `ApiOutputConverter` -- 把商業物件結果轉成 wire 回應型別（依名稱複製屬性；入站方向為 internal）。
- `ApiHeaders` -- API 通訊的標準 Header 常數。
- `DateTimeWireGuard` -- 對帶有日期時間的回應強制 [ADR-032](../../maintainers/adr/adr-032-datetime-timezone.md)
  的 wire 不變式。

### MessagePack 基礎設施

> 這些型別皆為 `internal`。之所以在此說明，是因為它們定義了 wire 的行為，
> 但它們不屬於本套件的公開介面——要走同一條管線，請使用 `MessagePackPayloadCodec`（公開）。

- `SafeMessagePackSerializerOptions` / `WireTypeWhitelist` -- 將反序列化限制在允許清單內的型別。
- `MessagePackCodec` -- MessagePack 序列化的編碼器/解碼器。
- `WireContracts` -- 每個 wire 型別的顯式 formatter 註冊。contractless resolver 只是桌面端的
  便利，不是承載機制：.NET for iOS 關閉動態程式碼，未註冊的型別在那裡會直接失敗（見
  [ADR-037](../../maintainers/adr/adr-037-wire-explicit-registration.md)）。
- `WireValueFormatter` -- `object` 型別成員（篩選值、參數值、表格儲存格）的鑑別式信封。

### 內建訊息

- 內建操作的 request/response 型別位於 `Messages/System/`（登入、session、公司、定義、API 金鑰）、
  `Messages/Form/`（清單、資料、儲存、刪除、查詢）與 `Messages/AuditLog/`；`ExecFuncRequest` /
  `ExecFuncResponse` 位於 `Messages/`。

## 主要公開 API

| 類別 / 介面 | 用途 |
|-------------|------|
| `PolhemJsonRpc` | 建立框架提供 API 用的 JSON-RPC 伺服器選項 |
| `PolhemPayload` | 以框架的方式組出 payload 選項 |
| `MessagePackPayloadCodec` | `messagepack` body codec |
| `ApiAccessValidator` | 透過 `ApiAccessControlAttribute` 的方法層級保護 |
| `ApiAuthorizationValidator` | 請求授權驗證 |
| `ApiCallContext` | 每次呼叫的中繼資料（Token、保護、身分） |

## 設計慣例

- **策略模式（Strategy Pattern）** -- codec、壓縮器、加密器是 payload 套件的介面（`IPayloadCodec`、`IPayloadCompressor`、`IPayloadEncryptor`），各自可在 `PayloadOptions` 上替換。
- **嚴格管線順序** -- payload 套件在出站時執行「序列化 -> 壓縮 -> 加密」，入站時執行「解密 -> 解壓縮 -> 反序列化」；此順序不可更動。
- **型別白名單** -- MessagePack 反序列化只接受明確允許清單內的型別。
- **反射式分派** -- dispatcher 依名稱解析並呼叫商業物件方法，將傳輸層與具體 BO 型別解耦。
- **保護等級** -- `ApiAccessControlAttribute` 宣告方法的 `ApiProtectionLevel` 與 `ApiAccessRequirement`；成員與意義見這兩個列舉的 XML 文件（`Polhem.Definition.Security`）。
- 啟用 **Nullable Reference Types**（`<Nullable>enable</Nullable>`）。

## 目錄結構

- `Authorization/` -- `IApiAuthorizationValidator`、`ApiAuthorizationValidator`、`ApiAuthorizationContext`、`ApiAuthorizationResult`
- `Conversion/` -- API 型別與 BO 型別之間的 .NET 物件模型轉換（`ApiOutputConverter`）
- `Json/` -- `object` 型別成員的 JSON converter
- `Dispatch/` -- 把 Polhem 接上 `Polhem.JsonRpc.Server` dispatcher 的元件
- `JsonRpc/` -- `ActionPayloadType`、`DateTimeWireGuard`、錯誤碼與錯誤合約
- `Messages/` -- `ApiRequest`、`ApiResponse`、`ApiHeaders`、`ExecFunc*`，以及 `System/`、`Form/`、`AuditLog/` 訊息
- `MessagePack/` -- 內部的 MessagePack 基礎設施與 formatter
- `Transformers/` -- payload 管線中 Polhem 這一側（`PolhemPayload`、`MessagePackPayloadCodec`、`PayloadCodecNames`）
- `Validator/` -- `ApiAccessValidator`、`ApiCallContext`
- `Wire/` -- `WireValueCode`（兩種 wire 共用的鑑別碼）

命名空間佈局遵循 [ADR-008](../../maintainers/adr/adr-008-polhem-db-namespace-layout.md) 的設計原則：
依職責分組（`Messages` 放訊息型別、`Conversion` 放型別轉換、`Transformers` 放位元組層級管線等）；
根層保留給跨切面基礎設施，目前沒有。
