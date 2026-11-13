# Polhem.Hosting

> Polhem 框架的組合根（composition root）——將後端服務註冊到任意 `IServiceCollection`，不依賴 ASP.NET Core。

[English](README.md)

## 架構定位

- **層級**：組合根（DI 註冊）
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/architecture/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。它們確實漂了：`Polhem.Hosting` 抽出後，有四份 README 的下游數個月都沒把它補上。
- 也可由沒有 ASP.NET Core 的宿主使用：在行程內執行後端的桌面 head、主控台、Worker Service 與整合測試。

組合根本質上橫跨所有層，因此「API 層不得引用 Repository 層」這條約束不適用於此。適用的是：此套件**沒有
自己的 SQL**。它的 hosted service 都只是外殼 —— cache-notify 輪詢器透過 `ICacheNotifyReader`（`Polhem.Db`）
讀取，預設的稽核 sink 透過 `IAuditLogWriteRepository`（`Polhem.Repository.Abstractions`，實作在
`Polhem.Repository`）寫入。`db` 指令背後的資料庫初始化，透過 `IAdministratorSeedRepository` 寫入第一位管理員，
透過 `ICompanyRowAssignmentRepository` 把既有資料指派給公司。此套件若建構 `DbCommandSpec` 或 `DbBatchSpec`、
經由 `DbAccess` 執行語句（`Execute*`、`Query*`、`Update*`），或建立、執行 ADO.NET 命令，
`HostingHoldsNoSqlGateTests`（`tests/Polhem.Hosting.UnitTests`）會失敗。

## 目標框架

- `net10.0`

## 何時引用此套件

| 宿主類型 | 引用方式 |
|---------|---------|
| ASP.NET Core web host | `Polhem.Hosting`，HTTP 端點另加 `Polhem.JsonRpc.AspNetCore` |
| 在行程內執行後端的桌面 head、主控台、Worker Service | 直接引用 `Polhem.Hosting` |
| 整合測試 | 直接引用 `Polhem.Hosting` |

只與遠端伺服器溝通的 head（以遠端端點使用 `Polhem.Api.Client`）不需要此套件。在自己行程內執行後端的 head
兩者都要引用：以 `AddPolhemFramework` 建立後端，再把產生的 `IServiceProvider` 傳給 `PolhemApiClient.CreateLocal`。

## 主要公開 API

| 類別 / 成員 | 用途 |
|------------|------|
| `PolhemHostApplicationBuilderExtensions.AddPolhemBackend` | 在以 `IHostApplicationBuilder` 建立的宿主（Web 應用程式、Worker Service、generic host）中啟動後端：除非 `PolhemBackendOptions.DefinePath` 另行指定，否則從建置輸出往上尋找 `Define` 資料夾；補上該資料夾還沒有的框架預設定義；讀取 `SystemSettings.xml`、初始化 `SysInfo`，再呼叫 `AddPolhemPayload` 與 `AddPolhemFramework`。回傳它註冊的 `PathOptions` |
| `PolhemFrameworkServiceCollectionExtensions.AddPolhemFramework` | 將框架服務（`IDefineAccess`、`IDbAccessFactory`、`IBusinessObjectFactory`、JSON-RPC dispatcher 與其選項、`IInProcessTransportFactory`（供 `PolhemApiClient.CreateLocal` 使用）、各 hosted service 等）註冊至傳入的 `IServiceCollection` |
| `PolhemFrameworkServiceCollectionExtensions.AddPolhemApiKeyGateCheck` | 尚未發行 API key 時於啟動時記錄 log。供以 HTTP 提供 API 的宿主使用 |
| `PolhemFrameworkServiceCollectionExtensions.AddPolhemPayload` | 依 `CommonConfiguration.ApiPayloadOptions` 註冊伺服器讀寫 payload 外殼所用的選項（壓縮器、加密器、frame）。不呼叫時採預設值：gzip、aes-cbc-hmac、不開 frame。.NET 用戶端把相同設定存放在 `PolhemApiClient.PayloadOptions` |
| `IAuditLogSink` | 稽核紀錄的去處。預設寫入 log 資料庫；在 `AddPolhemFramework` 之前註冊自己的實作即可送往別處 |
| `IDatabaseInitializer` | 建立 `DatabaseSettings` 的資料庫、建立或升級 `DbCategorySettings` 的資料表，並建立第一位管理員與公司。由 `AddPolhemFramework` 註冊 |
| `DatabaseCommandLine.TryRun` | 從主機的命令列執行 `db` 指令（建立資料庫、建立與升級資料表、建立第一位管理員…），背後是 `IDatabaseInitializer` |

## 使用方式

請先註冊資料庫提供者（見 `Polhem.Db` 的 README）。

### ASP.NET Core 宿主

```csharp
using Polhem.Hosting.DependencyInjection;
using Polhem.JsonRpc.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddPolhemBackend();
builder.Services.AddJsonRpcServer();
builder.Services.AddPolhemApiKeyGateCheck();

var app = builder.Build();
app.MapJsonRpc("/api");
app.Run();
```

`AddPolhemBackend` 以 callback 接受選項（`PolhemBackendOptions`）：`DefinePath`、`CustomizePath`、
`MaterializeDefaults` 與 `MaterializeOptions`，以及 `AutoCreateMasterKey`。不建立 `IHostApplicationBuilder`
或以其他方式載入設定的宿主，自己依序呼叫 `SystemSettingsLoader.Load`、`SysInfo.Initialize`、`AddPolhemPayload` 與
`AddPolhemFramework`。

`AddJsonRpcServer` 來自 `Polhem.JsonRpc.AspNetCore`，沿用 `AddPolhemFramework` 註冊的 JSON-RPC 選項；`MapJsonRpc`
把它們發布在 `POST /api`。

### 沒有 ASP.NET Core 的宿主（在行程內執行後端的桌面 head）

框架會註冊 hosted service（保留 progId 註冊、cache-notify 輪詢器、session 清理、背景稽核寫入器），因此要建立
generic host 並啟動它。這需要 `Microsoft.Extensions.Hosting` 套件。

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polhem.Api.Client;
using Polhem.Core;
using Polhem.Definition;
using Polhem.Hosting.DependencyInjection;

var paths = new PathOptions { DefinePath = definePath };
var settings = SystemSettingsLoader.Load(paths);
SysInfo.Initialize(settings.CommonConfiguration);

var builder = Host.CreateApplicationBuilder();
builder.Services.AddPolhemFramework(settings.BackendConfiguration, paths);
builder.Services.AddPolhemPayload(settings.CommonConfiguration.ApiPayloadOptions, settings.CommonConfiguration.IsDebugMode);
using var host = builder.Build();
await host.StartAsync();

// 行程內 client 接受後端服務提供者，而不是端點 URL。
var client = PolhemApiClient.CreateLocal(host.Services);
var login = await client.System.LoginAsync("demo", "demo");
```

建立在 [`Polhem.UI.ApiBridge`](../Polhem.UI.ApiBridge/README.zh-TW.md) 上的單一使用者 head 則把 `host.Services`
指派給 `ClientInfo.LocalServiceProvider`，行程內的 `ClientInfo.ApiClient` 會派遣到它。

### 初始化資料庫

主機可以從自己的命令列建立資料庫、建立資料表並建立第一位管理員。在建好主機之後、啟動之前檢查 `db` 指令：

```csharp
if (DatabaseCommandLine.TryRun(host.Services, args) is int exitCode) return exitCode;
```

之後 `dotnet run -- db create`、`db upgrade`、`db seed-admin --company <id>` 與其他 `db` 指令就會在不啟動主機的情況下執行。
`dotnet run -- db` 不帶指令時會列出全部指令；各指令的使用時機見 [DatabaseSettings 指引](../../docs/zh-TW/database/database-settings-guide.md#7-初始化新部署)。

## 設計慣例

- **組合根** — DI 註冊集中於此，與 HTTP 端點（由 `Polhem.JsonRpc.AspNetCore` 提供）分離
- **不依賴 ASP.NET Core** — 只引用 `Microsoft.Extensions.DependencyInjection` 與 `Microsoft.Extensions.Hosting` 的抽象套件，非 web 宿主也能註冊框架而不必拉進整個 web stack
- **以型別名稱替換實作** — `BackendComponents`（位於 `SystemSettings.xml`）列出的服務，例如 `IDefineAccess`、`ISessionInfoService`、`ICacheDataSourceProvider` 與 `RepositoryFactory`，可在那裡指定型別加以替換；留空代表框架預設。型別名稱錯誤會在啟動時失敗，並指出是哪個設定。其他服務（例如 `IBusinessObjectFactory`）則直接註冊
- **Hosted service 皆為 internal** — 稽核寫入器、cache-notify 輪詢器、過期 session 清理與啟動檢查由 `AddPolhemFramework` 註冊，並透過 `BackendConfiguration` 設定；`IAuditLogSink` 是公開的替換點

## 目錄結構

- `DependencyInjection/`（命名空間 `Polhem.Hosting.DependencyInjection`）：
  - `PolhemFrameworkServiceCollectionExtensions*.cs` -- `AddPolhemFramework` 與其輔助方法
  - `PolhemHostApplicationBuilderExtensions.cs`、`PolhemBackendOptions.cs` -- `AddPolhemBackend` 與其選項
  - `BackendDefaultTypes.cs` -- `BackendComponents` 項目留空時採用的型別名稱
- `ApiKeys/` -- 尚未發出任何 API 金鑰時的啟動警告
- `Audit/` -- `IAuditLogSink` 與內部的稽核寫入器
- `CacheNotify/` -- cache-notify 輪詢器
- `Database/` -- 啟動時檢查 `DatabaseSettings` 具備框架必需的項目，以及 `db` 指令背後的資料庫初始化
- `Session/` -- 過期 session 清理
- `Registry/` -- 啟動時的註冊與警告服務
