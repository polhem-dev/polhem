# Polhem.Hosting

> Polhem 框架的組合根（composition root）——將後端服務註冊到任意 `IServiceCollection`，不依賴 ASP.NET Core。

[English](README.md)

## 架構定位

- **層級**：組合根（DI 註冊）
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/architecture/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。它們確實漂了：`Polhem.Hosting` 抽出後，有四份 README 的下游數個月都沒把它補上。
- 也可由沒有 ASP.NET Core 的宿主使用：在行程內執行後端的桌面 head、主控台、Worker Service 與整合測試。

組合根本質上橫跨所有層，因此「API 層不得引用 Repository 層」這條約束不適用於此。適用的是：此套件目前**沒有
自己的 SQL**，也應維持如此。它的 hosted service 都只是外殼 —— cache-notify 輪詢器透過 `ICacheNotifyReader`
（`Polhem.Db`）讀取，預設的稽核 sink 透過 `IAuditLogWriteRepository`（`Polhem.Repository.Abstractions`，
實作在 `Polhem.Repository`）寫入。語句的組裝與執行屬於那些層；在此新增 SQL 就是分層倒退。

## 目標框架

- `net10.0`

## 何時引用此套件

| 宿主類型 | 引用方式 |
|---------|---------|
| ASP.NET Core web host | `Polhem.Api.AspNetCore`（透過遞移帶入 `Polhem.Hosting`）|
| 在行程內執行後端的桌面 head、主控台、Worker Service | 直接引用 `Polhem.Hosting` |
| 整合測試 | 直接引用 `Polhem.Hosting` |

只與遠端伺服器溝通的 head（以遠端端點使用 `Polhem.Api.Client`）不需要此套件。在自己行程內執行後端的 head
兩者都要引用：以 `AddPolhemFramework` 建立後端，再把產生的 `IServiceProvider` 傳給行程內連接器的建構子。

## 主要公開 API

| 類別 / 成員 | 用途 |
|------------|------|
| `PolhemFrameworkServiceCollectionExtensions.AddPolhemFramework` | 將框架服務（`IDefineAccess`、`IDbAccessFactory`、`IBusinessObjectFactory`、`JsonRpcExecutor`、各 hosted service 等）註冊至傳入的 `IServiceCollection` |
| `IAuditLogSink` | 稽核紀錄的去處。預設寫入 log 資料庫；在 `AddPolhemFramework` 之前註冊自己的實作即可送往別處 |

## 使用方式

請先註冊資料庫提供者（見 `Polhem.Db` 的 README）。

### ASP.NET Core 宿主

```csharp
using Polhem.Api.AspNetCore;
using Polhem.Api.Core;
using Polhem.Base;
using Polhem.Definition;
using Polhem.Hosting;

var builder = WebApplication.CreateBuilder(args);

var paths = new PathOptions { DefinePath = Path.Combine(AppContext.BaseDirectory, "Define") };
var settings = SystemSettingsLoader.Load(paths);
SysInfo.Initialize(settings.CommonConfiguration);
ApiServiceOptions.Initialize(settings.CommonConfiguration.ApiPayloadOptions, settings.CommonConfiguration.IsDebugMode);

builder.Services.AddPolhemFramework(settings.BackendConfiguration, paths);
builder.Services.AddControllers();

var app = builder.Build();
app.UsePolhemFramework();
app.MapControllers();
app.Run();
```

控制器是衍生自 `ApiServiceController` 的類別（見 `Polhem.Api.AspNetCore` 的 README）。

### 沒有 ASP.NET Core 的宿主（在行程內執行後端的桌面 head）

框架會註冊 hosted service（保留 progId 註冊、cache-notify 輪詢器、session 清理、背景稽核寫入器），因此要建立
generic host 並啟動它。這需要 `Microsoft.Extensions.Hosting` 套件。

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core;
using Polhem.Base;
using Polhem.Definition;
using Polhem.Hosting;

var paths = new PathOptions { DefinePath = definePath };
var settings = SystemSettingsLoader.Load(paths);
SysInfo.Initialize(settings.CommonConfiguration);
ApiServiceOptions.Initialize(settings.CommonConfiguration.ApiPayloadOptions, settings.CommonConfiguration.IsDebugMode);

var builder = Host.CreateApplicationBuilder();
builder.Services.AddPolhemFramework(settings.BackendConfiguration, paths);
using var host = builder.Build();
await host.StartAsync();

// 行程內連接器接受後端服務提供者，而不是端點 URL。
var connector = new SystemApiConnector(host.Services, Guid.Empty);
var login = await connector.LoginAsync("demo", "demo");
```

`Polhem.UI.Core` 的 head 則把 `host.Services` 指派給 `ClientInfo.LocalServiceProvider`，由 `ClientInfo`
建立的連接器使用它。

## 設計慣例

- **組合根** — DI 註冊集中於此，與 ASP.NET Core middleware（保留在 `Polhem.Api.AspNetCore`）分離
- **不依賴 ASP.NET Core** — 只引用 `Microsoft.Extensions.DependencyInjection` 與 `Microsoft.Extensions.Hosting` 的抽象套件，非 web 宿主也能註冊框架而不必拉進整個 web stack
- **以型別名稱替換實作** — `BackendComponents`（位於 `SystemSettings.xml`）列出的服務，例如 `IDefineAccess`、`ISessionInfoService`、`ICacheDataSourceProvider` 與 `RepositoryFactory`，可在那裡指定型別加以替換；留空代表框架預設。型別名稱錯誤會在啟動時失敗，並指出是哪個設定。其他服務（例如 `IBusinessObjectFactory`）則直接註冊
- **Hosted service 皆為 internal** — 稽核寫入器、cache-notify 輪詢器、過期 session 清理與啟動檢查由 `AddPolhemFramework` 註冊，並透過 `BackendConfiguration` 設定；`IAuditLogSink` 是公開的替換點

## 目錄結構

- `PolhemFrameworkServiceCollectionExtensions*.cs` -- `AddPolhemFramework` 與其輔助方法
- `Audit/` -- `IAuditLogSink` 與內部的稽核寫入器
- `CacheNotify/` -- cache-notify 輪詢器
- `Database/` -- 啟動時檢查 `DatabaseSettings` 具備框架必需的項目
- `Session/` -- 過期 session 清理
- `Registry/` -- 啟動時的註冊與警告服務
