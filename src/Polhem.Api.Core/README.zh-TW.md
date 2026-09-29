# Polhem.Api.Core

> 核心 API 框架，負責 JSON-RPC 執行、Payload 加密管線、授權驗證與型別對應。

[English](README.md)

## 架構定位

- **層級**：API 層（核心引擎）
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。它們確實漂了：`Polhem.Hosting` 抽出後，有四份 README 的下游數個月都沒把它補上。

## 目標框架

- `net10.0` -- 存取現代執行階段 API 與效能改進

## 主要功能

### JSON-RPC 執行

- `JsonRpcExecutor` -- 解析 `ProgId.Action` 方法識別碼，從 `IBusinessObjectFactory` 取得商業物件並呼叫目標方法。
  一個 action 名稱能呼叫到哪些方法，由 `JsonRpcExecutor.IsResolvableAction` 決定。
- `JsonRpcRequest` / `JsonRpcResponse` / `JsonRpcError` -- 標準 JSON-RPC 2.0 訊息型別。
- `ApiPayload` / `ApiPayloadConverter` -- JSON-RPC 傳輸的 Payload 包裝與轉換。
- 錯誤回應 -- 給終端使用者看的框架例外（`UserMessageException` 及相關型別）會帶著訊息回到呼叫端；
  其他例外則依錯誤碼回應固定訊息，實際訊息寫入 `JsonRpcExecutor.Logger`。

### Payload 安全管線

- `ApiPayloadTransformer` -- 協調「序列化 -> 壓縮 -> 加密」管線（入站時反向執行）。
- `IApiPayloadSerializer` -- body codec。框架內建 `MessagePackPayloadSerializer` 與 `JsonPayloadSerializer`
  （名稱見 `PayloadCodecNames`）；其他 codec 以 `ApiServiceOptions.RegisterPayloadCodec` 加入。
- `IApiPayloadCompressor` / `GzipPayloadCompressor` -- 可插拔的 Gzip 壓縮。
- `IApiPayloadEncryptor` / `AesPayloadEncryptor` -- 可插拔的 AES-CBC-HMAC 加密。
- `ApiPayloadOptionsFactory` -- 依部署的 `ApiPayloadOptions` 建立指定的壓縮器與加密器。

### Body codec 協商

`Encoded` 或 `Encrypted` payload 的 body codec 不是部署設定：每個請求在 payload 封套中宣告，伺服器以同一個
codec 回應。未宣告的請求以 MessagePack 解讀，這正是協商機制之前的所有用戶端送出的格式。用戶端由
`ApiConnector.PayloadCodec`（`Polhem.Api.Client`）選擇。見 [ADR-044](../../maintainers/adr/adr-044-payload-codec-negotiation.md)。

### 防重放（選用，預設關閉）

- `ApiPayloadFrame` -- 放在封套內、payload body 之前的時間戳記與序號。
- `IReplayWindowStore` -- 以單一原子呼叫、依 session 決定某個序號可否接受。預設的
  `MemoryReplayWindowStore` 在行程記憶體中維護滑動視窗；多節點部署可以在共用儲存上實作此介面，
  並指派給 `ApiServiceOptions.ReplayWindowStore`。
- `ApiServiceOptions.RequireWireFrame` -- 總開關；**用戶端與伺服端必須設為相同值**。
- `ApiReplayProtection` -- `ApiAccessControlAttribute` 的第三個維度，逐方法宣告是否檢查序號。以
  `AddPolhemFramework` 建立的 host 在方法宣告了它、但 frame 關閉時，會記錄一則啟動警告。

序號檢查適用於 `Encrypted` payload，此時 payload HMAC 涵蓋 frame。`Plain` 呼叫不帶 frame、不做檢查；
`Encoded` 的 frame 沒有經過驗證，因此需要防重放的方法請宣告為 `Encrypted` 保護等級。推行順序與細節見
[ADR-042](../../maintainers/adr/adr-042-api-replay-protection.md)。

### 授權與存取控制

- `IApiAuthorizationValidator` / `ApiAuthorizationValidator` -- 驗證傳入請求的授權上下文。
- `ApiAuthorizationContext` / `ApiAuthorizationResult` -- 授權輸入與結果型別。
- `ApiAccessValidator` -- 透過 `ApiAccessControlAttribute` 強制方法層級保護。
- `ApiCallContext` -- 每次呼叫的中繼資料（Token、保護等級、呼叫者身分）。

### 型別對應

- `ApiOutputConverter` -- 把商業物件結果轉成 wire 回應型別（依名稱複製屬性；入站方向為 internal）。
- `ApiHeaders` -- API 通訊的標準 Header 常數。
- `PayloadFormat` -- payload 的傳輸方式：`Plain`、`Encoded`（序列化並壓縮）或 `Encrypted`。
- `DateTimeWireGuard` -- 對帶有日期時間的回應強制 [ADR-032](../../maintainers/adr/adr-032-datetime-timezone.md)
  的 wire 不變式。

### MessagePack 基礎設施

> 這些型別皆為 `internal`。之所以在此說明，是因為它們定義了 wire 的行為，
> 但它們不屬於本套件的公開介面——要走同一條管線，請使用 `MessagePackPayloadSerializer`（公開）。

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
| `JsonRpcExecutor` | 解析 `ProgId.Action`、建立 BO、呼叫方法 |
| `ApiServiceOptions` | 管線元件、codec、授權驗證器與重放儲存的行程層級設定 |
| `ApiPayloadTransformer` | 序列化 -> 壓縮 -> 加密管線 |
| `ApiAccessValidator` | 透過 `ApiAccessControlAttribute` 的方法層級保護 |
| `PayloadFormat` | Payload 格式列舉（`Plain`、`Encoded`、`Encrypted`） |
| `ApiAuthorizationValidator` | 請求授權驗證 |
| `ApiCallContext` | 每次呼叫的中繼資料（Token、保護、身分） |
| `IReplayWindowStore` | 可替換的每 session 序號檢查 |

## 設計慣例

- **策略模式（Strategy Pattern）** -- 序列化器、壓縮器、加密器皆透過介面注入（`IApiPayloadSerializer`、`IApiPayloadCompressor`、`IApiPayloadEncryptor`），每個階段可獨立替換。
- **嚴格管線順序** -- Payload 轉換器在出站時執行「序列化 -> 壓縮 -> 加密」，入站時執行「解密 -> 解壓縮 -> 反序列化」；此順序不可更動。
- **型別白名單** -- MessagePack 反序列化只接受明確允許清單內的型別。
- **反射式分派** -- `JsonRpcExecutor` 依名稱解析並呼叫商業物件方法，將傳輸層與具體 BO 型別解耦。
- **保護等級** -- `ApiAccessControlAttribute` 宣告方法的 `ApiProtectionLevel` 與 `ApiAccessRequirement`；成員與意義見這兩個列舉的 XML 文件（`Polhem.Definition.Security`）。
- 啟用 **Nullable Reference Types**（`<Nullable>enable</Nullable>`）。

## 目錄結構

- `Authorization/` -- `IApiAuthorizationValidator`、`ApiAuthorizationValidator`、`ApiAuthorizationContext`、`ApiAuthorizationResult`
- `Conversion/` -- API 型別與 BO 型別之間的 .NET 物件模型轉換（`ApiOutputConverter`）
- `Json/` -- `object` 型別成員的 JSON converter
- `JsonRpc/` -- `JsonRpcExecutor`、JSON-RPC 訊息型別、`ApiPayload`、`ApiPayloadFrame`、`IReplayWindowStore`、`DateTimeWireGuard`
- `Messages/` -- `ApiRequest`、`ApiResponse`、`ApiHeaders`、`PayloadFormat`、`ExecFunc*`，以及 `System/`、`Form/`、`AuditLog/` 訊息
- `MessagePack/` -- 內部的 MessagePack 基礎設施與 formatter
- `Transformers/` -- 位元組層級的 payload 管線（序列化器、壓縮器、加密器、`ApiPayloadOptionsFactory`、`PayloadCodecNames`）
- `Validator/` -- `ApiAccessValidator`、`ApiCallContext`
- `Wire/` -- `WireValueCode`（兩種 wire 共用的鑑別碼）
- 專案根目錄 -- `ApiServiceOptions`（啟動設定）

命名空間佈局遵循 [ADR-008](../../maintainers/adr/adr-008-polhem-db-namespace-layout.md) 的設計原則：
依職責分組（`Messages` 放訊息型別、`Conversion` 放型別轉換、`Transformers` 放位元組層級管線等）；
根層保留給跨切面基礎設施（在此僅有 `ApiServiceOptions`）。
