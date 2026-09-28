# Polhem.Api.AspNetCore

> 提供統一 JSON-RPC 2.0 API 端點的 ASP.NET Core 控制器程式庫。

[English](README.md)

## 架構定位

- **層級**：API 層（裝載）
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。它們確實漂了：`Polhem.Hosting` 抽出後，有四份 README 的下游數個月都沒把它補上。
- 由應用程式消費：使用者繼承控制器。

## 目標框架

- `net10.0` -- ASP.NET Core 裝載需要現代執行階段

## 使用方式

以 `AddPolhemFramework`（`Polhem.Hosting`，見其 README）註冊框架、衍生一個控制器，並在應用程式建置完成後呼叫
`UsePolhemFramework`：

```csharp
// Controllers/ApiController.cs
using Polhem.Api.AspNetCore.Controllers;

public sealed class ApiController : ApiServiceController { }
```

```csharp
// Program.cs，在 builder.Services.AddPolhemFramework(...) 與 builder.Services.AddControllers() 之後
var app = builder.Build();
app.UsePolhemFramework();   // using Polhem.Api.AspNetCore;
app.MapControllers();
app.Run();
```

`UsePolhemFramework` 執行 host 端的啟動檢查。目前它在尚未核發任何 API 金鑰時記錄一則警告，因為在那之前
`X-Api-Key` 標頭只檢查是否存在。

## 主要功能

### 單一 POST 端點

- 公開單一 `POST /api` 路由，標記 `[ApiController]` 與 `[Produces("application/json")]`。
- 處理前驗證 `Content-Type: application/json`，其他媒體類型回傳 `415 Unsupported Media Type`。

### 非同步請求管線

- `PostAsync` 協調生命週期：讀取請求、驗證授權、執行處理。
- 每個階段皆為 `protected virtual` 方法，子類別可獨立覆寫。

### JSON-RPC 請求解析

- `ReadRequestAsync` 讀取原始 body，反序列化為 `JsonRpcRequest`，並驗證 `Method` 欄位。
- 針對空 body、缺少 method 與格式錯誤的 JSON，回傳結構化的 `JsonRpcException` 錯誤。

### 授權驗證

- `ValidateAuthorization` 接收透過 `[FromHeader]` 綁定的 `X-Api-Key` 與 `Authorization` 標頭值，
  以 `ValidateApiKey`（已註冊的 `IApiKeyValidator`）檢查 API 金鑰，其餘委派給
  `ApiServiceOptions.AuthorizationValidator`。
- 驗證失敗時回傳 `401 Unauthorized` 與 JSON-RPC 錯誤。

### 請求執行

- `HandleRequestAsync` 以驗證後的存取權杖與請求的取消權杖，把請求交給 `JsonRpcExecutor`，並以
  `application/json` 回傳結果。
- 用戶端已放棄的請求回應狀態碼 499。從執行器逸出的例外回傳 `500 Internal Server Error`，
  Development 環境附根例外訊息，其他環境訊息為空。

### 結構化錯誤回應

- `CreateErrorResponse` 產生一致的 `JsonRpcResponse`，包含錯誤碼、訊息與選用資料，並對應適當的 HTTP 狀態碼。

## 擴充點

`PostAsync` 是公開的 action；其餘成員為 `protected`，在衍生控制器中使用。

| 類別 / 成員 | 用途 |
|-------------|------|
| `ApiServiceController` | 抽象基底控制器；繼承後在 ASP.NET Core 應用程式中註冊 |
| `PostAsync` | 所有 JSON-RPC 請求的進入點（`POST /api`） |
| `ReadRequestAsync`（virtual） | 解析並驗證 JSON-RPC 請求 body |
| `ValidateAuthorization`（virtual） | 檢查 API 金鑰與 Bearer Token |
| `ValidateApiKey`（virtual） | 透過已註冊的 `IApiKeyValidator` 檢查 API 金鑰 |
| `ApiKeyValidation` | 本次請求的 API 金鑰判定結果，由 `ValidateAuthorization` 設定 |
| `HandleRequestAsync`（virtual） | 將請求分派至 `JsonRpcExecutor` |
| `CreateErrorResponse`（virtual） | 建立標準化的 JSON-RPC 錯誤回應 |
| `IsDevelopment` | 裝載環境是否為 Development（無法解析時為 `false`） |
| `PolhemFrameworkApplicationBuilderExtensions.UsePolhemFramework` | Host 端啟動檢查 |

## 設計慣例

- **樣板方法模式（Template Method Pattern）** -- `PostAsync` 定義管線骨架；`ReadRequestAsync`、`ValidateAuthorization`、`ValidateApiKey`、`HandleRequestAsync` 與 `CreateErrorResponse` 皆為 `virtual`，可選擇性覆寫。
- **開發與正式環境錯誤訊息區分** -- 只有 `IsDevelopment` 為 `true` 時才附上例外細節。
- **不直接依賴 DI 容器** -- 服務從 `HttpContext.RequestServices` 解析，控制器可在任何 ASP.NET Core 主機中運作，無需額外設定。
- **外部相依**：`FrameworkReference: Microsoft.AspNetCore.App`。

## 目錄結構

- `Controllers/ApiServiceController.cs` -- 抽象基底控制器
- `PolhemFrameworkApplicationBuilderExtensions.cs` -- `UsePolhemFramework`
