# Polhem.Web.Blazor.Server

> Polhem 的 Blazor Server 元件庫 —— 以 FormSchema 驅動、在 ASP.NET Core 宿主行程內執行的 UI 元件。

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
circuit 在自己的 client 上登入，所以一位使用者的身分不會進到另一位使用者的呼叫），以及元件自身文字的 localizer。它不會呼叫 `AddPolhemFramework`。
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

## 元件

- `FormPage` -- 單一程式的清單加主檔記錄編輯，透過共用的 `FormDataObject` 串接。預設經由 `FormDefinitionLoader`
  載入定義，因此標題依 circuit 的 UI culture 呈現，租戶客製的版面也會套用；`DefinitionLoader` 參數可覆寫單一頁面的
  載入器。儲存前會檢查標記為 `Required` 的欄位，有空白時列出這些欄位，不送出儲存。明細資料表不會呈現。
- `DynamicGrid` -- 以 `LayoutGrid` 呈現的純顯示清單；點選資料列時以列 id 觸發 `OnRowSelected`。
- `FormLayoutPresenter` -- 渲染 `FormLayout` 的主檔 section，每個欄位一個欄位編輯器，由 `FieldEditorFactory` 依
  `ControlType` 選擇。`FormMode` 參數決定哪些編輯器可輸入，並依各欄位的 `ReadOnly` 與 `AllowEditModes`。
  不呈現明細 grid。
- 欄位編輯器（`Polhem.Web.Blazor.Server.Controls.Editors`）-- `TextEdit`、`MemoEdit`、`NumericEdit`、`TimeEdit`、
  `DateTimeEdit`、`DateEdit`、`YearMonthEdit`、`CheckEdit`、`DropDownEdit`，名稱、契約（`IFieldEditor`）與行為
  都和 `Polhem.UI.Avalonia` 的欄位編輯器相同。編輯器可透過 `DataObject` 參數、外層的 `FormScope` 或明確呼叫
  `Bind` 綁定 `FormDataObject`；它自行套用 schema 欄位的中繼資料（例如 `MaxLength`、`ListItems`），其他來源寫入
  該欄位時會自動更新。新的編輯器繼承 `FieldEditorBase`。標記上帶有 `polhem-field-editor` class、各編輯器的
  修飾 class（例如 `polhem-field-editor--numeric`），不可輸入時再加上 `polhem-field-editor--readonly`；
  套件本身不附 CSS。
- `PolhemLoginPanel` -- 精簡的登入表單；`OnLoggedIn` 收到 `LoginResponse`。
- `PolhemAccessTokenProvider` -- 持有頁面的登入狀態（存取權杖），並以 cascading value 提供給子元件。實際的呼叫
  使用 circuit 的 `PolhemApiClient`，登入時已在其上完成登入。
- `FormDataObject` -- 由 `FormSchema` 衍生記憶體內 `DataSet`（主檔列 + 明細資料表），提供 `GetField` /
  `SetField`（主檔列或指定的資料列）雙向綁定，值變更時觸發 `FieldValueChanged` 與 `DataSetReplaced`，並透過連接器執行 `LoadAsync` / `SaveAsync` / `DeleteAsync` / `NewAsync`。

## 相依限制

引用 `Polhem.Api.Client`、[`Polhem.UI.Core`](../Polhem.UI.Core/README.zh-TW.md) 與 ASP.NET Core。後端服務由宿主註冊：
Local 模式以 `AddPolhemFramework`，Remote 模式則由遠端伺服器提供。

`Polhem.UI.Core` 提供各 head 共用的表單值規則（`FormValueBinding`、`FormDataGuard`、元素能力解析器）。它的行程層級
部分不可使用：本 head 在同一行程服務多位使用者，因此不得使用 `UIServices`，也不得參考 `Polhem.UI.ApiBridge`，
兩者都在每個行程只持有一位使用者。`UIServicesUsageGateTests`（`tests/Polhem.Web.Blazor.Server.UnitTests`）會讀取
編譯後組件的參考，違反時測試失敗。見 [ADR-054](../../maintainers/adr/adr-054-front-end-decoupled-from-api-client.md)。

## 授權

MIT
