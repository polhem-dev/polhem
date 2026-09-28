# Polhem.Web.Blazor.Server

> Polhem 的 Blazor Server 元件庫 —— 以 FormSchema 驅動、在 ASP.NET Core 宿主行程內執行的 UI 元件。

[English](README.md)

## 架構定位

- **層級**：Web 前端（Razor Class Library）
- **Hosting 模式**：Blazor Server —— 元件邏輯在 ASP.NET Core server 端執行；瀏覽器透過 SignalR 接收 DOM 差異。
- **Provider 綁定**：以 `AddPolhemBlazor` 選擇（見下方）——經 `LocalApiProvider` 在行程內呼叫，或經
  `RemoteApiProvider` 走 HTTP，兩者皆來自 `Polhem.Api.Client`。
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。它們確實漂了：`Polhem.Hosting` 抽出後，有四份 README 的下游數個月都沒把它補上。
- 由 ASP.NET Core 宿主應用程式消費。

## 目標框架

- `net10.0`

## 註冊

```csharp
using Polhem.Web.Blazor.Server.DependencyInjection;

// Remote：元件透過 HTTP 呼叫 Polhem API 伺服器，每次呼叫都與其他 API 用戶端一樣受檢查。
// 此宿主不需要 AddPolhemFramework。
builder.Services.AddPolhemBlazor(options => options.UseRemoteProvider("https://api.example.com/api"));

// Local（預設）：宿主本身就是後端。先在同一個 service collection 以 AddPolhemFramework
// （Polhem.Hosting）註冊後端，再：
// builder.Services.AddPolhemBlazor(options => options.UseLocalProvider());
```

`AddPolhemBlazor` 註冊 `PolhemBlazorOptions`、每個 circuit 一個的 `ApiSessionContext`、元件用來建立連接器的
`PolhemApiConnectorFactory`，以及元件自身文字的 localizer。它不會呼叫 `AddPolhemFramework`。

> **Local 模式只適用於受信任的使用者。** 它發出的每次呼叫都是行程內呼叫，不論是哪位瀏覽器使用者觸發，
> 後端都視為受信任。只有當網站的每位使用者都被信任可使用整個後端時（例如內部管理工具）才使用它；
> 否則請用 Remote 模式。本機呼叫可以略過哪些檢查，列在 `PolhemBlazorProviderMode.Local` 的 XML 文件中。

## 元件

- `FormPage` -- 單一程式的清單加主檔／明細編輯，透過共用的 `FormDataObject` 串接。
- `DynamicGrid` -- 以 `LayoutGrid` 呈現的純顯示清單；點選資料列時以列 id 觸發 `OnRowSelected`。
- `DynamicForm` -- 渲染 `FormLayout` 的主檔 section，依欄位的 `ControlType` 選擇輸入元素（text、date、month、
  time、checkbox、textarea、dropdown）。
- `PolhemLoginPanel` -- 精簡的登入表單；`OnLoggedIn` 收到 `LoginResponse`。
- `PolhemAccessTokenProvider` -- 持有 circuit 的存取權杖，並以 cascading value 提供給子元件。
- `FormDataObject` -- 由 `FormSchema` 衍生記憶體內 `DataSet`（主檔列 + 明細資料表），提供 `GetField` /
  `SetField` 雙向綁定，並透過連接器執行 `LoadAsync` / `SaveAsync` / `DeleteAsync` / `NewAsync`。

## 相依限制

引用 `Polhem.Api.Client`（以及 ASP.NET Core）。後端服務由宿主註冊：Local 模式以 `AddPolhemFramework`，
Remote 模式則由遠端伺服器提供。

## 授權

MIT
