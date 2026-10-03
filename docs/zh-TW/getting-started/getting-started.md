<!-- source: en/getting-started/getting-started.md blob: fe7ee52176791c2545c552ac01cee71398be6080 -->
# 快速上手

[English](../../en/getting-started/getting-started.md) · [← 文件索引](../README.md)

> 從一個空資料夾建出第一個 Polhem 後端：安裝套件、備妥 `DefinePath`、接好 DI 容器、發布 JSON-RPC 端點、加一個商業物件，再由用戶端呼叫。

本文帶你建**自己的專案**。若你只想先看框架跑起來、還不想動手寫，repo 的 [`samples/`](../../../samples/README.zh-TW.md) 有可直接執行的範例 —— `QuickStart.Server` + `QuickStart.Console` 正是本頁對應的那組。

每個步驟都連向深入說明該主題的文件。本頁只給最小可跑的內容，不重複那些文件已寫的東西。

---

## 前置需求

- **.NET 10 SDK**
- **一個資料庫**。SQL Server、PostgreSQL、MySQL、Oracle、SQLite 皆可。SQLite 不需架設伺服器，以下即以它示範。

## 1. 建專案並安裝套件

```bash
dotnet new web -o MyApp.Server
cd MyApp.Server
dotnet add package Polhem.Hosting
dotnet add package Polhem.JsonRpc.AspNetCore
dotnet add package Polhem.Db
dotnet add package Microsoft.Data.Sqlite
```

`Microsoft.Data.Sqlite` 是下文所用 SQLite 資料庫的 ADO.NET driver。框架本身不附任何 driver；
步驟 3 列出各資料庫對應的套件。

**該選哪些 host 套件？** `Polhem.Hosting` 是組合根，`Polhem.JsonRpc.AspNetCore` 以 HTTP 提供 API。若你的 host 不是 ASP.NET Core —— WinForms、WPF、Console、Worker Service —— 只參考 `Polhem.Hosting`，並略過步驟 5。

## 2. 備妥 `DefinePath`

框架從一個 XML 定義檔目錄（`DefinePath`）啟動。它的預設集 —— `SystemSettings.xml`、`DatabaseSettings.xml`、`DbCategorySettings.xml` 等設定檔、`st_*` TableSchema，以及框架隨附的表單與其語系資源 —— 以嵌入資源形式放在 `Polhem.Definition.dll`；`dotnet polhem defines list` 會列出完整清單。在專案資料夾下把它們展開一次：

```bash
dotnet tool install -g Polhem.Cli
dotnet polhem defines materialize --path ./Define
```

預設 skip-existing，重跑不會覆蓋你自己的修改。同一動作也可用程式呼叫 `Polhem.Definition.Defaults.MaterializeTo(...)`。

接著編輯 `./Define` 下兩個檔案。

**`SystemSettings.xml`** —— 設定 `MasterKeySource`，也就是保護其他金鑰的主金鑰（master key）從哪裡來。
隨附的值是 `Environment`，從環境變數 `POLHEM_MASTER_KEY` 讀取金鑰。本文改用金鑰檔：

```xml
<MasterKeySource>
  <Type>File</Type>
  <Value>Master.key</Value>
</MasterKeySource>
```

相對路徑的 `Value` 以 `DefinePath` 為基準。搭配 `autoCreateMasterKey: true`（步驟 4），第一次啟動會寫出
`Define/Master.key`，之後每次啟動都讀同一把金鑰。這個檔案不要放進版本控制。

**`DatabaseSettings.xml`** —— 加入資料庫。本文用到的框架資料表都屬於 `common` 類別，
因此一筆 `common` 項目就夠了：

```xml
<DatabaseSettings>
  <Items>
    <DatabaseItem Id="common" CategoryId="common" DatabaseType="SQLite"
                  ConnectionString="Data Source=myapp.db" />
  </Items>
</DatabaseSettings>
```

→ 每個定義檔各管什麼：[定義檔全景](../definitions/definition-files-overview.md)。完整檔案清單與使用端擴充規則：[框架保留命名](../reference/framework-reserved-names.md)。

## 3. 註冊資料庫方言

框架不強迫每個 host 都拉進所有 ADO.NET driver，因此你用哪個方言就明確註冊哪個：

```csharp
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Db.Providers.Sqlite;
using Polhem.Definition.Database;
using Microsoft.Data.Sqlite;

DbProviderRegistry.Register(DatabaseType.SQLite, new SqliteProviderFactory(SqliteFactory.Instance));
DbDialectRegistry.Register(DatabaseType.SQLite, new SqliteDialectFactory());
```

只有 SQLite 需要框架自己的 `SqliteProviderFactory` 包裝，其餘四家直接使用廠商的 factory。
各資料庫的實際型別：

| `DatabaseType` | ADO.NET provider factory | Dialect factory | NuGet 套件 |
|----------------|--------------------------|-----------------|-----------|
| `SQLServer` | `SqlClientFactory.Instance` | `SqlDialectFactory` | `Microsoft.Data.SqlClient` |
| `PostgreSQL` | `NpgsqlFactory.Instance` | `PgDialectFactory` | `Npgsql` |
| `MySQL` | `MySqlConnectorFactory.Instance` | `MySqlDialectFactory` | `MySqlConnector` |
| `Oracle` | `OracleClientFactory.Instance` | `OracleDialectFactory` | `Oracle.ManagedDataAccess.Core` |
| `SQLite` | `new SqliteProviderFactory(SqliteFactory.Instance)` | `SqliteDialectFactory` | `Microsoft.Data.Sqlite` |

各 dialect factory 位於 `Polhem.Db.Providers.<Vendor>`，`using` 要跟著換。以 SQL Server 為例：

```csharp
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Db.Providers.SqlServer;
using Polhem.Definition.Database;
using Microsoft.Data.SqlClient;

DbProviderRegistry.Register(DatabaseType.SQLServer, SqlClientFactory.Instance);
DbDialectRegistry.Register(DatabaseType.SQLServer, new SqlDialectFactory());
```

## 4. 接線 DI 容器

完整的 `Program.cs`，開頭就是步驟 3 的兩行註冊：

```csharp
using Microsoft.Data.Sqlite;
using Polhem.Api.Core;
using Polhem.Core;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Db.Providers.Sqlite;
using Polhem.Db.Schema;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Storage;
using Polhem.Hosting;
using Polhem.JsonRpc.AspNetCore;

DbProviderRegistry.Register(DatabaseType.SQLite, new SqliteProviderFactory(SqliteFactory.Instance));
DbDialectRegistry.Register(DatabaseType.SQLite, new SqliteDialectFactory());

var builder = WebApplication.CreateBuilder(args);

var paths = new PathOptions { DefinePath = "./Define" };
var settings = SystemSettingsLoader.Load(paths);

SysInfo.Initialize(settings.CommonConfiguration);

builder.Services.AddPolhemFramework(
    settings.BackendConfiguration,
    paths,
    autoCreateMasterKey: true);
builder.Services.AddPolhemPayload(
    settings.CommonConfiguration.ApiPayloadOptions,
    settings.CommonConfiguration.IsDebugMode);

builder.Services.AddJsonRpcServer();
builder.Services.AddPolhemApiKeyGateCheck();

var app = builder.Build();

// 建立 common 類別中尚不存在的框架資料表。
var defineAccess = app.Services.GetRequiredService<IDefineAccess>();
var connectionManager = app.Services.GetRequiredService<IDbConnectionManager>();
var common = defineAccess.GetDbCategorySettings().Categories!["common"];
var schemaBuilder = new TableSchemaBuilder("common", defineAccess, connectionManager);
foreach (var table in common.Tables!)
    schemaBuilder.Execute("common", table.TableName);

app.MapJsonRpc("/api");
app.Run();
```

- `SysInfo.Initialize` 設定的是請求處理時會讀的全程序值（除錯旗標、允許的型別命名空間），要在 host 開始服務之前
  呼叫。`AddPolhemPayload` 註冊設定檔指名的 payload 壓縮器與加密器。
- **框架不會替你建資料表。** 即使是匿名呼叫，也會讀 `st_api_key` 來檢查 `X-Api-Key` 標頭，而因資料表不存在
  而查詢失敗時，呼叫會被拒絕；cache-notify 輪詢器則會讀 `st_cache_notify`。上面的迴圈建立
  `DbCategorySettings.xml` 在 `common` 底下登記的每一張表，之後每次啟動再依 TableSchema 把它們調整一致。
  實際的應用程式也用同樣方式建自己的資料表，每個資料庫一個 `TableSchemaBuilder`
  （見[資料庫 Schema 升級](../database/database-schema-upgrade.md)）。
- `AddPolhemApiKeyGateCheck` 會在尚未發出任何 API key 時，於啟動時記錄一筆 log（Development 環境為警告，其他環境為
  錯誤）。它在 host 啟動時（`app.Run()`）讀 `st_api_key`，那時上面的資料表已經建好。只有以 HTTP 提供 API 的 host 需要它。
- `autoCreateMasterKey: true` 會在主金鑰不存在時建立一把。搭配步驟 2 的 `File` 來源，這只發生一次。若用
  `Environment` 來源，產生的金鑰只存在於該程序的環境變數中，因此每次啟動都是不同的金鑰，先前以舊金鑰加密的值
  從此無法解密。除了用完即丟的示範之外，不要把 `Environment` 與 `autoCreateMasterKey: true` 搭配使用。
- `./Define` 與 `Data Source=myapp.db` 都相對於工作目錄，所以請在專案資料夾下啟動伺服器。

→ 啟動流程圖與 `AddPolhemFramework` 註冊了什麼：[端到端開發指引 § 框架初始化順序](../guides/development-cookbook.md#框架初始化順序)。順序背後的限制：[開發限制與反模式 § 初始化順序限制](../architecture/development-constraints.md#初始化順序限制)。

## 5. 發布 JSON-RPC 端點

步驟 4 的兩個呼叫就是整個端點：`AddJsonRpcServer()` 沿用 `AddPolhemFramework` 註冊的 JSON-RPC 選項，
`app.MapJsonRpc("/api")` 把它們對應到路由。不需要撰寫 controller。

`POST /api` 現在已能接受 JSON-RPC 2.0 請求。若要對每次呼叫執行自己的檢查，加入 filter：
`AddJsonRpcServer(options => options.Filters.Add(new MyFilter()))`。它在框架自己的檢查之內執行。

## 6. 寫第一個商業物件

商業物件以 **progId** 定位。框架保留了 `System`、`AuditLog` 與 `AuditRule`（`ReservedProgIds`）；其他 progId 一律走 form business object 派發，故繼承 `FormBusinessObject` 並比照其建構子簽章。程式碼放在 `BusinessObjects/EchoBusinessObject.cs`：

```csharp
using Polhem.Business;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Attributes;
using Polhem.Definition.Security;

namespace MyApp.Server.BusinessObjects;

public class EchoArgs : BusinessArgs
{
    public string Message { get; set; } = string.Empty;
}

public class EchoResult : BusinessResult
{
    public string Response { get; set; } = string.Empty;
}

public class EchoBusinessObject : FormBusinessObject
{
    public EchoBusinessObject(IBusinessObjectContext ctx, Guid accessToken, string progId, bool isLocalCall = false)
        : base(ctx, accessToken, progId, isLocalCall)
    {
    }

    [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Anonymous)]
    public virtual EchoResult Echo(EchoArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return new EchoResult { Response = $"echo: {args.Message}" };
    }
}
```

`[ApiAccessControl]` 決定該方法是否對外可達、以及其保護等級：沒有任何屬性涵蓋的方法，呼叫時會被拒絕，analyzer POLHEM3001 也會在建置時對這種方法發出警告。`Public` + `Anonymous` 不需 access token 也不需加密握手 —— 適合當第一次呼叫，**不適合**用在真實資料上。

progId 與型別的綁定寫在 `ProgramSettings.xml` —— 它是全框架的型別註冊表，不需要寫任何解析程式碼。建立 `Define/ProgramSettings.xml`：

```xml
<ProgramSettings>
  <Items>
    <ProgramItem ProgId="Echo" DisplayName="Echo"
                 BusinessObject="MyApp.Server.BusinessObjects.EchoBusinessObject, MyApp.Server" />
  </Items>
</ProgramSettings>
```

`BusinessObject` 是組件限定型別名。未列出的 progId 一律解析為框架預設的 `FormBusinessObject`，
因此**只有需要自訂邏輯的 progId 才要寫進來**。同一筆還可用 `Repository` 屬性綁定專屬的
Repository，兩個屬性彼此獨立。

啟動時框架會補上檔案中缺少的保留 progId 並寫回檔案（檔案不存在時就建立它），所以第一次執行後，
你會在 `Echo` 旁邊看到 `System`、`AuditLog` 與 `AuditRule` 三筆。詳見 [ADR-034](../../../maintainers/adr/adr-034-progid-type-registry.md)。

→ `Args` / `Result` 的命名規則與契約三層分離：[API ↔ BO 契約設計](../api/api-bo-contract-design.md)。哪些方法該放介面：[開發限制與反模式](../architecture/development-constraints.md)。

## 7. 由用戶端呼叫

以固定埠號啟動伺服器（`dotnet new web` 會在 `launchSettings.json` 隨機挑一個埠號）：

```bash
dotnet run --urls http://localhost:5050
```

.NET 端使用 `Polhem.Api.Client`，放在另一個專案：

```bash
dotnet new console -o MyApp.Client
cd MyApp.Client
dotnet add package Polhem.Api.Client
```

`Program.cs`：

```csharp
using Polhem.Api.Client;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages;

ApiClientInfo.ApiKey = "my-demo-key";

var connector = new FormApiConnector("http://localhost:5050/api", Guid.Empty, "Echo");
var result = await connector.ExecuteAsync<EchoResponse>(
    "Echo",
    new EchoRequest { Message = "hello" },
    PayloadFormat.Plain);

Console.WriteLine(result.Response);

public class EchoRequest
{
    public string Message { get; set; } = string.Empty;
}

public class EchoResponse
{
    public string Response { get; set; } = string.Empty;
}
```

`dotnet run` 會印出 `echo: hello`。

用戶端的 request / response DTO 請與伺服端的 `Args` / `Result` 分開宣告 —— 這才是第三方整合者看到契約的樣子，也能讓 wire 形狀誠實反映實際約定。

每次呼叫都帶 `X-Api-Key` 標頭。尚未發出任何 API key 時（`st_api_key` 中沒有啟用的 key），任何非空值都會被接受，
這正是 `AddPolhemApiKeyGateCheck` 在啟動時警告的狀況。一旦發出 key，就只接受已發出的 key。
→ [API 金鑰管理](../security/api-key-management.md)。

`PayloadFormat.Plain` 對應上面宣告的 `Public` + `Anonymous`。需要認證或加密的方法都得先 `Login`，由它發出 access token，並透過 RSA 握手交付 session 加密金鑰。

→ 前端無 .NET、以 JavaScript / TypeScript 呼叫：[JSON-RPC 前端整合指引](../api/jsonrpc-frontend-integration.md)。所有對外方法與其存取控制：[API 方法參考](../api/api-method-reference.md)。

## 8. 改用「定義」取代寫程式

上面的 Echo 物件是刻意手寫的 —— 它只是「證明管線通了」的最小單位。**一般 CRUD 完全不需要商業物件**：宣告一份 `FormSchema` 加上對應的 `TableSchema`，框架就會從定義產生 SQL、清單與存檔路徑。

這才是框架真正的重點，起點在此 → [定義檔全景](../definitions/definition-files-overview.md)，接著 [架構總覽](../architecture/architecture-overview.md)。

---

## 接下來讀什麼

| 你想 | 讀 |
|------|-----|
| 先理解設計再往下走 | [架構總覽](../architecture/architecture-overview.md) |
| 知道每個定義檔在管什麼 | [定義檔全景](../definitions/definition-files-overview.md) |
| 走完整條「定義 → API」流程 | [端到端開發指引](../guides/development-cookbook.md) |
| 不寫程式就完成欄位運算與驗證 | [運算式與規則](../definitions/expression-rules.md) |
| 加上認證與權限 | [權限與授權指南](../security/permission-authorization.md) |
| 把定義變更推送到線上資料庫 | [資料庫 Schema 升級](../database/database-schema-upgrade.md) |

上述內容的完整可執行版本在 [`samples/QuickStart.Server`](../../../samples/QuickStart.Server/README.zh-TW.md) 與 [`samples/QuickStart.Console`](../../../samples/QuickStart.Console/README.zh-TW.md)。若想看幾乎全以定義建成的完整應用，見 [`apps/Polhem.Northwind`](../../../apps/Polhem.Northwind/README.zh-TW.md)。
