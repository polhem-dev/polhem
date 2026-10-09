# QuickStart.Server

[English](README.md) | **繁體中文**

最小可運行的 Polhem JSON-RPC API host。對外暴露：

- `System.Ping` — 框架內建，anonymous；驗證 host 可達。
- `Echo.Echo` — sample BO，anonymous；回傳的訊息會被加上 `"echo: "` 前綴。

## 跑法

```bash
cd samples/QuickStart.Server
dotnet run
```

啟動後 listen 在 `http://localhost:5050`，JSON-RPC endpoint 為 `POST /api`。

## 預期輸出

第一次啟動會：

1. 載入 `samples/Define/` 下的定義，包括 `SystemSettings.xml`、`DatabaseSettings.xml` 與 `ProgramSettings.xml`（把 `Echo` progId 綁到本範例的業務物件）
2. 從環境變數 `POLHEM_MASTER_KEY` 取得 master key。demo bootstrap (`DemoBackend.AddPolhemBackend`) 在變數未設時會自動注入硬編碼的 demo 值,所以 fresh clone 可零設定直接跑。
3. 在專案資料夾產生 `quickstart.db`（SQLite，已被 `.gitignore`），並由 `DemoSchemaSeeder` 建表灌種子

console 應顯示 `Now listening on: http://localhost:5050`。

> **Production host 必須覆寫 demo master key。** 硬編碼的 demo 值位於
> `Polhem.Samples.Shared.DemoCredentials.DemoMasterKey`,進 git 公開,僅供 demo
> 使用。真實部署必須在 process 啟動「之前」由部署機制(K8s Secret、env file、
> Vault、AWS Secrets Manager…)把 `POLHEM_MASTER_KEY` 設成真實 secret;bootstrap
> 僅在變數未設時才填值,外部已注入的值會被保留。

## 對應到哪些 library 功能

`Program.cs` 之所以很短，是因為後端接線放在共用的 [`DemoBackend`](../Polhem.Samples.Shared/DemoBackend.cs)（`AddPolhemBackend` / `UsePolhemBackend`），Blazor demo 也用同一份。下表的呼叫除另有註明外都在該檔。

| 程式段落 | library 功能 |
|----------|--------------|
| `DbProviderRegistry.Register(DatabaseType.SQLite, new SqliteProviderFactory(SqliteFactory.Instance))` | `Polhem.Db.Manager.DbProviderRegistry` — ADO.NET provider 切換。`SqliteProviderFactory` 包住驅動程式的 factory，因為後者本身沒有 data adapter |
| `DbDialectRegistry.Register(DatabaseType.SQLite, new SqliteDialectFactory())` | `Polhem.Db.Providers.Sqlite` — SQLite dialect（form CRUD / schema 反射 / DDL） |
| `SystemSettingsLoader.Load(paths)` | `Polhem.Definition.SystemSettingsLoader` — boot-time 載入 XML |
| `services.AddPolhemFramework(...)` | `Polhem.Hosting.PolhemFrameworkServiceCollectionExtensions` — backend composition root |
| `samples/Define/ProgramSettings.xml` 裡的 `<ProgramItem ProgId="Echo" BusinessObject="…" />` | `Polhem.Definition.Settings.ProgramSettings` — progId → 業務物件的註冊表；綁定不需要任何程式碼 |
| `services.AddJsonRpcServer()` + `app.MapJsonRpc("/api")`（`Program.cs`） | `Polhem.JsonRpc.AspNetCore` — JSON-RPC endpoint，沿用 `AddPolhemFramework` 註冊的選項 |
| `services.AddPolhemApiKeyGateCheck()`（`Program.cs`） | `Polhem.Hosting` — 尚未發行 API key 時，於啟動時記錄 log |
| `[ApiAccessControl(Public, Anonymous)]`（`BusinessObjects/EchoBusinessObject.cs`） | `Polhem.Definition.Attributes.ApiAccessControlAttribute` — API 存取控制 |

## JavaScript 用戶端

[polhem-connector-js 的範例](https://github.com/polhem-dev/polhem-connector-js/tree/main/examples)（一支 Node 程式與一個瀏覽器頁面）可以直接連這個 host：以 `demo` / `demo`
登入、列出 `Staff` 表單，並新增、修改、刪除一筆資料。host 允許瀏覽器頁面的跨來源呼叫。

## 試打看看（不啟動 console demo 的情況下）

```bash
curl -s -X POST http://localhost:5050/api \
  -H 'Content-Type: application/json' \
  -H 'X-Api-Key: quickstart-demo' \
  -d '{
        "jsonrpc": "2.0",
        "id": "1",
        "method": "Echo.Echo",
        "params": { "format": 0, "value": { "message": "hello" } }
      }'
```

預期回一個 JSON-RPC envelope，其 `result.value.response` 為 `"echo: hello"`。payload 的屬性名稱是精確比對的小寫（`format`、`value`）；寫成 `Value` 時該成員不會被讀取，呼叫等於沒有帶值，會在業務物件執行前就回 `-32602 Invalid params`。`format: 0` 即 `Plain`，`Echo` 宣告為 `Public`，所以接受它。
