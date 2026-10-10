<!-- source: en/api/api-bo-contract-design.md blob: 7c66e880e61fa83420115e1735b33b041a162fd5 -->
# API 合約與 BO 參數設計原則

[English](../../en/api/api-bo-contract-design.md) · [← 文件索引](../README.md)

本文件說明 Polhem 框架中 API 合約（Request / Response）與 BO 參數（Args / Result）的設計架構與使用方式，供開發人員在擴充 API 方法或撰寫 BO 邏輯時參考。

---

## 核心觀念

框架將 API 傳輸層與 BO 業務邏輯層的參數型別分開管理，透過**合約介面**統一定義屬性，確保兩層各自獨立、關注點分離。

```
合約介面（ILoginRequest / ILoginResponse）   ← 定義共用屬性，唯一真實來源
     │
     ├── API 型別（LoginRequest / LoginResponse）  ← 可序列化，用於 API 傳輸
     │
     └── BO 型別（LoginArgs / LoginResult）        ← 純 POCO，用於業務邏輯
```

**為什麼要分層？**

- 用戶端（`Polhem.Api.Client`）只接觸 API 型別，不需知道 BO 實作細節
- BO 層不依賴 API 組件，可獨立測試與演進
- BO 可在合約之外為 BO 間呼叫新增屬性，不必更動 API 型別（遠端呼叫者仍可能設定它們，見情境二）

---

## 型別總覽

### 合約介面（Polhem.Api.Contracts）

定義 API 方法輸入與輸出的屬性合約，只包含唯讀屬性，不含任何序列化標記。介面依軸分入 `Polhem.Api.Contracts.System` / `.Form` / `.AuditLog`；root 的 `Polhem.Api.Contracts` 命名空間只保留跨軸共用型別（`IExecFuncRequest` / `IExecFuncResponse`）。

```csharp
namespace Polhem.Api.Contracts.System
{
    public interface ILoginRequest
    {
        string UserId { get; }
        string Password { get; }
        string ClientPublicKey { get; }
    }

    public interface ILoginResponse
    {
        Guid AccessToken { get; }
        DateTime ExpiredAt { get; }
        string ApiEncryptionKey { get; }
        string UserId { get; }
        string UserName { get; }
        string TimeZone { get; }
        string Culture { get; }
    }
}
```

### API 合約型別（Polhem.Api.Core.Messages.System）

繼承 `ApiRequest` / `ApiResponse`，實作合約介面。用戶端透過這些型別發送請求與接收回應。

它們**完全不帶任何序列化標註** —— 一個帶 public 可讀寫屬性的普通類別就是全部：

```csharp
public sealed class LoginRequest : ApiRequest, ILoginRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ClientPublicKey { get; set; } = string.Empty;
}

public sealed class LoginResponse : ApiResponse, ILoginResponse
{
    public Guid AccessToken { get; set; } = Guid.Empty;
    public DateTime ExpiredAt { get; set; }
    public string ApiEncryptionKey { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public string Culture { get; set; } = string.Empty;
}
```

那 wire 綁定在哪裡：

- **JSON** —— System.Text.Json 以屬性名綁定，不需要宣告任何東西。
- **MessagePack** —— 由 `src/Polhem.Api.Core/MessagePack/WireContracts.*.cs` 的手寫合約逐一列出成員。
  **新增訊息型別必須到那裡註冊**：resolver 在禁用動態碼的平台上沒有反射退路；
  wire 閉包與註冊對不上時，`WireContractDriftTests`（位於 `tests/Polhem.Api.Core.UnitTests`）會失敗。

標註之所以被拿掉，正是因為留著它們會把傳輸套件放進定義層每一個消費者的相依表面。
見 [ADR-036](../../../maintainers/adr/adr-036-wire-serialization-externalized.md)。

> **僅限框架 repository。** `WireContract`、`WireContracts` 與 `MessagePackCodec` 都是 `internal`，
> 因此本 repository 之外的應用**無法**為自己的訊息型別註冊 formatter。那種型別只能經由反射式
> resolver 上 MessagePack wire —— 桌面與伺服器可行，在沒有動態碼的執行環境會擲例外。
> 逐請求宣告 `codec: json`（[ADR-044](../../../maintainers/adr/adr-044-payload-codec-negotiation.md)）可完全繞開這個問題。

> **多型階層**（`FilterNode` 與其子型別）需要的不只是一份成員清單，因此有專屬的手寫 formatter —— `FilterNodeFormatter` —— 在成員旁邊寫入判別子。同一個檔案家族、同一套註冊方式，只有 formatter 是量身打造的。

### BO 參數型別（Polhem.Business）

繼承 `BusinessArgs` / `BusinessResult`，實作合約介面，純 POCO。可在合約屬性之外新增 BO 專用屬性（見下方情境二）。

```csharp
public sealed class LoginArgs : BusinessArgs, ILoginRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ClientPublicKey { get; set; } = string.Empty;
}
```

---

## 命名規則

| 用途 | 命名模式 | 範例 | 所在組件 |
|------|----------|------|----------|
| 合約介面（輸入） | `IXxxRequest` | `ILoginRequest` | Polhem.Api.Contracts |
| 合約介面（輸出） | `IXxxResponse` | `ILoginResponse` | Polhem.Api.Contracts |
| API 輸入 | `XxxRequest` | `LoginRequest` | Polhem.Api.Core |
| API 輸出 | `XxxResponse` | `LoginResponse` | Polhem.Api.Core |
| BO 輸入 | `XxxArgs` | `LoginArgs` | Polhem.Business |
| BO 輸出 | `XxxResult` | `LoginResult` | Polhem.Business |
| BO 流程內狀態 | `XxxContext` | `SaveContext` | Polhem.Business |

**判別法：跨層傳輸用 `Args` / `Result`，流程內共享狀態用 `Context`。** `XxxArgs` 由呼叫端反序列化
而來、`XxxResult` 序列化回去，兩者都是純 POCO，只承載 client 可以看到的資料。`XxxContext` 則
**不離開伺服端**：它承載單一次呼叫的各段所共用的東西——已解析的 repository 與 form schema、刪除前
的快照、前一段產生給後一段用的輸出——其中不乏不該上 wire 的伺服端物件。

選型前值得知道兩個推論。放在 `Args` 上的東西**呼叫端也可能填過**，所以把中間狀態擺在那裡，結構上
就是可被呼叫端影響的。而 context 是一個物件而非一組簽章，因此「多給各段一樣共享的值」是新增一個
屬性，不是簽章變更——後者會讓所有既有覆寫編譯失敗。

---

## 三種使用情境

API 方法一律具備三層：BO 方法以具體的 `XxxArgs` 為參數、回傳具體的 `XxxResult`，executor 依兩者共有的屬性，
把 API 請求複製成 args、把 result 複製成 API 回應。各情境的差別在於 BO 型別承載什麼。

### 情境一：API 方法，BO 不需額外屬性（最常見）

API 與 BO 的參數屬性完全相同：`XxxArgs` / `XxxResult` 實作與 API 型別相同的合約介面，不多加任何東西。

**需要建立的型別：** 合約介面 + API 合約型別 + 與之對應的 BO 參數型別
**BO 方法簽章：** 參數與回傳型別使用具體 `XxxArgs` / `XxxResult` 型別

```csharp
public GetOrderResult GetOrder(GetOrderArgs args)
{
    // Executor 傳入 GetOrderArgs，BO-to-BO 呼叫也直接傳入 GetOrderArgs
    return new GetOrderResult { OrderId = args.OrderId };
}
```

### 情境二：API 方法，BO 需要額外屬性

BO 間互相呼叫時需要傳遞 API 不可見的內部屬性。

**需要建立的型別：** 合約介面 + API 合約型別 + 帶額外屬性的 BO 參數型別

```csharp
// BO 參數帶有額外屬性
public sealed class GetOrderArgs : BusinessArgs, IGetOrderRequest
{
    public string OrderId { get; set; } = string.Empty;
    public bool IncludeCancelledLines { get; set; }  // BO 專用
}

// BO 方法直接以具體 args 型別為參數，額外屬性可直接取用
public GetOrderResult GetOrder(GetOrderArgs args)
{
    bool includeCancelled = args.IncludeCancelledLines;
    // ...
}
```

> **WARNING：** BO 專用屬性不在 API 型別裡，但這並不能讓它脫離遠端呼叫者的掌控。Plain 請求的 body
> 會直接綁定到方法的參數型別 —— 也就是 `XxxArgs` 本身 —— 因此它的任何 public setter 都可能被呼叫端設定。
> 請把這類屬性與其餘 args 一樣視為呼叫端輸入（見上方〈命名規則〉），只允許 BO 間呼叫設定的地方，
> 請檢查 `IsLocalCall`。

### 情境三：BO 內部方法（不公開至 API）

僅在 BO 內部使用的方法，不對外公開為 JSON-RPC API。

**需要建立的型別：** 僅 BO 參數型別（不需要合約介面，不需要 API 型別）

```csharp
public sealed class RecalcArgs : BusinessArgs
{
    public string OrderId { get; set; } = string.Empty;
    public bool ForceRecalc { get; set; }
}
```

---

## 序列化規則

| 層級 | 序列化標註 | wire 註冊 |
|------|:---:|:---:|
| 合約介面 | 無 | — |
| API 型別 | **無** | `WireContracts.*.cs`（框架 repository） |
| BO 型別 | 無 | — |

沒有任何一層帶 MessagePack 標註。XML 標註屬於會被存成檔案的定義型別，不屬於這些 wire 訊息。

---

## 用戶端開發

用戶端透過 `SystemApiConnector` 呼叫 API 時，一律使用 `Request` / `Response` 型別：

```csharp
var connector = PolhemApiClient.CreateRemote(endpoint, apiKey).System;

// 使用 API 型別，不使用 BO 型別
LoginResponse response = await connector.LoginAsync("admin", "password");
Console.WriteLine(response.AccessToken);
```

用戶端**不應引用**也**不需引用** `BusinessArgs`、`BusinessResult` 或任何 `XxxArgs` / `XxxResult` 型別。

---

## BO 開發

### 方法簽章

BO 方法的參數與回傳型別使用**具體 `XxxArgs` / `XxxResult` 型別**，BO 介面宣告（`ISystemBusinessObject`、`IFormBusinessObject`）也一樣是具體型別：

```csharp
// BO 方法（及其介面宣告）皆使用具體型別
public LoginResult Login(LoginArgs args) { ... }
```

合約介面（`ILoginRequest` / `ILoginResponse` 等）仍然存在，且由 `XxxArgs` / `XxxResult`
型別（以及 API 的 `XxxRequest` / `XxxResponse` 型別）**實作** —— 例如
`LoginArgs : BusinessArgs, ILoginRequest`。它們提供共用屬性合約與跨層獨立性，但
**不用於方法簽章**；簽章綁定具體型別。

### 回應映射

當 BO 方法回傳純 POCO（如 `LoginResult`），框架的 `ApiOutputConverter` 會透過**命名慣例**自動對應至 API 型別（`LoginResponse`），**不需要任何註冊**。

對應規則：

```
{Action}Result  ──反射搜尋 Polhem.Api.Core 組件──▶  {Action}Response
```

例如 `PingResult` 會自動對應到 `PingResponse`。查找結果以 result 型別為 key 快取，每個型別只解析一次。

> 沒有任何機制檢查這個慣例。名稱不以 `Result` 結尾、或在 `Polhem.Api.Core` 找不到 `{Action}Response` 的 result
> 型別不會被轉換：`ApiOutputConverter` 直接回傳 BO result 本身，就這樣送出。背景請參閱 [ADR-007](../../../maintainers/adr/adr-007-convention-based-type-resolution.md)。

### ExecFunc 模式

`ExecFunc` 有相同的分層（`IExecFuncRequest`、`ExecFuncRequest`、`ExecFuncArgs` 及對應的回應型別），
但唯一的強型別成員是 `FuncId`。各函式的資料放在每個 Args / Result 與 Request / Response 都繼承的
`Parameters` 集合（`ParameterCollection`）裡傳遞，因此新增函式不需要新型別。

---

## 新增 API 方法的步驟

以新增 `GetOrder` 方法為例：

1. **定義合約介面**（`src/Polhem.Api.Contracts/<Axis>/`，namespace 為 `Polhem.Api.Contracts.<Axis>`）
   - `IGetOrderRequest.cs` — 輸入屬性
   - `IGetOrderResponse.cs` — 輸出屬性

2. **建立 API 合約型別**（`src/Polhem.Api.Core/Messages/<Axis>/`；namespace 為 `Polhem.Api.Core.Messages.<Axis>`）
   - `GetOrderRequest.cs` — 繼承 `ApiRequest`，實作 `IGetOrderRequest`；不帶標註
   - `GetOrderResponse.cs` — 繼承 `ApiResponse`，實作 `IGetOrderResponse`；不帶標註
   - 兩者都要到 `src/Polhem.Api.Core/MessagePack/WireContracts.*.cs` 註冊 —— 漏了
     `WireContractDriftTests` 會失敗

3. **實作 BO 方法**
   - 方法簽章使用具體 `GetOrderArgs` / `GetOrderResult` 型別
   - 必須遵守 `{Action}Args` / `{Action}Result` 命名慣例，`ApiOutputConverter` 才能自動將 `GetOrderResult` 對應至 `GetOrderResponse`
   - args / result 型別實作對應合約介面（`GetOrderArgs : BusinessArgs, IGetOrderRequest` 等），提供跨層屬性共用

4. **更新用戶端 Connector**（若需要）
   - 在 Connector 中新增對應方法，使用 `GetOrderRequest` / `GetOrderResponse`

> 除了步驟 2 的 wire 註冊之外，不需要註冊任何東西：回應映射由命名慣例推導（詳見 [ADR-007](../../../maintainers/adr/adr-007-convention-based-type-resolution.md)）。

---

## 組件相依方向

```
Polhem.Api.Contracts           ← 合約介面（API 與 BO 共用）
    │
    ├── Polhem.Api.Core        ← API 型別（含序列化）
    │       │
    │       └── Polhem.Api.Client  ← 用戶端（只用 Request / Response）
    │
    └── Polhem.Business        ← BO 型別（純 POCO）+ BO 介面
```

**原則**：箭頭方向為相依方向。`Polhem.Api.Core` 與 `Polhem.Business` 彼此不相依，兩者共用的合約放在 `Polhem.Api.Contracts`。兩者也各有其他相依（例如 `Polhem.Definition`）；完整的相依圖見 [專案相依性全景圖](../architecture/dependency-map.md)。
