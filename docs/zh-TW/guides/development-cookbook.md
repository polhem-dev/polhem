<!-- source: en/guides/development-cookbook.md blob: 90506d4cf8d39af6757d206f15b82c8ad2efa082 -->
# 端到端開發指引

[English](../../en/guides/development-cookbook.md) · [← 文件索引](../README.md)

> 本文件說明 Polhem 框架的核心開發流程，幫助開發者（與 AI Coding 工具）理解從定義到 API 的完整串接方式。

## 框架初始化順序

框架以標準 `IServiceCollection` DI 容器註冊；所有 framework 服務透過
ctor 注入解析，無靜態入口點（service locator）。

### Host 啟動流程

```text
┌─────────────────────────────────────────────────────┐
│ 1. paths = new PathOptions { DefinePath = "..." }   │
│ 2. settings = SystemSettingsLoader.Load(paths)      │
│ 3. SysInfo.Initialize(settings.CommonConfiguration) │
├─────────────────────────────────────────────────────┤
│ 4. services.AddPolhemFramework(                     │
│      settings.BackendConfiguration,                 │
│      paths,                                         │
│      autoCreateMasterKey: true)                     │
│    → 來自 Polhem.Hosting（composition root）        │
│    → 註冊 IDefineStorage / IDefineAccess /          │
│      ICacheContainer / IDbConnectionManager /       │
│      ISessionInfoService / ILanguageService /       │
│      IBusinessObjectFactory / JsonRpcExecutor       │
├─────────────────────────────────────────────────────┤
│ 5. 建出 provider（ASP.NET Core 用 builder.Build()， │
│    其他宿主用 services.BuildServiceProvider()）     │
│ 6. app.UsePolhemFramework()（僅 ASP.NET ——          │
│    啟動期檢查，不註冊 middleware 或 endpoint）      │
└─────────────────────────────────────────────────────┘
```

宿主套件選擇：

- **ASP.NET Core web host**：引用 `Polhem.Api.AspNetCore`（會透過遞移帶入 `Polhem.Hosting`）。啟動程式加上 `using Polhem.Hosting;`（取 `AddPolhemFramework`）與 `using Polhem.Api.AspNetCore;`（取 `UsePolhemFramework`）。`POST /api` 端點是你自己寫、繼承 `ApiServiceController` 的 controller，所以宿主還要呼叫 `AddControllers()` 與 `MapControllers()`。
- **非 ASP.NET Core 宿主**（Console / Worker Service / 在自己 process 內跑後端的桌面 app / 整合測試）：直接引用 `Polhem.Hosting`，不會拖入 `Microsoft.AspNetCore.App`。要在 process 內呼叫後端，就把建好的 provider 交給 client 端：傳給 connector 的建構子（`new SystemApiConnector(provider, accessToken)`），或在使用 `Polhem.UI.Core` 的 head 中指定給 `ClientInfo.LocalServiceProvider`。

`AddPolhemFramework` 也會註冊數個 hosted service，包括保留 progId 的啟動期註冊、跨 process 的 cache-notify poller，以及過期 session 的清理。它們只在 provider 屬於 .NET Generic Host（`WebApplication`、`Host.CreateApplicationBuilder`）時才會啟動；單純以 `BuildServiceProvider()` 建出的 provider 不會啟動它們。

參考實作：`tests/Polhem.Tests.Shared/TestProcessBootstrap.cs` — 以 `tests/Define/`
（process 啟動時與 embedded 框架預設合併後的結果）作為 `DefinePath` 套用同一流程。

### 首次 `DefinePath` 初始化

啟動流程的第一步要求 `DefinePath` 已存在框架自己的定義檔：`SystemSettings.xml`、
`DatabaseSettings.xml`、`DbCategorySettings.xml`、`st_*` TableSchema，以及框架的表單
（Department、Employee、AuditRule）連同其版面與語系檔；完整清單可用
`dotnet polhem defines list` 列出。這些檔以 embedded resource 形式 ship 在
`Polhem.Definition.dll` 內；消費者首次啟動前 materialize 一次到目標目錄即可。

```bash
# 一次性安裝框架 CLI（per-machine）
dotnet tool install -g Polhem.Cli

# materialize 框架預設到自家 DefinePath
dotnet polhem defines materialize --path ./Define

# 編輯 SystemSettings（設 MasterKeySource）+ DatabaseSettings（補連線字串）
# 然後啟動 app —— DefinePath 已就緒
```

CLI 是 `Polhem.Definition.Defaults.MaterializeTo(...)` 的 thin shell；宿主想在
code 內 materialize 可直接呼叫同一支 API，而 `tools/DefineEditor` 開啟資料夾時
也會呼叫它。已存在的檔案會被略過，除非傳入 `--overwrite`（程式碼中為
`MaterializeOptions.Overwrite`），所以重跑會保留你的客製。

完整檔案列表與消費者擴充指引見 [框架保留命名](../reference/framework-reserved-names.md)。

## 請求處理管線

### 完整請求流程

```mermaid
sequenceDiagram
    participant C as Client ApiConnector
    participant P as Provider Local/Remote
    participant S as Server ApiServiceController
    participant E as Executor JsonRpcExecutor
    participant B as Business Object

    C->>C: 建立 JsonRpcRequest method = ProgId.Action
    C->>C: Payload 轉換 Serialize Compress Encrypt
    C->>P: ExecuteAsync(request)

    alt Remote HTTP
        P->>S: POST /api Headers X-Api-Key，已登入時加 Bearer token
        S->>S: 驗證 Content-Type
        S->>S: 解析 JsonRpcRequest
        S->>S: 驗證 API key 與 Authorization header
        S->>E: ExecuteAsync(request)
    else Local 同進程
        P->>E: ExecuteAsync(request)
    end

    E->>E: 解析 Method 為 ProgId + Action
    E->>B: 建立 BO via BusinessObjectFactory
    E->>E: 解析 Action 對應的方法
    E->>E: ApiAccessValidator 驗證存取權限
    E->>E: 還原 Payload 解密 解壓 反序列化
    E->>E: 方法要求時檢查重放防護 frame
    E->>E: ApiInputConverter 轉換參數型別
    E->>B: 反射呼叫 Action 方法
    B-->>E: 回傳結果
    E->>E: ApiOutputConverter 依命名慣例轉為 API Response
    E->>E: 轉換 Payload 格式
    E-->>C: JsonRpcResponse
```

存取權限在 payload 解密**之前**驗證，所以不被允許的呼叫不會耗費任何解密成本（`JsonRpcExecutor.ExecuteAsync`）。
沒有 `Authorization` header 的請求是匿名呼叫：只有宣告為 `ApiAccessRequirement.Anonymous` 的方法會接受，
其餘方法回應 JSON-RPC 錯誤 `-32001`（Unauthorized）。

### Payload 格式

| 格式 | 處理流程 | 適用場景 |
|------|----------|----------|
| Plain | 無轉換 | Local 呼叫、開發除錯 |
| Encoded | Serialize → Compress | 一般 API 呼叫 |
| Encrypted | Serialize → Compress → Encrypt | 敏感資料傳輸 |

降級規則：connector 被要求用 Encrypted、但手上還沒有加密金鑰（登入前）時，改送 Encoded。
若方法的 `[ApiAccessControl]` 要求 Encrypted，該次呼叫就會被拒絕。使用 in-process provider 的
connector 除非 `SysInfo.IsDebugMode` 開啟，否則一律送 Plain。

## API 契約三層分離

框架將 API 型別分為三層，避免序列化屬性汙染商業邏輯：

### 層級對照

| 層級 | 組件 | 基底類別 | 特徵 |
|------|------|----------|------|
| Contract | Polhem.Api.Contracts | 無（純介面） | `ILoginRequest`、`ILoginResponse` 等 |
| API Type | Polhem.Api.Core | `ApiRequest` / `ApiResponse` | 實作 Contract 介面；不帶序列化屬性 —— MessagePack formatter 以手寫方式放在 `src/Polhem.Api.Core/MessagePack/` |
| BO Type | Polhem.Business | `BusinessArgs` / `BusinessResult` | 實作 Contract 介面，純 POCO |

### 型別轉換流程

```text
Client 發送 → LoginRequest (API Type，以該次請求的 codec 編碼)
    ↓ JsonRpcExecutor
    ↓ ApiInputConverter 屬性對應（{Action}Request → {Action}Args）
BO 接收 → LoginArgs (BO Type, POCO)
    ↓ 商業邏輯處理
BO 回傳 → LoginResult (BO Type, POCO)
    ↓ ApiOutputConverter 命名慣例推導（{Action}Result → {Action}Response）
Client 接收 → LoginResponse (API Type，以同一 codec 編碼)
```

### 關鍵元件

- **ApiInputConverter**（internal）：將 API Request 的屬性值對應到 BO Args（依屬性名稱匹配）。`Plain` body 以 `JsonElement` 抵達，會讀成該 action 的框架請求型別（`{Action}Request`；沒有對應型別時為方法的參數型別），再與解碼後的 body 一樣複製到 BO Args，因此無法設定契約未宣告的成員；`object` 型別的成員（例如篩選值）依其 JSON 種類綁定（字串、整數、小數、布林、陣列）
- **ApiOutputConverter**：執行後將 BO `{Action}Result` 以反射自動對應到 `{Action}Response`，結果以 `ConcurrentDictionary` 快取（詳見 [ADR-007](../../../maintainers/adr/adr-007-convention-based-type-resolution.md)）
- wire body 由該次請求宣告的 codec（`messagepack` 或 `json`）寫出，與輸出映射無關。見 [ADR-044](../../../maintainers/adr/adr-044-payload-codec-negotiation.md)。

## ExecFunc 自訂函式模式

ExecFunc 是框架提供的擴展機制，允許開發者新增自訂商業邏輯而不需修改框架核心。

### 開發步驟

框架自己的 handler 都是 internal。應用程式以自己的 handler 新增函式，並從綁定到該 progId 的
BO 分派過去。

#### 1. 撰寫 handler

handler 實作標記介面 `IExecFuncHandler`（`Polhem.Business`）。每個簽章為
`(ExecFuncArgs args, ExecFuncResult result)` 的 public 方法就是一個函式，方法名稱即 client 送出的 `FuncId`。
每個這樣的方法都要宣告 `[ExecFuncAccessControl]`（`Polhem.Business.Attributes`）：沒有宣告的方法在分派時
會被拒絕（`ExecFuncHandlerExtensions.InvokeExecFunc`），analyzer 規則 POLHEM3003 也會在建置時回報這項遺漏。
不得讓遠端 client 呼叫到的操作，設定 `LocalOnly = true`。

```csharp
using Polhem.Business;
using Polhem.Business.Attributes;
using Polhem.Definition.Collections;   // the ParameterCollection.Add extension
using Polhem.Definition.Security;

public sealed class CustomerFuncHandler : IExecFuncHandler
{
    [ExecFuncAccessControl(ApiAccessRequirement.Authenticated)]
    public void Greet(ExecFuncArgs args, ExecFuncResult result)
    {
        string name = args.Parameters.GetValue<string>("Name", "customer");
        result.Parameters.Add("Greeting", $"Hello, {name}");
    }

    // Maintenance operation: in-process callers only.
    [ExecFuncAccessControl(ApiAccessRequirement.Authenticated, LocalOnly = true)]
    public void RebuildBalances(ExecFuncArgs args, ExecFuncResult result)
    {
        // ...
        result.Parameters.Add("Rebuilt", true);
    }
}
```

#### 2. 從 BO 分派到 handler

`ExecFunc` 與 `ExecFuncAnonymous` 會呼叫 `BusinessObject` 的 protected virtual `DoExecFunc` /
`DoExecFuncAnonymous`。在綁定到該 progId 的 BO 中覆寫你需要的那一個（見下方「客製化 ProgId 對應的 BO」），
呼叫 handler，並傳入該入口點的存取要求與 `IsLocalCall`。handler 沒有宣告的 `FuncId` 交給基底實作處理，
框架自己的函式才仍然呼叫得到：

```csharp
protected override void DoExecFunc(ExecFuncArgs args, ExecFuncResult result)
{
    var handler = new CustomerFuncHandler();
    if (handler.GetType().GetMethod(args.FuncId) is null)
    {
        base.DoExecFunc(args, result);
        return;
    }
    handler.InvokeExecFunc(ApiAccessRequirement.Authenticated, IsLocalCall, args, result);
}
```

`System` progId 也是同樣做法，透過在 `ProgramSettings.xml` 綁定的 `SystemBusinessObject` 子類。

#### 3. Client 端呼叫

```csharp
// Form-level: the request goes to the business object of progId "Customer".
var connector = new FormApiConnector(endpoint, accessToken, "Customer");
var response = await connector.ExecFuncAsync(new ExecFuncRequest
{
    FuncId = "Greet",
    Parameters = new ParameterCollection { { "Name", "Contoso" } }
});
string greeting = response.Parameters!.GetValue<string>("Greeting");
```

系統層級函式以同樣方式走 `SystemApiConnector.ExecFuncAsync`。框架自己的系統函式 `UpgradeTableSchema` 與
`TestConnection` 都是 `LocalOnly`，所以只有在 process 內分派的 connector —— 以後端的 `IServiceProvider`
建構的那種 —— 才能執行它們：

```csharp
var sysConnector = new SystemApiConnector(serviceProvider, accessToken);
var upgrade = await sysConnector.ExecFuncAsync(new ExecFuncRequest
{
    FuncId = "UpgradeTableSchema",
    Parameters = new ParameterCollection
    {
        { "DatabaseId", "company01" },
        { "CategoryId", "company" },
        { "TableName", "ft_customer" }
    }
});
bool upgraded = upgrade.Parameters!.GetValue<bool>("Upgraded");
```

### 執行流程

```text
Client: await connector.ExecFuncAsync(new ExecFuncRequest { FuncId = "Greet" })
  → ApiConnector.ExecuteAsync<ExecFuncResponse>("ExecFunc", request)
  → JsonRpcRequest { method: "Customer.ExecFunc" }
  → JsonRpcExecutor 呼叫 CustomerBo.ExecFunc()        // BusinessObject.ExecFunc
  → CustomerBo.DoExecFunc()                           // 你的覆寫
  → handler.InvokeExecFunc(...)                       // ExecFuncHandlerExtensions
    → handler.GetType().GetMethod("Greet")            // 反射取得方法
    → 沒有 [ExecFuncAccessControl] → 拒絕
    → LocalOnly 且為遠端呼叫者 → 拒絕
    → 匿名入口點呼叫 Authenticated 方法 → 拒絕（-32001）
    → method.Invoke(handler, args, result)            // 反射呼叫
  → 回傳 ExecFuncResult
```

## FormSchema 驅動開發

FormSchema 是框架的定義中樞，同時驅動 UI、資料庫與驗證規則。

### 核心概念

```text
FormSchema（Single Source of Truth）
├── ProgId: "Customer"
├── DisplayName: "客戶"
├── CategoryId: "company"       ← 必填：common / company / log
├── Tables: FormTableCollection
│   ├── Master: FormTable（TableName 等於 ProgId）
│   │   ├── TableName: "Customer"
│   │   ├── DbTableName: "ft_customer"
│   │   └── Fields: FormFieldCollection
│   └── Detail: FormTable（明細表）
│       ├── TableName: "CustomerContact"
│       ├── DbTableName: "ft_customer_contact"
│       └── Fields: FormFieldCollection
│
├── → 衍生 TableSchema（資料庫維度）
├── → 設計階段衍生 FormLayout（UI 維度）
└── → 驅動 IFormCommandBuilder 家族（SQL 產生）
```

### CategoryId 與 DbCategory 路由

每個 FormSchema 必須指定 `CategoryId`，對應 `DbCategorySettings.xml` 中某個 `<DbCategory Id="...">` 的識別碼。它只接受 `common`、`company`、`log` 三個值：form repository 會把它對應到 `DbScope`，其他值一律拋出 `InvalidOperationException`（"Unknown schema.CategoryId"）。`CategoryId` 同時決定：

- 該 FormSchema 衍生的所有 `TableSchema` 應持久化於 `TableSchema/{categoryId}/` 子目錄
- 該 FormSchema 的資料表位於哪個資料庫（`CategoryId` → `DbScope` → `IRepositoryDatabaseRouter`）

業務資料表（`ft_*`）屬於 `company`，也就是各公司各自的資料庫。`common` 放跨公司共用的框架資料表，`log` 放稽核與異常 log。框架自帶 `Employee` 與 `Department` 表單（company 資料表 `st_employee` / `st_department`），所以應用程式不要重用這兩個 progId；見 [框架保留命名](../reference/framework-reserved-names.md)。

`SaveFormSchema` 會驗證 `CategoryId` 必填（透過 `TableSchemaGenerator.GetCategoryId(formSchema)`），未設定時拋出 `InvalidOperationException`。

### BO 方法中取得 DatabaseId

BO 方法**不應**寫死 `databaseId` 字串，也**不應**自行讀 `SessionInfo.CompanyId` / `CompanyInfo`。改用 `BusinessObject` 基底提供的 helper：

```csharp
// FormSchema-driven CRUD —— one-liner，自動路由
var repository = CreateDataFormRepository(ProgId);
// 等同於：
// Services.GetRequiredService<IRepositoryFactory>()
//         .CreateFormRepository<IDataFormRepository>(AccessToken, ProgId);

// 自訂 bo repo —— 取目標 scope 的 databaseId 再建 repo
var logDbId = ResolveDatabaseId(DbScope.Log);         // "log"（不需 session）
var companyDbId = ResolveDatabaseId(DbScope.Company); // 透過 session.CompanyId → CompanyInfo.CompanyDatabaseId
var repo = new MonthlySalesReportRepo(Services.GetRequiredService<IDbAccessFactory>(), companyDbId);
```

`DbScope` 解析規則：

| `DbScope` | 解析後 `databaseId` | 需要 session？ |
|-----------|---------------------|---------------|
| `Common` | 固定 `"common"` | 否 |
| `Log` | 固定 `"log"` | 否（Login / Logout 等 pre-EnterCompany 方法可寫 audit log） |
| `Company` | `SessionInfo.CompanyId` → `CompanyInfo.CompanyDatabaseId` | 是——沒有 session 時拋 `AuthenticationRequiredException`，尚未 `EnterCompany` 時拋 `CompanyNotEnteredException` |

詳見 [ADR-010 §「後續延伸：執行時路由」](../../../maintainers/adr/adr-010-logical-database-category.md) 與 [ADR-012](../../../maintainers/adr/adr-012-session-company-context.md)。

### 客製化 ProgId 對應的 BO

框架預設每個 ProgId 都以 `FormBusinessObject` 具現化。當特定表單需要超出 FormSchema 驅動 CRUD 的行為（客製驗證、領域事件、AnyCode SQL 等），繼承 `FormBusinessObject` 並透過 `ProgramSettings.xml` 綁定子類別。

#### 1. 繼承 `FormBusinessObject`

```csharp
namespace MyErp.Business;

public class CustomerBo : FormBusinessObject
{
    public CustomerBo(IBusinessObjectContext ctx, Guid accessToken, string progId, bool isLocalCall = false)
        : base(ctx, accessToken, progId, isLocalCall) { }

    // Override a Do* hook (see the next section) or add custom methods
    // exposed via [ApiAccessControl].
    protected override void DoBeforeSave(SaveContext context)
    {
        base.DoBeforeSave(context);
        // custom validation or computed values
    }
}
```

#### 2. 在 `ProgramSettings.xml` 綁定子類別

```xml
<ProgramItem ProgId="Customer"
             DisplayName="Customer Management"
             BusinessObject="MyErp.Business.CustomerBo, MyErp.Business" />
```

`BusinessObject` 使用 assembly-qualified 格式（`"Namespace.Type, AssemblyName"`）。未填時 resolver fallback 回 `FormBusinessObject`——只需要為「真的要客製」的 ProgId 填 `BusinessObject`。

#### 3. 解析行為

`ProgramSettingsBoTypeResolver`（由 `AddPolhemFramework` 註冊）讀取 `ProgramItem.BusinessObject`、透過 `AssemblyLoader` 載入型別、驗證繼承自 `BusinessObject`。**已宣告的名稱解析不出可用型別時一律拋例外**——型別載不到、或繼承不對，都是設定錯誤，沒有無害的解讀。退回換到的只有「看起來還在跑」：`FormBusinessObject` 接受任何 progId，一定建構成功，故障因此浮現得晚，症狀是「這支程式行為變成通用 CRUD」。

漸進採用仍然安全，因為**「沒宣告」不是失敗**：註冊表沒這筆 progId、或 `BusinessObject` 留空，仍如舊解析為 `FormBusinessObject`。只需要為真的要客製的 progId 填 `BusinessObject`。

**保留字 progId**（`ReservedProgIds.All`：`System`、`AuditLog`、`AuditRule`）適用同一策略，只是基底約束更緊：`System` 與 `AuditLog` 必須解析為該軸的框架物件或其子類，`AuditRule` 則須為 `FormBusinessObject`。它們沒有項目時，resolver 會使用框架自己的型別，而 Generic Host 啟動時會把缺少的項目寫進註冊表（`SaveProgramSettings`），既有的 `ProgramSettings.xml` 不需手動修改。

解析出的型別只有在「它所依據的 `ProgramSettings` 實例仍是快取中的那一個」時才會重用；檔案變更、定義快取載入新實例之後，下一次呼叫會重新解析。

### BO 擴充點與交易邊界

`Save` 與 `Delete` 各切成三段可覆寫的步驟。**要客製就覆寫其中一段，不要覆寫 public 方法**——
授權與 record-scope 檢查寫在 public 方法裡，覆寫它等於把那些檢查一併接手。

```text
Save:   DoBeforeSave  →  DoSave  →  [變更稽核]  →  DoAfterSave
Delete: DoBeforeDelete → DoDelete → [刪除稽核]  → DoAfterDelete
                          ↑
                    只有這一段在資料庫交易中
```

交易由 repository 在 `DoSave` / `DoDelete` 內部開啟並提交。其餘全部在交易外——**包含你在覆寫中
加在 `base.DoSave(context)` 前後的程式碼**。

這條邊界是刻意的：`DoBeforeSave` 會求值運算式、查 lookup、可能呼叫其他 BO，而 `DoAfterSave`
正是發通知、呼叫外部系統該待的位置。把交易撐過這些呼叫，等於讓鎖持有時間被外部延遲綁架，
連線池耗盡與分散式死結都由此而來。

#### 中止流程

丟 `UserMessageException`（`Polhem.Base.Exceptions`）——框架的業務流程中止訊號。它以
`JsonRpcErrorCode.UserMessage` 傳到用戶端並還原成同一型別，所以它的訊息會送到使用者面前。其他任何例外
對遠端呼叫者都只會呈現為固定的通用訊息。schema 驅動的規則引擎，其 `BeforeSave` 驗證規則走的也是同一個機制。

要讓訊息跟著使用者的語言，使用帶 key 的建構子：語系 key（`"{namespace}.{subKey}"`）、以英文撰寫的
composite format，以及其引數。

```csharp
protected override void DoBeforeSave(SaveContext context)
{
    base.DoBeforeSave(context);
    if (/* business condition fails */)
        throw new UserMessageException(
            "Customer.CreditLimitExceeded",
            "The credit limit of {0} has been exceeded.",
            creditLimit);
}
```

錯誤離開 server 之前，會以 session 的 culture 到部署的語系資源（namespace `Customer`、項目
`CreditLimitExceeded`）查這個 key，走的是與其他所有查詢相同的回退鏈（`LanguageFallback`：該 culture、
其父 culture，然後 `CommonConfiguration.DefaultLanguage`；但英文 culture 會停在英文文字）。回退鏈上沒有任何
culture 翻譯這個 key 時，就送出英文文字。不帶 key 的建構子則照原文送出訊息。

#### 三個必須納入設計的後果

**`DoBeforeSave` 的驗證有 TOCTOU 空窗。** 讀到「庫存足夠」之後、`DoSave` 執行之前，另一個交易
可能已把庫存扣光，而本次存檔照樣寫入。丟例外解決的是「怎麼中止」，不會讓檢查結果在寫入當下仍然
成立。**需要原子性的檢查要放進交易內或交給資料庫**——條件式 UPDATE、唯一索引，或 check constraint。
`DoBeforeSave` 的讀取只適合擋明顯錯誤的輸入，不能當並發防線。

**變更稽核與資料不是原子的。** 它寫在 `DoSave` 回傳之後，所以「資料寫成功、稽核寫失敗」有可能
發生。把稽核拉進交易會讓 `DoSave` 不只是持久化，且需要在 BO 層提供交易 API；框架選擇接受這個
落差。

**`DoAfterSave` 失敗時資料已經存進去了。** 例外會往上拋、該次呼叫回報失敗，但交易在這一段開始前
就已提交。放在這裡的副作用必須能被重試，或交給佇列而非同步執行——通知同步送出後失敗，就沒有任何
東西可以重試。

#### 邏輯必須與資料同交易時

`DataFormRepository.Save` 不是 virtual，也沒有提供把額外 statement 加進其交易的掛勾點；那個交易由
`DbAccess.UpdateDataTables` 開啟並提交。因此能與資料原子提交的，要不是由資料庫強制的條件（唯一索引、
check 與 foreign key constraint），就是由你自己掌握的寫入路徑：在客製 repository（見下方「為 ProgId 客製
Repository」）上寫一個方法，開連線、開 `DbTransaction`，每個 statement 都透過
`new DbAccess(connection, databaseType).Execute(command, transaction)` 執行，最後只提交一次 —— 框架自己的
repository 也是這個模式。再由覆寫的 `DoSave` 取代 `base.DoSave(context)` 呼叫它，並自行設定
`context.RefreshedDataSet` 與 `context.AffectedRows`。

### 業務 plugin

繼承是把 BO 換掉；**plugin** 是在既有的那個 BO 上加一段。客製屬於「追加」時用 plugin
——存檔前多一道檢查、存檔後發個通知；需要攔截或取代框架既有行為時才繼承。

| 需求 | 手段 | 能力 |
|------|------|------|
| 攔截或取代既有邏輯 | 繼承 BO 覆寫 `Do*` 子方法 | 可包夾 `base.DoXxx()` 前後，也可完全不呼叫 |
| 在既有邏輯之後追加 | plugin | 只有後置一個控點 |

兩者可疊著用：plugin 跑在該段**最終實作之後**，不論那是框架的還是客製子類的。

#### 怎麼寫

繼承 `FormBusinessPlugin`，override 這個 plugin 要跑的那**一個**時點。
**一個 plugin 只掛一個時點** —— 橫跨兩個時點的需求就是兩個類別。

```csharp
public class CreditLimitPlugin : FormBusinessPlugin
{
    public CreditLimitPlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
        : base(ctx, accessToken, progId) { }

    public override void BeforeSave(SaveContext context)
    {
        if (/* over the limit */)
            throw new UserMessageException(
                "Customer.CreditLimitExceeded", "The credit limit of {0} has been exceeded.", creditLimit);
    }
}
```

建構子接收每次呼叫的業務 context、access token 與 progId，其後可再宣告自己的相依——會由容器注入。

#### 四個時點

| 時點 | 執行位置 | 拿得到什麼 |
|------|---------|-----------|
| `BeforeSave` | 規則引擎之後、**稽核快照之前** | `SaveContext`，資料集仍可修改 |
| `AfterSave` | 持久化與變更稽核之後 | `SaveContext`，含 `RefreshedDataSet` 與 `AffectedRows` |
| `BeforeDelete` | guard 規則之後、刪除之前 | `DeleteContext`，含 `Snapshot` |
| `AfterDelete` | 刪除與刪除稽核之後 | `DeleteContext`，含 `Snapshot` 與 `RowsAffected` |

**`BeforeSave` 是 plugin 唯一能安全改資料的位置**：它在稽核快照與持久化之前，所以改動會被寫入
**也會**被稽核記到。到了 `AfterSave` 資料已存檔——改 `DataSet` 沒有作用，改 `RefreshedDataSet`
才會影響呼叫端收到的內容。

**四個時點全部在資料庫交易之外**，交易只涵蓋 `DoSave` / `DoDelete`。後果見上方
「BO 擴充點與交易邊界」。

#### 一個類別一個時點

類別必須恰好覆寫繫結宣告的那一個時點 —— 不多也不少。「存檔前檢查、存檔後動作」因此是兩個類別，
而且**兩者之間沒有共享狀態**：`After` 那個類別需要的東西必須自己重讀或重算。
這是「設定檔看得出誰跑在哪個時點」的代價。

plugin 按需建構 —— 一個 plugin 只在它自己那個時點第一次執行時才被建出來，否則完全不建，
所以一次 Save 不會建構只掛 delete 時點的 plugin。實例是每次操作各自的、不跨呼叫共用，
所以不需要考慮鎖。

#### 怎麼綁

plugin 依 progId、依租戶綁在 `{CustomizePath}/{customizeId}/PluginSettings.xml`。
**宣告順序即執行順序**，沒有 priority 數字。

```xml
<PluginSettings>
  <Items>
    <ProgramPluginItem ProgId="Order">
      <Plugins>
        <PluginItem Type="MyErp.Plugins.CreditLimitPlugin, MyErp.Plugins" Stage="BeforeSave" />
        <PluginItem Type="MyErp.Plugins.OrderSyncPlugin, MyErp.Plugins"   Stage="AfterSave" />
      </Plugins>
    </ProgramPluginItem>
  </Items>
</PluginSettings>
```

`Stage` 是必填的，而且建鏈時會與類別對帳：類別必須覆寫那個時點、且不覆寫其他時點。
**因此改變類別覆寫的時點時，必須連帶改這份檔案** —— 否則下一次解析就會拋例外，
訊息會指出類別實際覆寫的是哪一個。另一種做法是跑一個設定檔沒寫的時點，
而那正是這個宣告存在的目的所要避免的。

套裝層的 `{DefinePath}/PluginSettings.xml` 同樣會被讀取，兩層**相加**：套裝鏈先跑、租戶鏈後跑。
因此租戶**無法停用**套裝的 plugin——要拿掉套裝行為，請繼承 BO 覆寫該子方法。

租戶檔透過 `SystemBusinessObject.GetCustomizePluginSettings` / `SaveCustomizePluginSettings` 維護（client 端為
`SystemApiConnector.GetCustomizePluginSettingsAsync` / `SaveCustomizePluginSettingsAsync`）。兩者皆為
`LocalOnly`：這些綁定決定「哪些程式碼會在存檔與刪除流程裡執行」，所以維護工具跑在主機上、
in-process。儲存時會逐一驗證每筆繫結——型別必須可載入、繼承 `FormBusinessPlugin`、
且恰好覆寫該筆繫結宣告的那一個時點——一筆不合格就整份拒存。

#### 失敗，以及送往其他系統的副作用

丟例外會中止整個操作，與從 `Do*` 覆寫丟出完全一樣；要給使用者看的訊息用
`UserMessageException`。

在 `After` 時點資料已經提交，所以丟例外等於「對已存檔的資料回報失敗」。這對最常見的
「把異動同步到其他系統」影響最大：

| 可靠性要求 | 正確位置 |
|---|---|
| 不能漏（財務、庫存、對外承諾） | 透過你自己掌握的寫入路徑（見「邏輯必須與資料同交易時」），在與資料相同的交易內寫入 outbox 列，由背景 worker 送出 |
| 盡力而為，或有對帳作業兜底 | `AfterSave` / `AfterDelete` plugin 直接送 |

與其他系統往來的 plugin 也應自行判斷失敗是否值得中止使用者的作業。框架預設「丟出即中斷」是因為
驗證類 plugin 需要它——但別讓外部系統的可用性決定一筆資料能不能存檔。

#### plugin 與 schema 規則的分界

兩者都在擴充表單行為，分界值得寫明：

| | schema 規則（`FormSchema`） | plugin |
|---|---|---|
| 存放於 | 表單定義內——**不可客製** | `PluginSettings.xml`——依租戶 |
| 寫法 | 宣告式運算式 | 編譯後的型別 |
| 適用 | 欄位預設值、計算欄、驗證 | 跨表、跨系統的副作用 |
| 部署方式 | 改定義檔 | 交付組件 |

### 為 ProgId 客製 Repository

資料存取以同樣方式綁在同一筆註冊表項目上。繼承 `DataFormRepository`，把 BO 需要的成員宣告在擴充自 `IDataFormRepository` 的介面上，再於 `ProgramItem.Repository` 指名該型別：

```csharp
public interface IOrderRepository : IDataFormRepository
{
    string GetStoredStatus(Guid rowId);
}

public sealed class OrderRepository : DataFormRepository, IOrderRepository
{
    public OrderRepository(IRepositoryContext ctx, Guid accessToken, string progId)
        : base(ctx, accessToken, progId) { }

    public string GetStoredStatus(Guid rowId)
    {
        var spec = new DbCommandSpec(DbCommandKind.Scalar,
            "SELECT status FROM ft_order WHERE sys_rowid = {0}", rowId);
        return CreateDbAccess().Execute(spec).Scalar as string ?? string.Empty;
    }
}
```

```xml
<ProgramItem ProgId="Order"
             DisplayName="Orders"
             BusinessObject="MyErp.Business.OrderBo, MyErp.Business"
             Repository="MyErp.Repositories.OrderRepository, MyErp.Repositories" />
```

BO 端以自己的介面取得它，免 cast、也不需指名資料庫——綁定來自註冊表，路由來自 form schema 的 `CategoryId`：

```csharp
private IOrderRepository Repository() => CreateFormRepository<IOrderRepository>();
```

**與 `BusinessObject` 一樣，`Repository` 型別載不到是直接拋。** 資料存取沒有無害的降級模式：退回等於讓這支程式的讀寫改跑作者刻意替換掉的通用 SQL，而故障會延後到資料已經錯了的時候才浮現。留空則是「沒宣告」，照舊用框架自己的 repository。

子類可以有自己的相依——工廠以 `ActivatorUtilities` 建構它，介面型別的建構子參數會自 DI 注入。但**不得**再宣告第二個 `string` 或 `Guid` 參數，那兩個型別已被工廠的引數佔用。

子類只能新增成員，不會改變繼承來的成員。`DataFormRepository` 的 `GetList`、`GetData`、`Save`、`Delete` 等 `IDataFormRepository` 成員都不是 virtual。子類自己的成員以 protected 的 `CreateDbAccess()`、`DatabaseId` 與 `Context`（連線管理、cache-notify 服務）為基礎撰寫。

### FormSchema → SQL 產生

```text
FormApiConnector.GetListAsync(...)
  → FormBusinessObject.GetList 處理請求
  → 綁定到該 progId 的 DataFormRepository 執行查詢
    → IFormCommandBuilder，依連線的資料庫類型由
      DbDialectRegistry.Get(databaseType).CreateFormCommandBuilder(formSchema, defineAccess) 建立
    → BuildSelect(tableName, selectFields, filter, sortFields, ...)
      → SelectCommandBuilder.Build(...)
        → SelectBuilder: 產生 SELECT 欄位清單
        → FromBuilder: 產生 FROM 子句（為關聯欄位加上 JOIN）
        → WhereBuilder: 從 FilterNode 樹產生 WHERE 子句
        → SortBuilder: 產生 ORDER BY 子句
    → 回傳參數化的 DbCommandSpec
  → DbAccess.Execute(spec) 執行查詢
```

### FilterCondition 查詢建構

```csharp
// Build a filter: all three conditions must hold
FilterNode filter = FilterGroup.All(
    FilterCondition.Equal("region", "North"),
    FilterCondition.Contains("sys_name", "Trading"),
    FilterCondition.Between("credit_limit", 30000m, 80000m));

var list = await formConnector.GetListAsync(selectFields: "sys_id,sys_name", filter: filter);
```

`FilterGroup.All` 以 AND 組合、`FilterGroup.Any` 以 OR 組合；group 可以巢狀。`FilterCondition` 提供
`Equal`、`NotEqual`、`Contains`、`StartsWith`、`EndsWith`、`Between` 與 `In` 的 factory 方法；`ComparisonOperator`
的其他運算子（例如 `GreaterThan`）透過建構子
`new FilterCondition(fieldName, ComparisonOperator.GreaterThan, value)` 建立。欄位名稱是表單的欄位名稱，
而 `GetList` 會拒絕對表單未宣告的欄位做篩選或排序。

## 數值語意、公司小數位與捨入

數值欄位在 `FormField` 上宣告一個語意化的 **`NumberKind`**（會傳遞到 `LayoutFieldBase`）。這個 kind 驅動三件事 —— 顯示格式、寫入時是否捨入、以及小數位數的來源。各成員、框架預設值，以及設計理由（為何 round-then-sum、為何金額於執行時解析、為何 DB scale 與此正交）為已簽核的合約，見 [ADR-026](../../../maintainers/adr/adr-026-numeric-semantics-rounding.md)。

| `NumberKind` | 捨入策略 | 小數位來源 | 框架預設 | 用途 |
|-------------|---------|-----------|:-------:|-----|
| `Quantity` / `Weight` | `Round` | `Unit`（必須綁 `UnitField`） | 0 / 3 | 數量、重量 |
| `Amount` | `Round` | `Currency`（回退至公司） | 2 | 金額、稅額、合計 |
| `Percent` | `Round` | `Company` | 2 | 百分比 |
| `UnitPrice` / `Cost` | `Preserve` | `Company`（僅顯示用） | 4 | 單價、成本 |
| `ExchangeRate` | `Preserve` | `SystemFixed` | 5 | 匯率 |

### 兩條容易寫錯的規則

- **Round-then-sum（合計不變量）。** 對 `Round` 類 kind，合計必須等於**已個別捨入的明細之和**，絕不是全精度加總後才在最後捨入一次。每筆明細先以 `NumberFormatResolver.RoundByKind(value, kind, company)` 捨入 —— 金額與數量／重量則用參照感知的 `RoundByKind(value, kind, ctx, refCode)`，並傳入其幣別或單位代碼（見下）—— 再把已捨入的值相加。這保證 `Σ 明細 == 合計`。
- **`Preserve` 絕不寫入捨入後的值。** `UnitPrice` / `Cost` / `ExchangeRate` 以輸入精度儲存；其小數位僅供顯示。`RoundByKind` 對這些值原樣返回。對來源值捨入會把誤差注入下游 —— 不要這麼做。（就 API 匯入而言，唯一的硬邊界是 DB scale；見 [ADR-026](../../../maintainers/adr/adr-026-numeric-semantics-rounding.md) 中的持久化邊界決策 D6。）

### 顯示格式於交付時烘焙（bake）

定義 API 照原樣供應 `FormSchema`，烘焙由消費端負責：在 .NET 各 head 中，由 `FormDefinitionLoader.GetLocalizedSchemaAsync`（`Polhem.Api.Client`）複製（clone）快取的 schema 並呼叫 `NumberFormatApplier.Bake(clone, company)`，對每個來源為公司或系統、且沒有明確格式的 `NumberKind` 欄位設定 `FormField.NumberFormat`（例如 `"N2"`、`"P4"`、`"N5"`）。金額與數量／重量不烘焙，而是逐列依其幣別或單位解析。作者自行提供的 `NumberFormat` 永遠優先。快取的 schema 絕不被異動 —— 烘焙只在每次呼叫的 clone 上執行（見該方法的 remarks）。

由於格式是從 session 公司的小數位解析而來，同一份 schema 交付給兩家公司可能帶有不同格式（例如 `Percent` 為 `P2` vs `P4`）。`SystemFixed` 類 kind（`ExchangeRate`）忽略任何公司覆寫，永遠使用框架預設。

### 多幣別：金額於執行時依其幣別解析

`Amount` 的小數位跟隨**幣別**而非公司（JPY = 0、USD = 2、BHD = 3 —— 類似 SAP TCURX）。幣別主檔為系統層級定義 **`CurrencySettings`**（`DefineType.CurrencySettings`，精選的 ISO 4217 表；每個 `CurrencyItem` 帶有一個 `Rounding` 自然最小單位，小數位由此導出）。它透過一般的 `GetDefine` 通道送達 client；主檔缺漏也沒關係 —— 金額此時回退至框架預設 2。

每個金額欄位透過 `FormField.CurrencyField` 綁定一個**幣別 key 欄位**（SAP CUKY）；主單據幣別位於 `FormSchema.CurrencyField`（慣例為 `sys_currency`）。金額幣別的解析優先序為：**明確的 `CurrencyField` → 主檔 `sys_currency` → 公司 `DefaultCurrency`**。公司本幣為必填 —— 公司沒有本幣時，解析器會擲 `InvalidOperationException` 而不是自行猜測；只有完全沒有公司上下文時（例如尚未進入公司），金額才回退至框架預設 2。明細金額欄位讀取主列的幣別。交付時，`Bake` **不烘焙** `Amount` 格式（其小數位依執行時的幣別值而定 —— UI 逐列解析）；改為把有效的幣別參照欄位標記到每個金額欄位上，讓 UI 知道要監看哪個欄位。

伺服器端捨入使用帶 `RoundingContext`（`Company` + `CurrencySettings`）的幣別感知多載：

- **逐明細：** `NumberFormatResolver.RoundByKind(value, NumberKind.Amount, ctx, currencyCode)` 捨入至該幣別的自然小數位。照常 round-then-sum —— 原幣與本位幣金額各自獨立捨入至其幣別。
- **本位幣：** `home_amount = RoundByKind(amount × rate, Amount, ctx, homeCurrency)` —— 已捨入的原幣金額乘上全精度（preserve）匯率，再捨入至本位幣的小數位。本位幣預設為 `CompanyInfo.DefaultCurrency`。
- **最終現金捨入（選用）：** `RoundCash(total, currencyCode, ctx)` 把最終應付金額對齊到公司的逐幣別現金捨入單位（SAP T001R、`CompanyInfo.CashRounding`，例如 CHF → 0.05）；未覆寫時維持該幣別的自然單位（不額外捨入）。刻意產生的差額 `payable − total` 由呼叫端記入捨入科目。

幣別小數位為**系統層級**（在 `CurrencySettings` 中）；只有**現金捨入單位**可由公司覆寫（`CompanyInfo.CashRounding`）。逐公司的 `CompanyInfo.AllowedCurrencies` 白名單限定一張單據可選用哪些幣別（空 = 所有系統幣別）。

### 計量單位：數量／重量於執行時依其單位解析

`Quantity` / `Weight` 的小數位跟隨**計量單位**而非公司（KG = 3、PCS = 0 —— 類似 SAP T006），與金額對幣別完全平行。單位主檔為系統層級定義 **`UnitSettings`**（`DefineType.UnitSettings`，精選表；每個 `UnitItem` 直接儲存其 `Decimals`）。它透過一般的 `GetDefine` 通道送達 client；主檔缺漏則回退至框架預設。

每個數量／重量欄位**必須**透過 `FormField.UnitField` 綁定一個**單位欄位**（SAP UNIT）（沒有主檔層級的單位 —— 單位是逐列的，單位欄從同一列讀取）。不需要單位的數值（例如單位隱含在語意裡的件數）是一般數值欄位，不標 `NumberKind`。`NumberFormatResolver` 不會從公司取這些小數位：公司有本幣可以回退，但沒有預設單位。

| 情況 | `RoundByKind(value, kind, ctx, unitCode)` |
|------|------|
| 單位代碼在 `UnitSettings` 中找得到 | 捨入至該單位的小數位 |
| 該列的單位代碼為空 | 原值返回，不捨入 |
| 單位代碼不在 `UnitSettings` 中，或未部署單位主檔 | 捨入至該 kind 的框架預設 |
| 數量／重量計算欄沒有 `UnitField` | `FormExpressionCalculator` 擲 `InvalidOperationException` |

`Bake` 從不烘焙數量／重量欄位。伺服器端捨入傳入攜帶 `UnitSettings` 的 `RoundingContext`；round-then-sum 逐單位成立（混合單位的欄不存在有意義的合計）。Grid 與 `NumericEdit` 以與幣別相同的方式逐格／逐列解析單位。`AmountColumnSummary` 可以為混合單位的頁尾合計設限，但 Grid 本身沒有頁尾 —— 由宿主自行接上（見 DemoCenter 的 `MultiUnitModule`）。

### DB 儲存精度是容量上限，不是顯示／計算設定

數值欄位使用 `Decimal`，搭配單一框架層級的高 scale（例如 `Scale=8`），與任何公司或幣別小數位無關 —— 因此沒有逐公司／逐幣別的 `ALTER`。顯示小數位（`NumberFormat`）與計算小數位（`RoundByKind`）與 DB scale 正交；scale 只限定該欄位能容納多少精度。

## 跨 process 快取失效

in-process 快取（`Polhem.ObjectCaching`）在發生寫入的那個 process 會即時失效（`SaveX → Remove()`）。要把失效傳播到**其他 process / 節點** —— 多節點部署、以及由資料庫載入的快取（如 `CompanyInfo`，或 `DbDefineStorage` 下的定義）需要此能力 —— 使用資料庫通知機制。設計理由見 [ADR-017](../../../maintainers/adr/adr-017-db-cache-invalidation.md)，完整機制見[快取機制](caching.md)；本節講實務用法。

### 讓快取可被失效 —— 不用做任何事

建立在 `ObjectCache<T>` 或 `KeyObjectCache<T>` 上的快取，預設就會參與跨 process 失效。`GetPolicy()` 沒有設定 `ChangeNotifyKey` 時，基底類別會套用 `"{CacheGroup}:*"`（單物件快取）或 `"{CacheGroup}:{key}"`（鍵值快取），其中 `CacheGroup` 預設為被快取型別的名稱。帶有該 key 的條目會取得一個綁定「該 key 已發布版本」的到期 token。

只有當快取要監聽別的 key 時，才需要自己設定：

```csharp
policy.ChangeNotifyKey = changeSource.NotifyKey;
```

### 觸發失效 —— 在同一 transaction 內 bump

當寫入端改動了「對某快取有意義」的來源資料，就在**改資料的同一 transaction** 內 bump 通知列：

```csharp
// "群組:實體" key，群組須等於目標快取的 CacheGroup（預設為其型別名）
_cacheNotify.Touch($"CompanyInfo:{companyId}", transaction, databaseType);
```

`"群組:實體"` key 的慣例：

- **群組** = 該快取的 `CacheGroup`，除非快取覆寫它，否則就是被快取型別名（`CompanyInfo`、`FormSchema`、`LanguageResource`…）。
- **實體** = 與該快取 `Remove` 所用的 key 完全一致。單鍵快取直接傳該 key（`progId`、`layoutId`）；複合鍵快取用**點**形式（`TableSchema` → `"common.st_user"`、`LanguageResource` → `"zh-TW.common"`）；單物件快取用 `"*"`（`"DbCategorySettings:*"`）。

> ⚠️ bump **必須**與資料變更在同一 transaction 提交。分開提交會讓 poller 在資料可見前就看到新版本 → reload 讀到舊值並標記新鮮 → 永久 stale。`DbDefineStorage.SaveX` 已如此處理；自訂 repository 要把自己的寫入 `DbTransaction` 傳給 `Touch`（服務為 `ICacheNotifyService`，repository 可由 `Context.CacheNotify` 取得；宿主未註冊時為 `null`）。

### 失效如何傳到其他節點

各節點的 `CacheNotifyPoller`（hosted service）每 `IntervalSeconds` 輪詢 `st_cache_notify`，找出 `cache_version` 變大的 key（以 `sys_update_time` 增量抓取、以 version 冪等判定），並經 `CacheInfo.NotifyVersions` 發布新版本。`ChangeNotifyKey` 相符的條目會發現版本與自己擷取時不同，於下次讀取時過期並從來源 lazy 重載。不推送、不主動觸碰任何條目：每個節點各自輪詢同一張表。

### 設定（`BackendConfiguration.CacheNotifyOptions`）

| 鍵 | 預設 | 說明 |
|----|------|------|
| `Enabled` | `true` | 註冊 poller。純**單一 process** 單節點可停用（本地寫入即時失效）。同機多 process 仍需要。 |
| `IntervalSeconds` | `5` | 輪詢間隔；實質是跨節點失效延遲。每輪只是一筆走索引、多回 0 列的查詢，負載可忽略 —— 依延遲容忍度調，而非成本。 |
| `MarginSeconds` | `5` | 增量重疊回看，cover 長交易邊界情況。 |
| `DatabaseId` | `common` | 被輪詢的 `st_cache_notify` 所在資料庫。 |

> 本機制**只用資料庫伺服器時鐘**（從不用 app 端時鐘）且全程不轉時區，故不受主機時區影響。將資料庫伺服器設為 **UTC**，存入的 `sys_update_time` 即為 UTC（見 [ADR-017](../../../maintainers/adr/adr-017-db-cache-invalidation.md)）。

## Frontend API 連線模式

兩個 .NET UI 家族消費 API 的方式結構不同。設計理由見 [ADR-013](../../../maintainers/adr/adr-013-frontend-api-connection-strategy.md)，本節說明各自的**實際使用方式**。各家族能跑在哪些平台、瀏覽器或行動裝置 head 需要自行設定什麼，見 [平台支援](../getting-started/platform-support.md)。

### 決策樹

> 你的前端屬於哪類？

```
你的前端是什麼？
│
├── Avalonia（桌面、瀏覽器、iOS、Android），或你自己的 WinForms / WPF host
│   → 使用 Polhem.UI.* family，透過 ClientInfo static singleton
│   → 參考下方「Polhem.UI.* head」章節
│
├── Blazor Server（ASP.NET Core server-rendered）
│   → 使用 Polhem.Web.Blazor.Server，connector 以 circuit 為 scope
│   → 參考下方「Blazor Server」章節
│
└── 沒有 .NET（JavaScript、TypeScript…）
    → 直接呼叫 JSON-RPC 端點
    → 參考 JSON-RPC 前端整合指引
```

> 框架提供兩個 UI 套件：`Polhem.UI.Avalonia` 與 `Polhem.Web.Blazor.Server`。
> 其他 .NET 前端——WinForms、WPF、你自己的 Blazor WebAssembly app——沒有對應的框架 UI 套件，
> 直接使用 `Polhem.UI.Core`（`ClientInfo`）或 `Polhem.Api.Client`。沒有 .NET 的 client 依
> [JSON-RPC 前端整合指引](../api/jsonrpc-frontend-integration.md)。

### Polhem.UI.* head（ClientInfo）

這些 head 透過 `Polhem.UI.Core.ClientInfo` static singleton 管理連線狀態，適用於「一個 process = 一個使用者」的環境。以下程式碼在所有使用它的 head 都一樣：Avalonia 的桌面、瀏覽器（WebAssembly）、iOS 與 Android head，以及你自己的 WinForms 或 WPF host。各平台不同的是設定——瀏覽器中 endpoint 與 API key 的儲存方式、發佈設定、適用哪些連線類型——這些見 [平台支援 § 瀏覽器或行動裝置 head 的檢查清單](../getting-started/platform-support.md#3-瀏覽器或行動裝置-head-的檢查清單)。Local 連線（後端在同一個 process）是桌面才有的選項；瀏覽器與行動裝置 head 走 Remote。

**1. App 啟動時呼叫 `InitializeAsync`**：

```csharp
// MyApp/Program.cs (or App.axaml.cs, etc.)
using Polhem.Api.Client;
using Polhem.UI.Core;

// 1. Implement IUIViewService (provides the connection settings dialog)
public sealed class MyUIViewService : IUIViewService
{
    public async Task<bool> ShowApiConnectAsync()
    {
        // Ask the user for the endpoint with your own dialog (an Avalonia view, a WinForms Form, …).
        string? endpoint = await ShowEndpointDialogAsync();
        if (endpoint is null) { return false; }      // the user cancelled
        await ClientInfo.SetEndpointAsync(endpoint);  // validates, connects and stores it
        return true;
    }
}

// 2. Initialize at startup — the accessors are asynchronous end-to-end, so await it.
var supportedConnectTypes = SupportedConnectTypes.Remote; // Both allows Local as well (desktop only)
if (!await ClientInfo.InitializeAsync(new MyUIViewService(), supportedConnectTypes))
{
    // The user cancelled connection setup; exit the app.
    return;
}
```

`InitializeAsync` 從 `ClientInfo.EndpointStorage` 讀出已儲存的 endpoint，驗證後初始化系統 connector；endpoint 不存在或連不上時，呼叫 `IUIViewService.ShowApiConnectAsync()`。預設的儲存是 `FileEndpointStorage`（`Polhem.UI.Core`），把 endpoint 與 API key 存在使用者本機應用程式資料目錄下、以進入點組件命名的資料夾中。命令列引數 `Endpoint=<url>` 或 `ApiKey=<key>` 會在該次執行中覆蓋已儲存的值。瀏覽器 head 要同時替換 `ClientInfo.EndpointStorage` 與 `ClientInfo.ApiKeyStorage`，因為 WebAssembly 沒有可持久保存的檔案系統。

**2. 登入、套用結果、進入公司**：

```csharp
var loginResponse = await ClientInfo.SystemApiConnector.LoginAsync(userId, password);
ClientInfo.ApplyLoginResult(loginResponse);
// ClientInfo.AccessToken / UserInfo are now populated

var enterResponse = await ClientInfo.SystemApiConnector.EnterCompanyAsync(companyId);
ClientInfo.ApplyEnterCompanyResult(enterResponse);
// ClientInfo.Company / Capabilities are now populated
```

`ApplyLoginResult` 也會把 server 回傳的 culture —— 使用者的 `st_user.culture`，或部署的 `CommonConfiguration.DefaultLanguage` —— 設為此 process 目前與預設的 culture。框架自己的 UI 文字、在地化後的定義標題，以及數字與日期的顯示和輸入都讀它，所以帳號設定為 `en-US` 的使用者在中文作業系統上看到的是英文。`CategoryId` 為 `company` 的表單要先 `EnterCompany`；在那之前，它們的呼叫會以 `CompanyNotEnteredException` 失敗。

**3. 透過 `ClientInfo` 取得 connector 呼叫 API**：

```csharp
// System-level API
PingResponse ping = await ClientInfo.SystemApiConnector.PingAsync();

// Form-level API (FormBO)
var formConnector = ClientInfo.CreateFormApiConnector("Customer");
var listResult = await formConnector.GetListAsync(selectFields: "sys_id,sys_name");

// Definition data (FormSchema, TableSchema, etc.), exactly as stored
FormSchema schema = await ClientInfo.DefineAccess.GetFormSchemaAsync("Customer");
```

沒有傳分頁選項的 `GetListAsync` 回傳第一頁、最多 `PagingOptions.MaxPageSize` 筆；要取得更多，傳入 `PagingOptions` 逐頁讀取。`ClientInfo.DefineAccess` 照原樣回傳定義，不做在地化，也不套租戶覆寫層。Avalonia 的 `FormView`、`ListView` 與 `LookupDialog` 則改由 `ClientInfo.DefinitionLoader` 載入定義——使用者語言的標題、租戶客製層與公司的數值格式——程式碼也可以自己呼叫 `ClientInfo.DefinitionLoader.GetLocalizedSchemaAsync(progId, CultureInfo.CurrentUICulture.Name)`。`ClientInfo.UseDefinitionLoader` 預設為開啟；啟動時設為 `false` 會照原樣呈現定義，省下載入器額外的往返。

**4. 切換 endpoint（使用者更換 server）**：

```csharp
await ClientInfo.SetEndpointAsync("https://new-server.example.com/api");
// Clears AccessToken, so the user signs in again.
```

`SetEndpointAsync` 驗證 endpoint、切換連線類型、清除 access token 與使用者時區、初始化系統 connector，並透過 `ClientInfo.EndpointStorage` 儲存 endpoint。它不會登入：要再呼叫一次 `LoginAsync` 與 `ApplyLoginResult`。

### Blazor Server（Polhem.Web.Blazor.Server）

Blazor Server 透過 ASP.NET Core DI 建立 connector。**每個 SignalR circuit 一個 DI scope** —— `AddPolhemBlazor` 把 connector factory 與 `ApiSessionContext` 註冊為 scoped —— 避免 cross-user data leak。Blazor Server 跑在 server process 內，所以其他 head 的裁剪與平台問題不適用於它。

**1. `Program.cs` 註冊**：

```csharp
using Polhem.Hosting;                               // AddPolhemFramework
using Polhem.Web.Blazor.Server.DependencyInjection; // AddPolhemBlazor

var builder = WebApplication.CreateBuilder(args);

// Backend services (IDbConnectionManager / IDefineAccess / BO, etc.) — the Local provider dispatches to them
builder.Services.AddPolhemFramework(backendConfiguration, pathOptions);

// Polhem.Web.Blazor.Server services: options, connector factory, the components' UI text
builder.Services.AddPolhemBlazor(options => options.UseLocalProvider());

// Standard Blazor Server setup
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
```

> 同時對外提供 `POST /api` 端點的 Blazor 宿主——繼承 `ApiServiceController` 的 controller，加上
> `AddControllers()` 與 `MapControllers()`——也要在它讀取的資料庫建好之後呼叫 `app.UsePolhemFramework()`。
> `UsePolhemFramework` 不註冊任何 middleware，也不註冊 endpoint；它執行啟動期檢查，目前會在 `st_api_key`
> 沒有任何啟用中的 key 時記錄一筆 error（Development 環境為 warning），因為此時 `X-Api-Key` header 只檢查是否存在。
> 見 [API 金鑰管理](../security/api-key-management.md)。

**2. 在 Razor component 中建立 connector**：

```razor
@page "/customers"
@using Polhem.Api.Core.Messages.Form
@using Polhem.Web.Blazor.Server.DependencyInjection
@inject PolhemApiConnectorFactory ConnectorFactory

<h3>Customers</h3>

@code {
    // Cascaded by a PolhemAccessTokenProvider around the page.
    [CascadingParameter] public Guid AccessToken { get; set; }

    private GetListResponse? listResult;

    protected override async Task OnParametersSetAsync()
    {
        if (AccessToken == Guid.Empty) { return; }
        var formConnector = ConnectorFactory.CreateFormConnector(AccessToken, "Customer");
        listResult = await formConnector.GetListAsync(selectFields: "sys_id,sys_name");
    }
}
```

`PolhemAccessTokenProvider` 保存 circuit 的 access token 並以 cascading 方式傳遞；`PolhemLoginPanel` 負責登入並把 token 交給它；`FormPage` 依 `ProgId` 呈現整張表單。[`samples/Blazor.Server.Demo`](../../../samples/Blazor.Server.Demo/README.zh-TW.md) 把三者接在一起。元件自己的文字來自 `AddPolhemBlazor` 註冊的 `IStringLocalizer<PolhemUIText>` —— 一個 `LanguageResourceStringLocalizer`，依 circuit 目前的 UI culture，先讀宿主的語系資源，再讀框架內附的翻譯。宿主若自行為 `PolhemUIText` 註冊 localizer，會保留宿主的那一個。

`FormPage` 載入定義的方式與 Avalonia 畫面相同：經由 `FormDefinitionLoader`，由 `PolhemApiConnectorFactory.CreateDefinitionLoader` 為每個頁面建立，因此標題依 circuit 的 UI culture 呈現，租戶客製的版面也會套用。在 `AddPolhemBlazor` 中設定 `options.UseDefinitionLoader = false` 會照原樣呈現定義；把 loader 傳給頁面的 `DefinitionLoader` 參數，則可改變單一頁面的組裝方式（例如提供 `CompanyAccessor`，套用公司的數值格式）。儲存前，`FormPage` 會檢查標記為 `Required` 的欄位；只要有空白，就在工具列上方列出這些欄位，且不送出儲存。Avalonia 的 `FormView` 也會這樣做，訊息顯示在它的錯誤列。

**3. Local vs Remote 模式**：

模式在 `AddPolhemBlazor` 選定，`PolhemApiConnectorFactory` 依此建立每一個 connector：

- **Local mode（in-process）**—— `options.UseLocalProvider()`，預設值：元件與後端共用同一個 ASP.NET Core process，connector 經 `LocalApiProvider` 分派，沒有 HTTP。**每次呼叫都是受信任的 local 呼叫**：access token 檢查與 `LocalOnly` 限制都會略過。只有在網站的每個使用者都可以看到整個後端時才使用，例如內部管理工具。
- **Remote mode（HTTP）**—— `options.UseRemoteProvider("https://api.example.com/api")`：後端在另一個 process 或 server，connector 走 `RemoteApiProvider`，每次呼叫都和其他 API client 一樣接受檢查。此時 Blazor 宿主不需要 `AddPolhemFramework`，但必須在第一次呼叫前把 `Polhem.Api.Client.ApiClientInfo.ApiKey` 設為伺服器核發給此應用程式的 key：`RemoteApiProvider` 會把這個行程共用的值當作 `X-Api-Key` 標頭送出，`UseRemoteProvider` 本身不接受 key，而沒有 key 時伺服器對 `System.Ping` 以外的每個方法都回應 `401 Unauthorized`，所以最先失敗的是登入。key 識別的是應用程式而不是使用者，因此所有 circuit 共用同一把。

### Avalonia（Polhem.UI.Avalonia）

`Polhem.UI.Avalonia` 歸 **`Polhem.UI.*` family**，所以在每一個 Avalonia head 上，連 API 的方式都與上方「Polhem.UI.* head」章節相同 —— 透過 `ClientInfo` static singleton，per-process 一個 token。

內含 FormSchema 驅動控制項：`FormView` 單筆、`ListView` 清單、`GridControl` 表格，加上一組 field editor 與 `FormScope` ambient 綁定，皆以 `FormDataObject` 為資料中樞。套件目標為 `net10.0`；平台專屬的 target framework 屬於各 head 專案。

```csharp
// Avalonia desktop head — configure ClientInfo BEFORE any UI control instantiates.
public static void Main(string[] args)
{
    ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Remote;
    // The shipped key seeds empty storage on first run; after that the stored value wins.
    ClientInfo.ApplyApiKey("my-app-key");

    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
}
```

`FormView` 在 host 只設 `ProgId` 時自動向 `ClientInfo` 取得 `Schema` / `FormConnector` / `AccessToken`。`GridControl`（`ContentControl` 組合式控件，內部 `DataGrid` 以 `InnerGrid` 公開）的 cell 走 `DataGridTemplateColumn` + `FuncDataTemplate<DataRowView>` + code-fetch（**不**走 `Binding "[FieldName]"`，原因詳見 [ADR-020](../../../maintainers/adr/adr-020-avalonia-datagrid-binding-strategy.md)），並以 `GridEditMode` 提供兩種編輯模型（`InCell` 逐格 / `EditForm` 彈窗整列，詳見 [ADR-021](../../../maintainers/adr/adr-021-avalonia-datagrid-editing-strategy.md)）。field editor 支援 ambient 綁定：容器設一次 `FormScope.DataObject`，子孫編輯器憑 `FieldName` 自動接線。

實際範例：[`apps/Polhem.Northwind`](../../../apps/Polhem.Northwind/README.zh-TW.md)（完整 CRUD 流程；桌面、瀏覽器、iOS 與 Android head 共用一個 UI 專案）與 [`samples/Avalonia.DemoCenter`](../../../samples/Avalonia.DemoCenter/README.zh-TW.md)（控件 demo center）。

### 速查表

| 前端 | 連線抽象 | Token 承載 | Endpoint 持久化 | 模式 | 註冊方式 |
|------|---------|-----------|---------------|------|---------|
| `Polhem.UI.*` head（桌面、瀏覽器、iOS、Android 上的 Avalonia；你自己的 WinForms / WPF host） | `ClientInfo` static | **1 個使用者 / process**（`ClientInfo.AccessToken` 背後的 static 欄位） | `ClientInfo.EndpointStorage`（預設 `FileEndpointStorage`；瀏覽器中替換） | Remote；桌面可用 Local | 啟動時 `ClientInfo.InitializeAsync` |
| Blazor Server | DI scope | **N 個使用者 / process**（per SignalR circuit） | 啟動設定（`UseRemoteProvider(endpoint)`） | Local 或 Remote | `AddPolhemBlazor`（Local 另加 `AddPolhemFramework`） |

> ⚠️ **不要在 Blazor Server 使用 `Polhem.UI.Core.ClientInfo`**：它把 access token 存在單一 static 欄位，一個 process 內只能存 **1 個** AccessToken。Blazor Server 同 process 服務 N 個 user circuit 時，後登入者會覆蓋前者，造成 cross-user data leak。詳見 [ADR-013](../../../maintainers/adr/adr-013-frontend-api-connection-strategy.md)。
