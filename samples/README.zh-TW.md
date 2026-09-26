# Polhem — Samples

[English](README.md) | **繁體中文**

最小可運行的 Polhem demo 集合。每個 demo 聚焦單一目的,採 `ProjectReference` 直接引用 `src/` 下的 library(不走 NuGet),改 library 即時反映。

> Solution:[`samples/Polhem.Samples.slnx`](Polhem.Samples.slnx)(獨立於主 `Polhem.Library.slnx`,不會拖累主 CI / build 時間)。

## 30 秒看到 Polhem 跑起來

```bash
# Terminal 1 — 啟動 JSON-RPC API host
cd samples/QuickStart.Server
dotnet run                          # listen on http://localhost:5050

# Terminal 2 — 連線並呼叫 Echo BO
cd samples/QuickStart.Console
dotnet run
```

預期 Terminal 2 印出 `response : echo: hello from QuickStart.Console`。

要看 Blazor 元件實際渲染 `FormSchema`、走 Login + Employee CRUD:

```bash
# Blazor Server(in-process LocalApiProvider,無 HTTP round-trip)
cd samples/Blazor.Server.Demo
dotnet run                          # → http://localhost:5055
```

以 **`demo / demo`** 登入,渲染 `Employee` FormSchema。

## 我該從哪個 demo 看起

| 想了解 | 看這個 |
|--------|--------|
| 如何起一個 Polhem 後端、註冊自訂 BO、暴露 JSON-RPC API | [`QuickStart.Server`](QuickStart.Server/README.zh-TW.md) |
| 如何用 `Polhem.Api.Client` 從第三方端連 Polhem(Remote 模式) | [`QuickStart.Console`](QuickStart.Console/README.zh-TW.md) |
| 如何在 Blazor 內用 `Polhem.Web.Blazor.Server` 元件(Local 派遣,效能最佳) | [`Blazor.Server.Demo`](Blazor.Server.Demo/README.zh-TW.md) |
| 同一份 `FormSchema` 在桌面／瀏覽器／行動端 Avalonia 上如何渲染 | [`apps/Polhem.Northwind`](../apps/Polhem.Northwind/README.zh-TW.md) |
| 主題導向控件 demo center（導覽樹 主題→案例、Demo/Source 分頁、主題/FormMode 工具列）：資料繫結、唯讀必填、FormMode、Layout、Grid、原生 vs 繼承比對 | [`Avalonia.DemoCenter`](Avalonia.DemoCenter/README.md) |
| 如何用純 JavaScript 從瀏覽器呼叫 Polhem（前端無 .NET，走 Plain wire format） | [`Web.Js.Demo`](Web.Js.Demo/README.zh-TW.md) |

## Demo 清單

| 專案 | 角色 | 預設 port | 啟動指令 | 對應 library |
|------|------|-----------|----------|--------------|
| [`QuickStart.Server`](QuickStart.Server/README.zh-TW.md) | API host | `5050` | `dotnet run` | Polhem.Api.AspNetCore + Polhem.Hosting + Polhem.Business + Polhem.Db |
| [`QuickStart.Console`](QuickStart.Console/README.zh-TW.md) | API client | — | `dotnet run` | Polhem.Api.Client |
| [`Blazor.Server.Demo`](Blazor.Server.Demo/README.zh-TW.md) | 全端 Blazor Server | `5055` | `dotnet run` | Polhem.Web.Blazor.Server + Polhem.Samples.Shared |
| [`Avalonia.DemoCenter`](Avalonia.DemoCenter/README.md) | 桌面 Avalonia 控件 demo center | —(無後端) | `dotnet run -c Debug` | Polhem.UI.Avalonia |
| [`Web.Js.Demo`](Web.Js.Demo/README.zh-TW.md) | 純 JS 瀏覽器客戶端 | —(連 5050) | `open index.html` | (無 .NET — vanilla HTML/JS) |
| [`Polhem.Samples.Shared`](Polhem.Samples.Shared/) | 共用後端 wiring | — | (被引用) | Polhem.Business + Polhem.Db + Polhem.Hosting + Polhem.Api.Client |

### Demo 之間的依賴

```
QuickStart.Console ──HTTP──▶ QuickStart.Server
Web.Js.Demo        ──HTTP──▶ QuickStart.Server  ← 需先啟動（已開 CORS）

Blazor.Server.Demo                ← 不需另起 server,前後端同 process
```

## 共用帳號

Blazor demo 的 Login 走 `demo / demo`:

| 欄位 | 值 |
|------|-----|
| User ID | `demo` |
| Password | `demo` |
| 顯示名稱 | `Demo User` |

由 [`DemoAuthenticatingSystemBusinessObject`](Polhem.Samples.Shared/DemoAuthenticatingSystemBusinessObject.cs) 寫死比對,不查 `st_user`,因此**完全不需要 seed 任何系統資料表**。

`QuickStart.Server` / `QuickStart.Console` 的 `Echo.Echo` 標 `[ApiAccessControl(Public, Anonymous)]`,**不需要登入**。

## 共用 Define

[`samples/Define/`](Define/) 是所有 demo 共用的定義檔目錄。各 host 用「從執行目錄向上找 `Define/SystemSettings.xml`」的策略指向這裡(見 [`DemoBackend.ResolveDefinePath`](Polhem.Samples.Shared/DemoBackend.cs)),確保「同一份定義驅動多個前端」。

```
Define/
├── SystemSettings.xml                       # 系統設定(IsDebugMode=true、MasterKeySource=Environment)
├── DbCategorySettings.xml                   # 一個 common category
├── DatabaseSettings.xml                     # SQLite local DB(quickstart.db)
├── FormSchema/
│   └── Employee.FormSchema.xml              # master-detail 示範(員工 + 員工電話)
└── TableSchema/
    └── common/
        ├── ft_employee.TableSchema.xml
        └── ft_employee_phone.TableSchema.xml
```

## Master key

`SystemSettings.xml` 預設 `MasterKeySource.Type = Environment`、`Value = POLHEM_MASTER_KEY`,所以每個 demo host 都從環境變數讀加密 master key。[`DemoBackend.AddPolhemBackend`](Polhem.Samples.Shared/DemoBackend.cs) 在 `POLHEM_MASTER_KEY` 未設時會自動注入一個固定 demo 值(`DemoCredentials.DemoMasterKey`),fresh clone 可零設定直接跑,且 `quickstart.db` 跨 run 的加密內容仍能正常解密。

> **Production host 必須覆寫 demo master key。** demo 常數會進 git 公開,僅供 demo 使用。真實部署必須在 process 啟動「之前」由部署機制(K8s Secret、env file、Vault、AWS Secrets Manager…)把 `POLHEM_MASTER_KEY` 設為真實 secret;bootstrap 僅在變數未設時才填值,外部已注入的值會被保留。

## 首次執行自動生成的檔案

下列檔案**不會**進 git,是執行時產物。clone 下來首次 `dotnet run` 會自動建立:

| 檔案 | 由誰建立 | 內容 | gitignore 規則 |
|------|----------|------|----------------|
| `samples/<Host>/quickstart.db` | [`DemoSchemaSeeder`](Polhem.Samples.Shared/DemoSchemaSeeder.cs) | SQLite,含 `ft_employee` + `ft_employee_phone` 兩張表與 3 筆 demo 資料(Alice / Bob / Carol) | `/samples/**/*.db` |

> 兩個 host(`QuickStart.Server` / `Blazor.Server.Demo`)**各有自己的 `quickstart.db`**,不會互相干擾。同一個 host 重跑會沿用既有資料(schema 建立與 seed 都是 idempotent)。

要重置 demo 資料:直接刪 `samples/<Host>/quickstart.db` 重跑即可。要輪換 demo master key:改 `DemoCredentials.DemoMasterKey`(或在外部把 `POLHEM_MASTER_KEY` 設成新值)並一併刪所有 `quickstart.db`(舊資料用舊 key 加密,留著會解不開)。

## Local vs Remote 派遣模式

Polhem 的 `Polhem.Api.Client` 對呼叫端有**一致的 API 表面**,差異只在底層 provider:

| 模式 | 路徑 | 用於 | 範例 demo |
|------|------|------|-----------|
| **Local** | client → `LocalApiProvider` → `JsonRpcExecutor` → BO(同 process) | Blazor Server、in-process 工具、跨 BO 直接呼叫 | `Blazor.Server.Demo` |
| **Remote** | client → `RemoteApiProvider` → HTTP POST → `ApiServiceController` → `JsonRpcExecutor` → BO | Console、桌面、行動端、跨機器 | `QuickStart.Console` |

切換只是 `AddPolhemBlazor` / `ApiClientInfo` 一行設定:

```csharp
// Local
builder.Services.AddPolhemBlazor(o => o.UseLocalProvider());

// Remote
builder.Services.AddPolhemBlazor(o => o.UseRemoteProvider("http://host:5070/api"));
```

## 建置全部 samples

```bash
dotnet build samples/Polhem.Samples.slnx
```

> `./test.sh` 與主 `Polhem.Library.slnx` 都**不會**跑到 samples;samples 永遠是「想試時手動跑」。

## 常見問題

**Q: Port 5050/5055/5070 被佔用怎麼辦?**
編輯 `samples/<Host>/Properties/launchSettings.json` 的 `applicationUrl`。別忘了同步更新指向該 host 的設定 —— 例如 `QuickStart.Console` 的 `--endpoint` 旗標。

**Q: 出現 `Could not locate 'Define/SystemSettings.xml' walking up from ...`?**
請從 polhem checkout 目錄內執行 `dotnet run`,不要把 binary 拷貝到 repo 外。`DemoBackend` 是用「從 `AppContext.BaseDirectory` 向上找」的策略,跳出 repo 後找不到 `Define/`。

**Q: 兩個 host 同時跑會不會打架?**
不會。兩個 host port 不同(5050 / 5055),各自有獨立的 `quickstart.db`,共用 `samples/Define/` 但只讀不寫。兩個都跑 + Console 一起測試完全可行。

**Q: 改了 `src/` 下的 library,要怎麼反映到 demo?**
重跑即可,`ProjectReference` 會自動 rebuild。不需要 `dotnet pack` / 也不需要清快取。

## 刻意不做

- 真實 ERP 業務情境(銷售單、進貨單等)— 留給未來獨立 demo repo
- SQL Server / PostgreSQL / Oracle / MySQL — SQLite 已足夠示範
- 認證 / 授權完整流程(OAuth、JWT、實際 `st_user` 表)— 用 hard-coded `demo/demo` 帶過
- 部署腳本(Docker / k8s / TestFlight / Microsoft Store)
