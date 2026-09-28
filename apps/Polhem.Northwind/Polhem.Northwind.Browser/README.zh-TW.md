# Polhem.Northwind.Browser

[English](README.md) | **繁體中文**

[Polhem.Northwind](../README.zh-TW.md) 示範的**網頁（WASM）head**。它以 **Avalonia Browser** 後端把共用的
`Polhem.Northwind.UI` Avalonia 應用程式編譯成 WebAssembly，並在瀏覽器中執行 —— 與
[`Polhem.Northwind.Desktop`](../Polhem.Northwind.Desktop) 相同的 `App`、view model 與 view，只是換了一個平台 head
（以 `.UseBrowser()` 取代 `.UseDesktop()`）。它是一個輕薄的 JSON-RPC 用戶端；後端是未經修改的
[`Polhem.Northwind.Server`](../Polhem.Northwind.Server)。

## 前置需求

.NET 10 SDK，加上 **WebAssembly tools** workload（只需安裝一次）：

```bash
sudo dotnet workload install wasm-tools
```

## 執行（開發）

從 repository 根目錄開兩個終端機：

```bash
# 1. 後端（JSON-RPC，http://localhost:5100）。僅限開發環境的 CORS 讓 WASM
#    dev server 能跨來源呼叫它。
dotnet run --project apps/Polhem.Northwind/Polhem.Northwind.Server

# 2. 網頁用戶端 dev server（Avalonia WASM，http://localhost:5200）
dotnet run --project apps/Polhem.Northwind/Polhem.Northwind.Browser
```

開啟 <http://localhost:5200/>，接著 **Connect**（endpoint 已預填
`http://localhost:5100/api`）→ 以 `demo` / `demo` **Sign in**。

由於 dev server（`5200`）與 API（`5100`）是不同的來源（origin），server 會啟用一個僅限開發環境的
CORS 政策（`PolhemDevWasm`，由 `IsDevelopment()` 把關），允許任何 `localhost` 來源。正式部署應該從 API
主機以**同源（same-origin）**方式提供發佈後的 WASM，並拿掉該政策。

## WASM 專屬接線

單執行緒的 `browser-wasm` 迫使我們做出幾個桌面 head 不需要的選擇；每一項都在其原始碼處附有註解：

| 關注點 | 原因 | 位置 |
|--------|------|------|
| 以 `localStorage` 持久化 endpoint 與 API 金鑰 | 預設的 `FileEndpointStorage` 會寫檔，而瀏覽器的檔案系統在記憶體中，重新載入後值就不見了 | `Storage/BrowserLocalStorageEndpointStorage.cs`，於 `Program.cs` 設定 |
| `JsonSerializerIsReflectionEnabledByDefault=true` | browser-wasm 預設停用 System.Text.Json 的反射；Polhem 的 `JsonCodec` 以反射為基礎 | `Polhem.Northwind.Browser.csproj` |
| 非同步連線／定義載入 | sync-over-async 在單一執行緒上會擲出 *"Cannot wait on monitors"* —— 改用 `ClientInfo.InitializeAsync` / `ClientInfo.DefineAccess.GetMenuSettingsAsync()` | `ConnectionViewModel`、`FormsViewModel` |
| UI 語系起始為 `en-US` | 內嵌字型 Inter 沒有 CJK 字形，而瀏覽器沒有系統字型可借用。登入後會像其他 head 一樣套用帳號的語系，所以在這裡請用語系為 `en-US` 的帳號 | `Program.cs` |
| 以 overlay 對話框取代 `Window` | 沒有原生視窗 —— lookup／列編輯對話框渲染在 `OverlayLayer` 上 | `Polhem.UI.Avalonia` `OverlayDialogHost` |

## Release／發佈

```bash
dotnet publish apps/Polhem.Northwind/Polhem.Northwind.Browser -c Release -o <out>
```

專案設定了 `<PublishTrimmed>false</PublishTrimmed>`：Polhem 以反射存取定義與訊息型別（JSON-RPC
envelope 用 System.Text.Json、定義用 XmlSerializer，另有 `TypeDescriptor`、`Assembly.GetType`），沒有一處是
source-generated，因此 IL trimming 既會分析失敗（在 `TreatWarningsAsErrors` 下的 `IL2026`），也會砍掉反射路徑
在執行階段需要的 metadata。MessagePack 是例外：每個 wire 型別都有手寫註冊的 formatter。停用 trimming
是以 bundle 大小（gzip 後約 16 MB）換取正確性。框架沒有提供 source-generated 序列化器，而 trim-safe 的
bundle 需要它。

發佈後的 `wwwroot/` 是一份靜態 bundle；可從任何靜態主機提供（或者，若採同源部署，由
`Polhem.Northwind.Server` 提供）。
