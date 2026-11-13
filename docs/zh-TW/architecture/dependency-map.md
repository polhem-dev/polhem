<!-- source: en/architecture/dependency-map.md blob: f658dcea41b9386b297f6ad53ad901f2ae272f93 -->
# 專案相依性全景圖

[English](../../en/architecture/dependency-map.md) · [← 文件索引](../README.md)

本文件以視覺化方式呈現 Polhem 框架中 `src/` 專案之間的相依關係。
圖中涵蓋的是執行期套件；`Polhem.Analyzers` 不列入，因為執行期沒有任何專案參考它 ——
消費端拿到的是建置期 analyzer，畫進去等於多一條組件圖上不存在的邊。
（此處不寫專案數：那會漂，而 `src/` 目錄本身就是權威來源。）

**閱讀方式**：箭頭方向 A → B 表示「A 依賴 B」；圖表由下而上排列，最底層為無相依性的基礎套件。

## 總覽

每個內層方塊是一個區塊，名稱列出其中的專案，並省略 `Polhem.` 前綴。區塊再分成三邊：兩邊都參考的**共用**、**伺服端**與**用戶端**。

<!-- dependency-map:overview:start -->
```mermaid
graph BT
  subgraph SharedSide ["共用"]
    Foundation["<b>基礎</b><br/>Core, Definition,<br/>Api.Contracts, Expressions"]
    Wire["<b>Wire</b><br/>Api.Core"]
  end
  subgraph ServerSide ["伺服端"]
    Backend["<b>資料與商業邏輯</b><br/>Db, Repository,<br/>Repository.Abstractions,<br/>Business, ObjectCaching"]
    ApiServer["<b>API 伺服端</b><br/>Api.Server"]
    Composition["<b>組合根</b><br/>Hosting"]
  end
  subgraph ClientSide ["用戶端"]
    ApiClient["<b>API 用戶端</b><br/>Api.Client"]
    FrontShared["<b>前端共通</b><br/>UI.Core, UI.ApiBridge"]
    Avalonia["<b>Avalonia 前端</b><br/>UI.Avalonia,<br/>UI.Avalonia.Shell"]
    Web["<b>Web 前端</b><br/>Web.Blazor, .Mud, .Fluent,<br/>.Server, .Wasm"]
  end

  Wire --> Foundation
  Backend --> Foundation
  ApiServer --> Wire
  Composition --> Foundation
  Composition --> Backend
  Composition --> ApiServer
  ApiClient --> Wire
  FrontShared --> Foundation
  FrontShared --> ApiClient
  Avalonia --> Foundation
  Avalonia --> FrontShared
  Web --> Foundation
  Web --> FrontShared
  Web --> ApiClient
```
<!-- dependency-map:overview:end -->

- 伺服端與用戶端只在**共用**相接：任一邊的專案都不參考另一邊的專案。若有這種參考，它會以兩邊之間的一條邊出現在這張圖上，因為 `./check-dependency-map.sh` 會讓總覽的邊與 csproj 中的參考保持一致。
- 伺服端由 **Polhem.Hosting** 組裝。
- 兩種前端都建立在**前端共通**之上。

**專案屬於哪一邊，看它的角色，而不是它跑在哪個行程。** 伺服端提供 API；用戶端呼叫 API 或呈現它的資料。`Polhem.Web.Blazor.Server` 雖然跑在 web 伺服器上，仍屬用戶端：它負責呈現 UI，並以 API 用戶端的身分存取後端，即使後端和它在同一個行程裡也一樣。兩邊都參考的專案歸為共用。`src/` 資料夾維持一層：套件前綴（`Polhem.UI.*`、`Polhem.Web.*`、`Polhem.Api.Server`）已標示出家族，而資料夾也表達不了 `Polhem.Expressions` 這類共用專案。

## 各區塊的專案

每個區塊一張圖，依共用、伺服端、用戶端的順序排列。實線方塊屬於該區塊，虛線方塊是它依賴的其他區塊專案。這些圖合起來就是 `src/` 中所有的專案參考。`./check-dependency-map.sh` 會在每次推送時，把這些圖和總覽與 csproj 比對。

<!-- dependency-map:areas:start -->
### 基礎

共用：兩邊都建立在這一層上，`Polhem.Core` 是整張圖的最底層。

```mermaid
graph BT
  subgraph Foundation ["基礎"]
    Base["Polhem.Core"]
    Expressions["Polhem.Expressions"]
    Definition["Polhem.Definition"]
    Contracts["Polhem.Api.Contracts"]
  end

  Definition --> Base
  Expressions --> Base
  Contracts --> Definition
```

### Wire

共用。呼叫兩端共用的訊息、codec、壓縮、加密與錯誤契約，不含任何伺服端的部分。

```mermaid
graph BT
  subgraph Wire ["Wire"]
    Core["Polhem.Api.Core"]
  end
  Definition["Polhem.Definition"]:::other
  Contracts["Polhem.Api.Contracts"]:::other

  Core --> Contracts
  Core --> Definition

  classDef other stroke-dasharray: 5 4
```

### 資料與商業邏輯

伺服端。`Polhem.Business` 透過 `Polhem.Repository.Abstractions` 存取資料，不參考 `Polhem.Db`。

```mermaid
graph BT
  subgraph Backend ["資料與商業邏輯"]
    RepoAbs["Polhem.Repository.Abstractions"]
    Db["Polhem.Db"]
    Repo["Polhem.Repository"]
    Business["Polhem.Business"]
    Caching["Polhem.ObjectCaching"]
  end
  Definition["Polhem.Definition"]:::other
  Contracts["Polhem.Api.Contracts"]:::other

  Db --> Definition
  RepoAbs --> Definition
  Caching --> Definition
  Business --> Contracts
  Business --> Definition
  Business --> RepoAbs
  Repo --> Db
  Repo --> RepoAbs

  classDef other stroke-dasharray: 5 4
```

### API 伺服端

伺服端。dispatcher、存取與 payload 篩選器，以及例外對應。

```mermaid
graph BT
  subgraph ApiServer ["API 伺服端"]
    Server["Polhem.Api.Server"]
  end
  Core["Polhem.Api.Core"]:::other

  Server --> Core

  classDef other stroke-dasharray: 5 4
```

### 組合根

伺服端。`Polhem.Hosting` 負責組裝伺服端，所以直接參考其中的各個專案。

```mermaid
graph BT
  subgraph Composition ["組合根"]
    Hosting["Polhem.Hosting"]
  end
  Server["Polhem.Api.Server"]:::other
  Business["Polhem.Business"]:::other
  Db["Polhem.Db"]:::other
  Repo["Polhem.Repository"]:::other
  Caching["Polhem.ObjectCaching"]:::other
  Expressions["Polhem.Expressions"]:::other

  Hosting --> Server
  Hosting --> Business
  Hosting --> Db
  Hosting --> Repo
  Hosting --> Caching
  Hosting --> Expressions

  classDef other stroke-dasharray: 5 4
```

### API 用戶端

用戶端。它不參考 `Polhem.Api.Server`，兩端只共用 `Polhem.Api.Core`。

```mermaid
graph BT
  subgraph ApiClient ["API 用戶端"]
    Client["Polhem.Api.Client"]
  end
  Core["Polhem.Api.Core"]:::other

  Client --> Core

  classDef other stroke-dasharray: 5 4
```

### 前端共通

用戶端。`Polhem.UI.Core` 不帶傳輸，Avalonia 前端透過 `Polhem.UI.ApiBridge` 把它接上用戶端。

```mermaid
graph BT
  subgraph FrontShared ["前端共通"]
    UICore["Polhem.UI.Core"]
    UIApiBridge["Polhem.UI.ApiBridge"]
  end
  Contracts["Polhem.Api.Contracts"]:::other
  Client["Polhem.Api.Client"]:::other

  UICore --> Contracts
  UIApiBridge --> UICore
  UIApiBridge --> Client

  classDef other stroke-dasharray: 5 4
```

### Avalonia 前端

用戶端。`Polhem.UI.Avalonia` 是控制項，不帶傳輸；`Polhem.UI.Avalonia.Shell` 再加上現成的應用程式外殼。

```mermaid
graph BT
  subgraph Avalonia ["Avalonia 前端"]
    UIAvalonia["Polhem.UI.Avalonia"]
    UIShell["Polhem.UI.Avalonia.Shell"]
  end
  UICore["Polhem.UI.Core"]:::other
  UIApiBridge["Polhem.UI.ApiBridge"]:::other
  Definition["Polhem.Definition"]:::other
  Expressions["Polhem.Expressions"]:::other

  UIAvalonia --> UICore
  UIAvalonia --> Definition
  UIAvalonia --> Expressions
  UIShell --> UIAvalonia
  UIShell --> UIApiBridge

  classDef other stroke-dasharray: 5 4
```

### Web 前端

用戶端。共用層、UI 套件，以及每種宿主各一個套件。UI 套件與宿主套件彼此不參考。

```mermaid
graph BT
  subgraph Web ["Web 前端"]
    BlazorShared["Polhem.Web.Blazor"]
    BlazorMud["Polhem.Web.Blazor.Mud"]
    BlazorFluent["Polhem.Web.Blazor.Fluent"]
    BlazorSrv["Polhem.Web.Blazor.Server"]
    BlazorWasm["Polhem.Web.Blazor.Wasm"]
  end
  Client["Polhem.Api.Client"]:::other
  UICore["Polhem.UI.Core"]:::other
  Expressions["Polhem.Expressions"]:::other

  BlazorShared --> Client
  BlazorShared --> UICore
  BlazorShared --> Expressions
  BlazorMud --> BlazorShared
  BlazorFluent --> BlazorShared
  BlazorSrv --> BlazorShared
  BlazorWasm --> BlazorShared

  classDef other stroke-dasharray: 5 4
```

<!-- dependency-map:areas:end -->

## 外部相依套件

<!-- dependency-map:packages:start -->

| 專案 | 外部套件 |
|------|----------|
| Polhem.Core | *(none)* |
| Polhem.Expressions | DynamicExpresso.Core 2.x |
| Polhem.Definition | Microsoft.Extensions.Localization.Abstractions 10.x |
| Polhem.Db | *(none)* |
| Polhem.ObjectCaching | Microsoft.Extensions.Caching.Memory 10.x、Microsoft.Extensions.Logging.Abstractions 10.x |
| Polhem.Api.Core | MessagePack 3.x、Polhem.JsonRpc.Payload |
| Polhem.Api.Server | Microsoft.Extensions.Logging.Abstractions 10.x、Microsoft.Extensions.DependencyInjection.Abstractions 10.x、Polhem.JsonRpc.Server、Polhem.JsonRpc.Payload.Server |
| Polhem.Business | Microsoft.Extensions.DependencyInjection.Abstractions 10.x、Microsoft.Extensions.Logging.Abstractions 10.x |
| Polhem.Repository | Microsoft.Extensions.DependencyInjection.Abstractions 10.x |
| Polhem.Hosting | Microsoft.Extensions.DependencyInjection.Abstractions 10.x、Microsoft.Extensions.Hosting.Abstractions 10.x、Microsoft.Extensions.Logging.Abstractions 10.x、Polhem.JsonRpc.Server、Polhem.JsonRpc.Payload.Server、Polhem.JsonRpc.Payload |
| Polhem.Web.Blazor | Microsoft.AspNetCore.Components.Web 10.x |
| Polhem.Web.Blazor.Mud | MudBlazor 9.x |
| Polhem.Web.Blazor.Fluent | Microsoft.FluentUI.AspNetCore.Components 5.x |
| Polhem.Web.Blazor.Server | `FrameworkReference: Microsoft.AspNetCore.App`、Polhem.JsonRpc.Payload |
| Polhem.UI.Avalonia | Avalonia 12.x、Avalonia.Controls.DataGrid 12.x |
| Polhem.Api.Client | Polhem.JsonRpc.Payload.Client |
| Polhem.UI.ApiBridge | Microsoft.Extensions.DependencyInjection.Abstractions 10.x |
| Polhem.UI.Avalonia.Shell | Avalonia 12.x, Avalonia.Controls.DataGrid 12.x, Semi.Avalonia 12.x, Semi.Avalonia.DataGrid 12.x, CommunityToolkit.Mvvm 8.x, Microsoft.Extensions.DependencyInjection 10.x |
| Polhem.Api.Contracts / Polhem.Repository.Abstractions / Polhem.UI.Core / Polhem.Web.Blazor.Wasm | *(none)* |

<!-- dependency-map:packages:end -->

> `Polhem.Api.Core` 的 MessagePack 是框架內**唯一**的傳輸格式套件，而讓它維持唯一正是
> [ADR-036](../../../maintainers/adr/adr-036-wire-serialization-externalized.md) 的用意。`Polhem.JsonRpc.*` 套件維護在
> [polhem-dev/polhem-jsonrpc](https://github.com/polhem-dev/polhem-jsonrpc)，框架使用的確切版本設定在 repo 根目錄的
> `PolhemJsonRpc.props`（檔頭說明為什麼不用版本範圍）；HTTP 宿主自行加入 `Polhem.JsonRpc.AspNetCore`。僅建置期的參考
> （`PrivateAssets="all"`：SourceLink、公開 API 分析器、本 repo 自己的 analyzer）
> 不列入 —— 它們不會流到任何消費者。`check-dependency-map.sh` 會比對本表的套件名稱與 csproj 檔，版本不比對。

## 目標框架摘要

所有執行期套件皆以 `net10.0` 單一目標發布。例外是 `Polhem.Analyzers`，它以 `netstandard2.0` 為目標
—— Roslyn 就是以該框架載入 analyzer，這是 analyzer host 的要求而非選擇。

## 工具套件（獨立發行）

不屬於上方 `src/` 套件相依圖——以 `dotnet tool` 套件形式 ship 在 NuGet：

| 套件 | 命令 | 說明 |
|------|------|------|
| **Polhem.Cli**（`tools/Polhem.Cli/`） | `dotnet polhem` | 框架 CLI；`dotnet polhem --help` 列出其命令。引用 `Polhem.Definition`，呼叫其公開 `Defaults` API 處理 embedded 框架預設的 materialize / list；並引用 `tools/Polhem.Definition.Spreadsheet`（MiniExcel）處理表單活頁簿。版本與框架一致（兩者都讀 `Version.props`）。 |
| **Polhem.DefineEditor**（`tools/DefineEditor/`） | `dotnet polhem-define`（本機工具） | Avalonia 桌面工具，可視覺化編輯各種定義類型；也可建置成 macOS `.app`。不帶資料庫驅動：資料庫相關工作交給 Polhem.DbManager。 |
| **Polhem.DbManager**（`tools/DbManager/`） | `polhem-db` | 以所有支援資料庫的驅動，對定義資料夾執行 `Polhem.Hosting` 的 `db` 指令。 |

## 架構要點

- **Polhem.Core** 為最底層基礎套件，無任何內部相依性。
- **Polhem.Expressions** 只承載 `DynamicExpressoEvaluator`——運算式引擎以 DynamicExpresso 為底的實作。**抽象**（`IExpressionEvaluator`、`ExpressionPolicy`、`ExpressionEvaluationException`）位於 `Polhem.Core.Expressions`，因此 `Polhem.Definition`（`FormExpressionCalculator`）與 `Polhem.Business`（規則處理器）消費引擎時不會相依 DynamicExpresso；只有決定用哪個實作的組裝層（`Polhem.Hosting` 的 DI 註冊、`Polhem.UI.Avalonia` 與 `Polhem.Web.Blazor` 的前端即時預覽）才引用本套件；兩端共用的即時預覽引擎 `Polhem.UI.Core` 的 `FormLiveComputationEngine` 由它們傳入運算引擎。這個分界讓定義層不帶第三方套件，同時維持前端算值與後端存檔一致。見 [adr-028](../../../maintainers/adr/adr-028-expression-rule-engine.md) 與 [adr-038](../../../maintainers/adr/adr-038-definition-dependency-boundary.md)。
- **Polhem.Definition** 為被依賴次數最多的專案，直接相依者為 Contracts、Db、RepoAbs、Caching、Business、Api.Core 與 UI.Avalonia。
- **Polhem.Api.Contracts** 是共用契約／抽象層，並非應用層級的 API 專案。雖名為「API」，但 `Polhem.Business` 與 `Polhem.Api.Core` 都相依於它（`Business → Contracts`、`Core → Contracts`），故其位置在兩者**之下** —— 圖上歸入**基礎**，而不和 API 專案放在一起。
- **Polhem.Hosting** 為 composition root：將後端服務（`Polhem.Api.Server`、`Polhem.Business`、`Polhem.Db`、`Polhem.Repository`、`Polhem.ObjectCaching`）整合於一個 `IServiceCollection.AddPolhemFramework` 擴充入口，不依賴 ASP.NET Core。非 web 宿主（WinForms、Console、Worker Service）直接引用此套件。圖上自成**組合根**一區，不和 `Polhem.Api.Server` 同區：橫跨各層本就是組合根的職責，故「API 層不得直接引用 Repository 層」的限制不適用於它。真正適用的是**它本身不含資料存取** —— SQL 語句歸 `Polhem.Db` / `Polhem.Repository`，Hosting 只留 hosted service 外殼與 DI 接線。
- **JSON-RPC 協定**來自 `Polhem.JsonRpc.*` 套件，它們對 Polhem 一無所知：`Polhem.Api.Server` 建在 `Polhem.JsonRpc.Server` 上，`Polhem.Api.Client` 建在 `Polhem.JsonRpc.Client` 上（經由 `Polhem.JsonRpc.Payload.Client`，以其 `PayloadConnector` 封裝每次呼叫），ASP.NET Core 宿主另加 `Polhem.JsonRpc.AspNetCore` 提供 `POST /api` 端點。相依只走這個方向。見 [ADR-049](../../../maintainers/adr/adr-049-jsonrpc-packages-in-1-2.md)（英文）。
- **Polhem.Api.Core** 是前後端共用的 wire 層：請求與回應訊息、payload codec、壓縮與加密，以及錯誤契約，讓用戶端（`Polhem.Api.Client`）與伺服器端的序列化與加解密行為一致。它不含任何伺服器端的東西，並由 `POLHEM9001` 把關。**Polhem.Api.Server** 放伺服器端：dispatcher 的設定、存取與 payload 篩選器、API 金鑰與授權檢查，以及例外對應。同程序的用戶端透過 `AddPolhemFramework` 註冊的 `IInProcessTransportFactory` 呼叫 dispatcher，因此 `Polhem.Api.Client` 不帶任何伺服器端套件。`Polhem.Api.Server` 會讀取 `Polhem.Api.Core` 的 internal 成員，所以兩者必須是完全相同的版本。詳見 [adr-055](../../../maintainers/adr/adr-055-api-core-split-from-server.md)。
- **前端與後端共用三個基礎組件**：`Polhem.Core`、`Polhem.Definition` 與 `Polhem.Api.Contracts`。它們不放只屬於單一端的東西；前端只透過 `Polhem.Api.Contracts` 的契約接觸後端（表單是 `IFormApiConnector` 與 `IFormApiConnectorFactory`），傳輸層（`Polhem.Api.Core`、`Polhem.Api.Client`）不在前端的相依閉包內。見 [adr-054](../../../maintainers/adr/adr-054-front-end-decoupled-from-api-client.md)（英文）。
- **Polhem.UI.Core** 為所有前端共用的一層，不帶任何傳輸：只引用 `Polhem.Api.Contracts`。內含 UI 的服務契約（`IFormDefinitionSource`、`IUserContext`）與 `UIServices`（由 markup 建立的控制項透過它解析這些服務）；各端共用的表單值規則（`FormValueBinding`、`FormDataGuard`，以及位於 `Polhem.UI.Core.Permissions` 的元素能力解析器）；`FormDataObject`，各端表單檢視與欄位編輯器綁定的資料物件；`StorageDefinitionSource`，建在 `IDefineStorage` 上、供沒有後端的應用程式使用的定義來源；以及供自行實作 `IFormApiConnector` 的應用程式使用的簡單回應型別。不含任何平台專屬 UI 程式碼。
- **Polhem.UI.ApiBridge** 將單一使用者的端（桌面、瀏覽器（WASM）、iOS、Android）接上 Polhem 後端。它引用 `Polhem.UI.Core` 與 `Polhem.Api.Client`，本身與 UI 框架無關。內含 `ClientInfo`（該端的連線狀態）與 endpoint、API key 的儲存（`IEndpointStorage`、`IApiKeyStorage`、`FileEndpointStorage`），並以 `AddPolhemUIApiBridge()` 註冊 `Polhem.UI.Core` 服務的 API 版實作。相依由 bridge 指向兩側：`Polhem.Api.Client` 從不引用 UI。
- **Polhem.UI.Avalonia** 為 Avalonia 控制項套件，供桌面、瀏覽器（WASM）、iOS 與 Android 各端使用；各端需要什麼見[平台支援](../getting-started/platform-support.md)。內含 FormSchema 驅動控制項（`FormView` 單筆、`ListView` 清單、`GridControl` 表格，加上一組 field editor 與 `FormScope` ambient 綁定，皆以 `FormDataObject` 為資料中樞），單一 `net10.0` TFM。它引用 `Polhem.UI.Core`、`Polhem.Definition` 與 `Polhem.Expressions`，不引用 `Polhem.Api.Client`：控制項接收 `IFormApiConnector`，所以應用程式提供自己的 connector 時，不需要 Polhem 後端也能運作。它對 `Avalonia` 與 `Avalonia.Controls.DataGrid` 的參考是版本下限；host 可以帶入更新的 `Avalonia 12.x`。DataGrid 為何不走 `Binding "[FieldName]"` 詳見 [adr-020](../../../maintainers/adr/adr-020-avalonia-datagrid-binding-strategy.md)，編輯策略詳見 [adr-021](../../../maintainers/adr/adr-021-avalonia-datagrid-editing-strategy.md)。
- **Polhem.UI.Avalonia.Shell** 是建立在上面兩個套件之上的現成前端：連線、登入公司，並以分頁開啟伺服器選單上的表單。它位在端（head）的層級，與應用程式自己的 UI 專案並列，因為它需要 `Polhem.UI.ApiBridge`，而 `Polhem.UI.Avalonia` 不得引用它。它也負責套用 Semi 主題，所以只有採用殼的應用程式才會相依這個主題。應用程式繼承 `ShellApplication`；細節見該套件的 README。
- **`Polhem.UI.*` family 判別準則**：所有前端都引用 `Polhem.UI.Core`，所以是否引用它已不能決定 family。決定的是套件是否建立在單一使用者模型上：一個 process 服務一位已登入使用者，服務來自整個 process 共用的 `UIServices.Provider`（有後端時再加上 `Polhem.UI.ApiBridge`）。
  - 單一使用者 → 歸 `Polhem.UI.*`（目前：`Polhem.UI.Core`、`Polhem.UI.ApiBridge`、`Polhem.UI.Avalonia`、`Polhem.UI.Avalonia.Shell`；未來：`Polhem.UI.WinForms`、`Polhem.UI.Wpf` 等同理）
  - 一個 process 服務多位使用者、自有狀態管理 → 走獨立 family prefix（如 `Polhem.Web.Blazor.*`）。Blazor 端過去不引用 `Polhem.UI.Core`，是因為該套件持有整個 process 共用的 `ClientInfo`；這份狀態已移到 `Polhem.UI.ApiBridge`，所以 Blazor 端現在為了共用的值規則引用 `Polhem.UI.Core`，但不得使用 `UIServices`，也不得引用 `Polhem.UI.ApiBridge`。違反時 `UIServicesUsageGateTests`（`tests/Polhem.Web.Blazor.Server.UnitTests`）會失敗。
- **Web 前端層**由三種套件組成：共用的 `Polhem.Web.Blazor`、UI 套件（`Polhem.Web.Blazor.Mud`、`Polhem.Web.Blazor.Fluent`）與宿主套件（`Polhem.Web.Blazor.Server`、`Polhem.Web.Blazor.Wasm`）；應用程式各選一個 UI 套件和宿主套件，兩者互不參考（[adr-059](../../../maintainers/adr/adr-059-blazor-shared-layer.md)）。除了 WebAssembly 宿主之外都是 Razor Class Library；WebAssembly 宿主是一般的類別庫，由 Blazor WebAssembly 專案參考。共用層相依 `Polhem.Api.Client`，為了與 native 各端共用的值規則相依 `Polhem.UI.Core`，並為了計算欄位的即時預覽相依 `Polhem.Expressions`。Blazor Server 宿主在 `AddPolhemBlazor` 選擇傳輸方式：`UseRemoteProvider`（每個 circuit 的 `PolhemApiClient` 經 HTTP 呼叫），或 `UseLocalProvider`（in-process 呼叫，僅限受信任的使用者，且須在同一個 service collection 上呼叫 `AddPolhemFramework`）。WebAssembly 宿主一律遠端呼叫。
