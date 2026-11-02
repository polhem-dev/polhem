# Polhem.Web.Blazor.Wasm

> Polhem 的 Blazor WebAssembly 宿主：遠端 client 與外殼的語系切換。控制項來自 UI 套件。

[English](README.md)

## 架構定位

- **層級**：Web 前端，宿主套件
- **Hosting 模式**：Blazor WebAssembly。元件在瀏覽器中執行，透過 HTTP 呼叫 Polhem API 伺服器。
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/architecture/dependency-map.md)。
- 應用程式將它與 UI 套件（例如 [`Polhem.Web.Blazor.Mud`](../Polhem.Web.Blazor.Mud/README.zh-TW.md)）一起參考。

## 目標框架

- `net10.0`，由 `Microsoft.NET.Sdk.BlazorWebAssembly` 專案使用

## 註冊

```csharp
using Polhem.Web.Blazor.Mud.DependencyInjection;
using Polhem.Web.Blazor.Wasm.DependencyInjection;
using Polhem.Web.Blazor.Wasm.Shell;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Routes>("#app");

builder.Services.AddPolhemBlazor(options =>
{
    options.Endpoint = "https://api.example.com/api";
    options.ApiKey = builder.Configuration["Polhem:ApiKey"] ?? string.Empty;
});
builder.Services.AddPolhemShell(shell => shell.AppTitle = "My application");
builder.Services.AddPolhemMud();

var host = builder.Build();
host.Services.ApplyPolhemShellCulture();
await host.RunAsync();
```

`ApplyPolhemShellCulture` 依序採用登入畫面選擇的語言（存在瀏覽器的 `localStorage`）、外殼有提供的瀏覽器語言、預設語言。請在 `RunAsync` 之前呼叫。

## 專案設定

```xml
<JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>
<BlazorWebAssemblyLoadAllGlobalizationData>true</BlazorWebAssemblyLoadAllGlobalizationData>
<InvariantGlobalization>false</InvariantGlobalization>
<InvariantTimezone>false</InvariantTimezone>
```

- 裁剪後的 WebAssembly 發佈會關閉 System.Text.Json 的反射，而 JSON-RPC 信封需要它。關閉時 `POLHEM9004` 會警告。
- 這類專案的 SDK 預設是 `TrimMode=partial`，Polhem 支援此模式。不要設定 `TrimMode=full` 或 `PublishAot`；見[平台支援](../../docs/zh-TW/getting-started/platform-support.md)。
- 框架透過 `TimeZoneInfo` 轉換時間，因此必須帶上時區資料。

## 與 Blazor Server 的差異

- **只有 Remote。** 後端無法在瀏覽器中執行，所以沒有 `UseLocalProvider`。
- **API key 是公開的。** 能開啟頁面的人都讀得到 bundle 的所有內容。這個 key 只識別應用程式，不保護任何東西。
- **框架不會加密呼叫。** 瀏覽器執行環境沒有 RSA，client 會略過金鑰交換並送出 `Encoded` 酬載。登入密碼只靠 HTTPS 保護，所以網站與 API 都要走 HTTPS。
- **要求 `ApiProtectionLevel.Encrypted` 的方法會被拒絕**，例如稽核記錄與 API key 管理。`ApiAccessValidator` 不接受以 `Encoded` 呼叫這些方法。
- **其他來源的伺服器必須以 CORS 允許此頁面。** 從 API 的同一個來源提供 bundle 就不需要設定。
- **靜態主機必須對未知路徑回傳 `index.html`**，否則在表單頁重新整理會得到 404。
- 重新整理會登出，與 Blazor Server 的新 circuit 相同。存取權杖只放在記憶體，刻意不寫入瀏覽器儲存區。
