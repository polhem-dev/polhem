# Polhem.UI.Avalonia.Shell

> 給 Polhem 後端用的現成 Avalonia 前端：連線、登入公司，並開啟伺服器選單上的每一張表單。桌面、瀏覽器
> （WebAssembly）、iOS 與 Android 各端共用同一個套件。

[English](README.md)

## 架構定位

- **層級**：UI 層，位在端（head）的層級（與應用程式自己的 UI 專案並列）
- **在相依圖中的位置**：見 [專案相依關係圖](../../docs/zh-TW/architecture/dependency-map.md)。
- 引用 [`Polhem.UI.Avalonia`](../Polhem.UI.Avalonia/README.zh-TW.md) 取得表單控制項，引用
  [`Polhem.UI.ApiBridge`](../Polhem.UI.ApiBridge/README.zh-TW.md) 處理連線與登入。`Polhem.UI.Avalonia` 不得引用
  bridge（[ADR-054](../../maintainers/adr/adr-054-front-end-decoupled-from-api-client.md)），所以要連後端的前端位在
  更上一層，本套件就是這一層。
- **只適用單一使用者的端**，與它所依賴的 bridge 相同。

## 目標框架

- `net10.0`

## 功能

| 步驟 | 畫面 |
|------|------|
| 連線 | 服務端點與 API 金鑰，先 ping 過才往下走。兩者都會記住。 |
| 登入 | 公司代號、帳號、密碼。先 `Login`，再 `EnterCompany`。公司代號預設為這台裝置上次進入的公司。 |
| 表單 | 選單由伺服器的 `MenuSettings` 產生；每個項目以分頁開啟表單，先顯示 `ListView`，選取記錄後換成 `FormView`。**登出**回到登入畫面；再次登入會重新取得所有定義，所以程式執行中改了定義，不必重新啟動就看得到。 |

登入前的介面語言跟隨作業系統（英文或繁體中文；其他語言退回英文），可在連線與登入畫面切換，選擇會被記住。
命令列加上 `Language=en` 或 `Language=zh-TW` 可指定單次執行的語言，不會改動記住的選擇；smoke test 這類自動化執行就是用它。
登入後改由帳號的 culture 決定：`ClientInfo.ApplyLoginResult` 會把它設為整個 process 的 culture。

殼會以 DefineEditor 的配色套用 Semi 主題（`Polhem.UI.Avalonia` 的 `VisualStudioTheme`；預設淺色，表單畫面有深淺切換），並把 API bridge 註冊為 `UIServices.Provider`。殼不加入字型，
由各端自行選擇。

## 用法

繼承 `ShellApplication` 並提供選項：

```csharp
using Polhem.UI.Avalonia.Shell;

public class MyApp : ShellApplication
{
    protected override ShellOptions CreateOptions() => new()
    {
        AppTitle = "My App",
        DefaultEndpoint = "http://localhost:5000/api",
        DefaultApiKey = "my-app-key",
        DefaultCompanyId = "ACME",
    };
}
```

各端像啟動一般 Avalonia 應用程式一樣啟動它：

```csharp
// 桌面
AppBuilder.Configure<MyApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime(args);

// iOS / Android
public partial class AppDelegate : AvaloniaAppDelegate<MyApp> { }
```

每個選項的說明在 `ShellOptions` 的 XML 文件。各端不同的設定透過 `ShellApplication.ConfigureOptions` 調整。
瀏覽器端沒有可持久保存的檔案系統，因此以 `BrowserLocalStorage` 把端點、API 金鑰與殼的偏好設定存在 `localStorage`；
字型畫不出中日韓文字時，也要關掉語言切換：

```csharp
var storage = new BrowserLocalStorage("MyApp");
ClientInfo.EndpointStorage = storage;
ClientInfo.ApiKeyStorage = storage;

AppBuilder.Configure(() => new MyApp
{
    ConfigureOptions = options =>
    {
        options.PreferenceStorage = storage;
        options.AllowLanguageSwitch = false;
    },
});
```

預設的偏好設定儲存 `FileShellPreferenceStorage` 寫在 bridge 的 `FileEndpointStorage` 所用的同一個每使用者資料夾。

## macOS 應用程式套件（.app）

本套件附一個 MSBuild target，可把發佈好的 macOS 端包成 `.app`，讓它能從 Finder 與 Dock 開啟，其他工具也能以
bundle identifier 指定它：

```xml
<PropertyGroup>
  <PolhemBundleIdentifier>tw.example.myapp</PolhemBundleIdentifier>
</PropertyGroup>
```

```bash
dotnet publish MyApp.Desktop.csproj -c Release -r osx-arm64 -p:PolhemMacAppBundle=true
```

產出位置是 `<publish 資料夾>/<名稱>.app`。加上 `--self-contained` 可把 .NET runtime 一起包進去。其他屬性
（`PolhemBundleName`、`PolhemBundleIcon`）的說明在 `buildTransitive/Polhem.UI.Avalonia.Shell.targets` 的檔頭。
產出的套件沒有簽章。

以 NuGet 套件引用時會自動匯入這個 target。本 repo 內以專案引用殼的專案要自行匯入該檔，做法見
`apps/Polhem.Northwind/Polhem.Northwind.Desktop`。

## 範例

[`apps/Polhem.Northwind`](../../apps/Polhem.Northwind/README.zh-TW.md) 就跑在這個殼上：它的 UI 專案只有一個類別
`NorthwindApp`，負責提供選項。
