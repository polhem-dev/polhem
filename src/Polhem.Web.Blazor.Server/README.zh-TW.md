# Polhem.Web.Blazor.Server

> Polhem 的 Blazor Server 宿主：Local 或 Remote 模式的 circuit client，以及外殼的註冊與語系端點。控制項來自 UI 套件。

[English](README.md)

## 架構定位

- **層級**：Web 前端（Razor Class Library）
- **Hosting 模式**：Blazor Server —— 元件邏輯在 ASP.NET Core server 端執行；瀏覽器透過 SignalR 接收 DOM 差異。
- **Provider 綁定**：以 `AddPolhemBlazor` 選擇（見下方）——每個 circuit 取得一個來自 `Polhem.Api.Client` 的
  `PolhemApiClient`，在行程內（`CreateLocal`）或經 HTTP（`CreateRemote`）呼叫後端。
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/architecture/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。它們確實漂了：`Polhem.Hosting` 抽出後，有四份 README 的下游數個月都沒把它補上。
- 由 ASP.NET Core 宿主應用程式消費。

## 目標框架

- `net10.0`

## 註冊

```csharp
using Polhem.Web.Blazor.Server.DependencyInjection;

// Remote：元件透過 HTTP 呼叫 Polhem API 伺服器，每次呼叫都與其他 API 用戶端一樣受檢查。
// 此宿主不需要 AddPolhemFramework。傳入核發給此應用程式的 API key：每個 circuit 的 client 都會送出它。
builder.Services.AddPolhemBlazor(options => options.UseRemoteProvider(
    "https://api.example.com/api", builder.Configuration["Polhem:ApiKey"] ?? string.Empty));

// Local（預設）：宿主本身就是後端。先在同一個 service collection 以 AddPolhemFramework
// （Polhem.Hosting）註冊後端，再：
// builder.Services.AddPolhemBlazor(options => options.UseLocalProvider());
```

`AddPolhemBlazor` 註冊 `PolhemBlazorOptions`、每個 circuit 一個、供元件注入以呼叫後端的 `PolhemApiClient`（每個
circuit 在自己的 client 上登入，所以一位使用者的身分不會進到另一位使用者的呼叫）、每個 circuit 一個
`BlazorUserContext`（同時註冊為該 circuit 的 `IUserContext`），以及元件自身文字的 localizer。它不會呼叫 `AddPolhemFramework`。
`options.UseDefinitionLoader`（預設開啟）決定 `FormPage` 是否在地化定義，見下文。

> **Remote 模式需要應用程式的 API key。** 伺服器預設的 `ApiAuthorizationValidator` 在 `X-Api-Key` 標頭缺少或不是
> 已啟用的 key 時，會拒絕 `System.Ping` 以外的每個方法，該請求以 `401 Unauthorized` 回應。key 是 `UseRemoteProvider` 的第二個參數：
> 它識別的是呼叫端應用程式而不是使用者，所以每個 circuit 送出的都是同一個。
> key 為空字串時，第一個呼叫（通常是登入）會失敗，訊息為「Response status code does not indicate success: 401
> (Unauthorized).」。伺服器尚未核發任何 key 時，任何非空值都能通過；一旦有已啟用的 key，就只有已啟用的 key 能通過。
> 範例中的設定鍵只是示意：請從宿主存放機密的地方讀取這個值。

> **Local 模式只適用於受信任的使用者。** 它發出的每次呼叫都是行程內呼叫，不論是哪位瀏覽器使用者觸發，
> 後端都視為受信任。只有當網站的每位使用者都被信任可使用整個後端時（例如內部管理工具）才使用它；
> 否則請用 Remote 模式。本機呼叫可以略過哪些檢查，列在 `PolhemBlazorProviderMode.Local` 的 XML 文件中。

## 本套件提供的內容

- `PolhemAccessTokenProvider` -- 持有頁面的登入狀態（存取權杖），並以 cascading value 提供給子元件。實際的呼叫
  使用 circuit 的 `PolhemApiClient`，登入時已在其上完成登入。

控制項來自 UI 套件，例如 [`Polhem.Web.Blazor.Mud`](../Polhem.Web.Blazor.Mud/README.zh-TW.md)；狀態來自共用的 [`Polhem.Web.Blazor`](../Polhem.Web.Blazor/README.zh-TW.md)。應用程式參考一個 UI 套件和這個宿主。

## 外殼（Shell）

外殼是網站現成的前端，對應 Avalonia 的
[`Polhem.UI.Avalonia.Shell`](../Polhem.UI.Avalonia.Shell/README.zh-TW.md)：先是登入並進入公司的登入畫面，接著是頂端列
（標題、使用者與公司、登出）、伺服器的選單與路由到的頁面。網站只要提供標題與登入畫面的預設值，自己只留下自己的頁面。
版面、選單與登入元件來自 UI 套件（`Polhem.Web.Blazor.Mud.Shell`）；`PolhemShellHome` 與 `PolhemShellOptions` 在共用的 `Polhem.Web.Blazor.Shell`；註冊與端點在本套件。

| 部分 | 作用 |
|------|------|
| `PolhemShellLayout` | 路由器的預設版面。circuit 的使用者進入公司之前顯示 `PolhemShellSignIn`，之後顯示頂端列、`PolhemShellNavMenu` 與頁面。選單按鈕在寬螢幕上收合選單；寬度小於 600 CSS 像素（表單頁預設的精簡門檻）時改為從側邊滑出蓋在頁面上，選了表單就關閉。 |
| `PolhemShellSignIn` | 標題、提示、公司與 `PolhemLoginPanel`；登入後經由 `BlazorUserContext.EnterCompanyAsync` 進入公司。被拒絕時把錯誤顯示在畫面上，並讓 circuit 再次登出。提供設定的語言之間的切換，並在下方列出 `PolhemShellOptions.SignInLinks`，指向網站本身基底位址的連結不顯示。 |
| `PolhemShellNavMenu` | 把伺服器的 `MenuSettings` 呈現為資料夾標題與連到 `{FormRoute}/{ProgId}` 的連結，標題依 circuit 的 UI culture 呈現。 |
| `PolhemShellHome` | 開啟選單的第一個表單，與 Avalonia 外殼登入後的行為相同；選單中還沒有表單時則說明這一點。放在根頁面上。 |
| `AddPolhemShell` | 註冊 `PolhemShellOptions`。每個選項都寫在它的 XML 文件中。 |
| `UsePolhemShellLocalization` | 以設定的語言設定 request localization，讓 circuit 以瀏覽者選的語言開始。 |
| `MapPolhemShellCulture` | `GET /culture`，語言切換背後的端點。它把提供的 culture 存進 request-localization cookie 後轉址回原頁，且只轉到本站的路徑；其他位址一律回到根目錄，因此這個端點不會成為 open redirect（`PolhemShellCultureEndpointTests`）。兩者都位於請求的 path base 底下，所以以 `app.Map("/sub", ...)` 掛載的應用程式在自己的分支對應這個端點，切換後會回到自己的頁面。 |

```csharp
using Polhem.Web.Blazor.Mud.DependencyInjection;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Polhem.Web.Blazor.Server.Shell;

builder.Services.AddPolhemBlazor(options => options.UseLocalProvider());
builder.Services.AddPolhemShell(shell =>
{
    shell.AppTitle = "My App";
    shell.DefaultCompanyId = "ACME";
});
builder.Services.AddPolhemMud();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.UsePolhemShellLocalization();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapPolhemShellCulture();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
```

應用程式保留自己的 `App.razor`（在其中連結 UI 套件的資源，見該套件的 README）、以 `DefaultLayout="typeof(PolhemShellLayout)"` 設定的路由器，
以及兩個頁面：

```razor
@* Pages/Home.razor *@
@page "/"
<PolhemShellHome />

@* Pages/FormHost.razor：路由是 "/{FormRoute}/{ProgId}"，預設為 "form"。 *@
@page "/form/{ProgId}"
<FormPage ProgId="@ProgId" />
@code {
    [Parameter] public string ProgId { get; set; } = string.Empty;
}
```

登入屬於 circuit，所以重新整理或開新分頁會再次顯示登入畫面。circuit 的 culture 在連線時就固定了，因此語言要在登入畫面、
任何人登入之前選擇：切換會經過 `/culture` 並重新載入頁面。之後不會再套用帳號本身的 culture。外殼自己的文字有英文與
繁體中文；元件與定義依各自的語系資源呈現。

外殼使用 host 選定的 provider，因此上方 Local 模式的警告同樣適用。

## 相依限制

引用 [`Polhem.Web.Blazor`](../Polhem.Web.Blazor/README.zh-TW.md)（由它帶入 API client 與共用規則）以及 ASP.NET Core
共用框架（`Microsoft.AspNetCore.App`）。後端服務由宿主註冊：Local 模式以 `AddPolhemFramework`，Remote 模式則由遠端伺服器提供。
讓 `UIServices` 與 `Polhem.UI.ApiBridge` 不進入任何 Blazor 套件的規則，說明在 `Polhem.Web.Blazor` 的 README。

## 授權

MIT
