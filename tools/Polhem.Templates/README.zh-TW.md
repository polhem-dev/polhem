# Polhem.Templates

[English](README.md) | **繁體中文**

[Polhem](https://github.com/polhem-dev/polhem) 的 `dotnet new` 範本。範本以所建立應用程式的前端技術命名
（`polhem-<前端>`）；資料庫是範本的選項。

```bash
dotnet new install Polhem.Templates
dotnet new polhem-avalonia -n Acme.Erp --database SqlServer
```

在安裝了 C# Dev Kit 的 VS Code 中，範本會出現在「New .NET Project」的清單，名稱是 **Polhem App (Avalonia)**。

## `polhem-avalonia`

建立以 Avalonia 桌面程式為前端的應用程式，也可另加瀏覽器、iOS 與 Android 前端。Server 已引用 Polhem 套件與所選資料庫的驅動：

| 路徑 | 內容 |
|------|------|
| `src/<名稱>.Server` | 提供 JSON-RPC API 的 ASP.NET Core host，附 `Polhem.Hosting` 的 `db create`、`db check`、`db upgrade`、`db seed-admin` 指令（`dotnet run -- db ...`） |
| `src/<名稱>.UI` | 各用戶端共用的前端：[Polhem.UI.Avalonia.Shell](../../src/Polhem.UI.Avalonia.Shell/README.zh-TW.md)，預設連到 Server 的端點並進入第一家公司 |
| `src/<名稱>.Desktop` | Avalonia 桌面用戶端；`dotnet publish` 加 `-p:PolhemMacAppBundle=true` 可產生 macOS `.app` |
| `src/<名稱>.Browser`、`.iOS`、`.Android` | 加 `--browser`、`--ios`、`--android` 時產生的瀏覽器（WebAssembly）、iOS 與 Android 用戶端。Android 用戶端以 `10.0.2.2`（模擬器存取執行它的主機的位址）連到 Server。加 `--browser` 時，Server 在開發環境另允許任何 `localhost` 來源的呼叫（CORS） |
| `Define/` | 開發用的 `SystemSettings.xml`，以及含 common、company、log 三個資料庫的 `DatabaseSettings.xml`。框架的其他預設定義由 Server 第一次執行時補上 |
| `Excel/` | 放表單活頁簿的位置，以 DefineEditor 匯入 `Define/` |
| `.config/dotnet-tools.json` | DefineEditor（`polhem-define`）與 DbManager（`polhem-db`），版號與 Polhem 套件相同 |
| `.vscode/` | 工作 **Polhem: Initialize database**、**Polhem: Upgrade database**、**Polhem: Open DefineEditor**，以 F5 同時啟動 **Server + Desktop**；有其他用戶端時另有 **Server + Browser**、**Polhem: Run iOS simulator**、**Polhem: Run Android emulator**，以及一次啟動伺服器與所有用戶端的 **Polhem: Run all** |

產生的 `README.md` 說明第一次執行的步驟：設定資料庫密碼、初始化資料庫、從 Excel 匯入表單、按 F5。

### 選項

| 選項 | 預設 | 意義 |
|------|------|------|
| `--database` | `SqlServer` | `SqlServer`、`PostgreSql`、`MySql`、`Oracle` 或 `Sqlite`：決定驅動套件、驅動註冊與 `DatabaseSettings.xml` 的連線 |
| `--company` | `DEMO` | 第一家公司的代號，由 **Polhem: Initialize database** 工作建立 |
| `--db-password` | 空白 | 寫入 `DatabaseSettings.xml` 的資料庫密碼；空白時在 DefineEditor 填入 |
| `--default-language` | `en-US` | `en-US` 或 `zh-TW`：沒有個人語言設定的使用者所用的預設語言，也是用戶端連線與登入畫面的預設語言（使用者切換後以其選擇為準） |
| `--browser` | 關 | 加入瀏覽器用戶端。建置需要 `wasm-tools` workload |
| `--ios` | 關 | 加入 iOS 用戶端。建置需要 macOS、`ios` workload 與該 workload 指定的 Xcode |
| `--android` | 關 | 加入 Android 用戶端。建置需要 `android` workload 與 Android SDK |

用戶端選項預設關閉，未加這些選項建立的應用程式在任何裝有 .NET SDK 的機器上都能建置。

產生的應用程式引用與本範本套件同版號的 Polhem 套件。

### 已知限制：瀏覽器用戶端的中文

瀏覽器用戶端的字型 Inter 沒有中日韓字形，瀏覽器也沒有系統字型可借用。登入前的畫面是英文；登入後，culture 為中文的帳號
會顯示成空白方框。請以 culture 為英文的帳號登入瀏覽器用戶端。

### 已知限制：SQLite 與 DefineEditor

SQLite 連線使用相對於應用程式資料夾的路徑；DefineEditor 匯入後用來升級資料庫的 DbManager 不接受相對路徑的 SQLite。
使用 SQLite 時，匯入後改執行 **Polhem: Upgrade database** 工作。
