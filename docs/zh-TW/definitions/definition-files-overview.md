<!-- source: en/definitions/definition-files-overview.md blob: 5876f0e2f71169ccf83702c231ebc1a107a8da4c -->
# 定義檔全景

[English](../../en/definitions/definition-files-overview.md) · [← 文件索引](../README.md)

> 所有定義檔的全景圖：各自管什麼、彼此怎麼串、改了哪個會影響哪一層。本頁是導引層 —— 每一項都連向深入說明它的文件。

Polhem 是定義驅動的：`DefinePath` 下的 XML 不是外掛在應用上的組態，它**本身就是**應用的結構。框架讀它來建 SQL、繪 UI、執行權限與在地化文字。

---

## 1. 全部定義類型

定義類型以 `DefineType` 列舉，全部透過 `IDefineAccess` 取得。`FormSchema`、`TableSchema`、`FormLayout` 與 `Language` 帶 key、放在子資料夾；其餘是 `DefinePath` 根目錄下的單一檔案。

定義檔裡寫的文字 —— 標題、顯示名稱、規則訊息、選單標題 —— 是基底文字，以英文撰寫。`Language` 檔負責翻譯；沒有任何語系資源宣告的 key 就沿用基底文字。

| 定義 | `DefinePath` 下的路徑 | 管什麼 | 深入閱讀 |
|------|---------------------|--------|---------|
| **FormSchema** | `FormSchema/{progId}.FormSchema.xml` | 定義中樞：欄位、型別、關聯、主從結構、計算欄與規則 | [架構總覽](../architecture/architecture-overview.md) |
| **TableSchema** | `TableSchema/{categoryId}/{tableName}.TableSchema.xml` | 實體資料表：欄位、型別、長度、可空性、索引 | [Schema 升級](../database/database-schema-upgrade.md) |
| **FormLayout** | `FormLayout/{layoutId}.FormLayout.xml` | 表單在畫面上如何排版。於設計階段產出——執行階段渲染這份檔案，缺檔即失敗 | [架構總覽](../architecture/architecture-overview.md) |
| **Language** | `Language/{lang}/{namespace}.Language.xml` | 基底文字的翻譯 —— 標題、列舉項目、規則訊息、選單標題 —— 每個 namespace × 語言一檔 | [租戶客製化](customization.md) |
| **SystemSettings** | `SystemSettings.xml` | 行程層級設定：主金鑰來源、payload 選項、debug 模式 | [端到端開發指引](../guides/development-cookbook.md) |
| **DatabaseSettings** | `DatabaseSettings.xml` | 實體資料庫與其連線字串 | [資料庫設定指引](../database/database-settings-guide.md) |
| **DbCategorySettings** | `DbCategorySettings.xml` | 各資料表屬於哪個邏輯分類（`common` / `company` / `log`）。資料庫不列在這裡：`DatabaseSettings` 的每個 `DatabaseItem` 各自標明它承載的分類 | [資料庫設定指引](../database/database-settings-guide.md) |
| **ProgramSettings** | `ProgramSettings.xml` | 型別註冊表：progId → 綁定其上的商業物件與 Repository。僅供 server 端 | — |
| **MenuSettings** | `MenuSettings.xml` | 導覽選單：分組、排序、標題與可見性，每個項目指向一個 progId | — |
| **PermissionModels** | `PermissionModels.xml` | 權限模型 registry：模型、動作與 record scope 策略 | [權限與授權](../security/permission-authorization.md) |
| **CurrencySettings** | `CurrencySettings.xml` | 幣別主檔：各幣別小數位與自然最小單位 | [端到端開發指引](../guides/development-cookbook.md) |
| **UnitSettings** | `UnitSettings.xml` | 計量單位主檔：各單位顯示小數位 | [端到端開發指引](../guides/development-cookbook.md) |
| **PluginSettings** | `PluginSettings.xml` | 業務 plugin 綁定：每個 progId 掛哪些 plugin、依宣告順序執行 | [租戶客製化](customization.md) |

## 2. FormSchema 是中樞

一份 `FormSchema` 同時驅動三個層。這是框架中最重要的一組關係：

```text
                    ┌──────────────────┐
                    │   FormSchema     │  欄位 · 型別 · 關聯
                    │   {progId}       │  主從結構 · 規則
                    └────────┬─────────┘
             ┌───────────────┼───────────────┐
             ▼               ▼               ▼
      ┌────────────┐  ┌──────────────────┐  ┌──────────────┐
      │ FormLayout │  │ TableSchema      │  │ 規則 /       │
      │  （UI）    │  │ ＋ 執行期 SQL    │  │ 運算式       │
      └────────────┘  └──────────────────┘  └──────────────┘
        長什麼樣          存在哪裡 · 怎麼進出        什麼才合法
```

- **對資料庫**：框架在執行期依 FormSchema 產生 SQL —— 沒有 ORM、沒有產生的 entity 類別。見 [FormSchema 驅動的資料庫存取](formschema-data-access.md)。
- **對 UI**：`FormLayout` 排列 FormSchema 宣告的欄位；控件直接讀欄位的 metadata（最大長度、清單項目、唯讀、關聯 → lookup）。
- **對驗證**：計算欄與 `FormRule` 就寫在 FormSchema 內。見 [運算式與規則](expression-rules.md)。標記 `Required="true"` 的欄位由伺服端強制：`FormBusinessObject.Save` 會拒絕讓該欄位留空的新增或修改資料列（主檔與明細皆然），並以使用者語言的欄位標題指出是哪個欄位。檢查在伺服端填入預設值之後、寫入任何資料之前執行。「空」指沒有值、空白文字或空的 GUID；數字、布林與日期一定有值，因為這些欄位是帶預設值的 `NOT NULL`。只檢查會儲存的欄位，因此要讓 lookup 必填，請標記它儲存的鍵值欄位，而不是用來顯示的關聯欄位。Avalonia 的 `FormView` 與 Blazor 的 `FormPage` 在送出儲存前套用同一條規則（`Polhem.Definition` 的 `RequiredFieldCheck`），一次列出所有空白欄位，且不送出儲存。

實務結果是：**一般 CRUD 不需要任何程式碼**。一份 FormSchema、對應的 TableSchema 與 FormLayout，加上一筆 `DbCategorySettings` 登錄，就是一張能用的表單；`MenuSettings` 的一個 `MenuEntry` 讓它出現在選單上。只有要綁定客製的商業物件或 Repository 時才需要 `ProgramSettings` 項目：註冊表沒提到的 progId 會解析為 `FormBusinessObject` 與 `DataFormRepository`（§4）。

## 3. 啟動三件組

三個設定檔是所有資料存取的基礎，而第一個必須先於另外兩個載入：

```text
SystemSettings.xml          ──▶ SysInfo.Initialize + ApiServiceOptions.Initialize
   （主金鑰、payload）            （行程層級狀態）
        │
        ▼
DatabaseSettings.xml        ──▶ 實體資料庫 + 連線字串
   （以 id 被參照）               （密碼用主金鑰解密）
        │
        ▼
DbCategorySettings.xml      ──▶ 資料表 → 分類
   （common / company / log）
```

`SystemSettings` 必須最先載入，因為它指名的主金鑰正是用來解密 `DatabaseSettings` 內資料庫密碼的東西。完整順序見[端到端開發指引 § 框架初始化順序](../guides/development-cookbook.md#框架初始化順序)；違反順序會壞在哪裡見[開發限制與反模式](../architecture/development-constraints.md)。

### CategoryId 是 scope 選擇器，不是自由字串

`CategoryId` 只認三個值（其他值會讓 `RepositoryFactory.ParseCategoryId` 拋出例外），選錯是最常見的設定錯誤：

| 分類 | 意義 |
|------|------|
| `common` | 跨公司共享的框架表（session、快取通知、使用者） |
| `company` | 各公司獨立資料 —— **所有業務表都屬於這裡**，應用的組織表亦然 |
| `log` | 日誌與稽核表 |

表前綴（`st_` / `ft_`）表示這張表**歸誰所有**，分類表示**資料落在哪裡**。兩者是**正交**的軸。見[資料庫設定指引](../database/database-settings-guide.md)與[框架保留命名](../reference/framework-reserved-names.md)。

## 4. ProgramSettings 是型別註冊表

`ProgramSettings.xml` 把每個 progId 對映到綁定其上的型別，僅此而已。它是**單層攤平**的清單 ——
progId 就是鍵，因此全域唯一性由結構本身保證，重複項在載入期即被擋下。

```xml
<ProgramSettings>
  <Items>
    <ProgramItem ProgId="Customer" DisplayName="Customers" />
    <ProgramItem ProgId="Order" DisplayName="Orders"
                 BusinessObject="MyApp.Server.BusinessObjects.OrderBO, MyApp.Server"
                 Repository="MyApp.Server.Repositories.OrderRepository, MyApp.Server" />
  </Items>
</ProgramSettings>
```

- **`BusinessObject` 留空** → 該 progId 解析到框架預設的 `FormBusinessObject`，即純定義驅動的 CRUD。
- **`BusinessObject` 有值** → 由該型別承接此 progId，用於宣告式表達不了的情況（跨列聚合、資料庫查詢）。
- **`Repository` 留空** → 框架預設的 `DataFormRepository`，其語句由 FormSchema 產生。
- **`Repository` 有值** → 由該型別承接，且必須衍生自 `DataFormRepository`，CRUD 表面才完整。BO 端以自己的介面取得它：`CreateFormRepository<IOrderRepository>()`。

兩個屬性彼此獨立 —— 一支程式可以只客製邏輯、只客製資料存取、兩者皆客製，或都不客製。兩者都有值時，
它們是同一個 progId 的一對：一支程式、一個商業物件、一個 Repository。

**兩個屬性的失敗策略一致：型別名載不到就直接拋。** 留空不算失敗 —— 那是「沒宣告」，
維持框架預設，漸進採用因此仍然安全。但**寫了一個名字卻解析不出可用型別**是設定錯誤。
退回換到的只有「看起來還在跑」：`Order` 設錯會先默默退化成通用行為，而在資料存取那一側，
更是讓這支程式的讀寫改跑作者刻意替換掉的通用 SQL，故障延後到資料已經錯了的時候才浮現。

`System`、`AuditLog` 與 `AuditRule` 是**保留字 progId**（`ReservedProgIds`），在此檔中與其他項目無異。
host 啟動時若發現缺項會自行補寫；它們的基底約束比一般 progId 更緊：`System` 必須解析為
`SystemBusinessObject`、`AuditLog` 必須解析為 `AuditLogBusinessObject`，或其子類；`AuditRule` 是一張
表單，預設商業物件為 `AuditRuleBusinessObject`，必須解析為 `FormBusinessObject`。綁定超出該基底時
host 無法啟動。補寫時 `Repository` 留空：`System` 與 `AuditLog` 改走框架 Repository 取數，
`AuditRule` 則使用 schema 驅動的預設。

`ProgramSettings` **僅供 server 端**。它承載組件限定型別名，client 端毫無用處，因此遠端 `GetDefine`
會擋下它：遠端呼叫者只能讀取 client 繪製表單與選單所需的定義型別（見 [API 方法總覽](../api/api-method-reference.md)）。

## 4b. MenuSettings 是導覽選單

一支程式**出現在哪裡**是另一份定義，由 client 端讀取：

```xml
<MenuSettings>
  <Items>
    <MenuFolder Id="transactions" Caption="Transactions" Order="10">
      <Items>
        <MenuEntry Id="sales-order" Caption="Orders" Order="10" ProgId="Order" />
      </Items>
    </MenuFolder>
  </Items>
</MenuSettings>
```

- **`Id` 是節點的鍵，且全樹唯一**，與 `ProgId` 各自獨立。同一支程式合理地可以出現在多處，
  因此 shell 追蹤目前開啟的節點要用 `Id` 而非 `ProgId`。
- **資料夾可任意巢狀**，其存在只為分組。
- **`Visible` 是設計期開關，不是權限。** 它對每個使用者都一樣；逐使用者的可見性屬
  [權限與授權](../security/permission-authorization.md) 的職責。框架目前對選單不做任何權限過濾。
- **`Caption` 是基底文字；翻譯**放在 `LanguageResource` 的 `Menu` namespace，sub-key 為
  `Folder.{id}.Caption` / `Entry.{id}.Caption` —— 以 `Id` 而非 `ProgId` 為鍵，因為同一支程式
  可能以不同標題出現在多處。由 `MenuLocalizer` 解析，client 透過
  `FormDefinitionLoader.GetMenuLocalizerAsync` 取得；沒有任何語系宣告的 key 就沿用節點自己的 `Caption`。

兩者分家的理由來自各自的用途：註冊表由 server 讀、裝的是型別名；選單由 client 讀、裝的是排序、
標題與可見性。讀者不同、生命週期不同、敏感度也不同 —— 而「型別名不上 wire」正是這個切分的直接結果。

因此在運行中的應用加一張表單，只需修改 XML、零程式碼。

## 5. 改了 X 要同步改什麼

| 你改了 | 還要一併更新 |
|--------|------------|
| 在 **FormSchema** 加欄位 | 對應 **TableSchema** 的欄位，然後執行 [schema 升級](../database/database-schema-upgrade.md)；要顯示就加進 **FormLayout**；標題的翻譯加進 **Language** |
| 新增**一張表單** | **FormSchema** + **TableSchema** + **FormLayout** + **DbCategorySettings** 的資料表登錄 + **MenuSettings** 的一個 `MenuEntry`；只有要綁定客製商業物件或 Repository 時才加 **ProgramSettings** 的 `ProgramItem` |
| 新增**一張資料表** | 它的 **TableSchema** 必須放在與 `DbCategorySettings` 分類相符的 `TableSchema/{categoryId}/` 資料夾 —— 資料夾名**就是**分類 |
| 新增**一個資料庫** | 在 **DatabaseSettings** 加一個 `DatabaseItem`，以 `CategoryId` 標明它承載的分類；公司資料庫透過 `st_company.company_database_id` 指派給該公司 —— 見[資料庫設定指引](../database/database-settings-guide.md) |
| 改**幣別或單位精度** | **CurrencySettings** / **UnitSettings**；欄位層級的捨入依 `NumberKind`，不是原始欄位型別 |
| 新增**受權限控管的動作** | **PermissionModels**，接著是相關的 `FormField.ScopeRole` —— 見[權限與授權](../security/permission-authorization.md) |

## 6. `DefinePath` 與 `Defaults/` scaffold

兩件很容易混淆的事：

- **`DefinePath`** 是執行期實際讀取的位置，也是執行期定義的**唯一**來源。
- **`Defaults/`** 內嵌於 `Polhem.Definition.dll`，是**開新專案的 scaffold 來源**。`dotnet polhem defines materialize` 會把它複製進你的 `DefinePath`，一次性動作。

> **不存在 fallback。** 若某份定義在 `DefinePath` 缺漏，框架**不會**回退去讀 `Defaults/`。要在專案中使用某個框架系統表，把它的定義展開進你的 `DefinePath` 再往上擴充 —— 並保留框架的標準欄位，權限與組織功能依賴它們。

### 定義資料在 init 後不可異動

透過 `IDefineAccess.GetX(...)` 取得的一切都是**行程層級的快取共用實例**，每個 session 拿到同一個 reference。在 runtime 上直接 mutate 會跨 session 洩漏。要改請先 clone；要持久化請走 `IDefineAccess.SaveX(...)`，它會寫入 storage 並使該快取失效。

完整規則見[開發限制與反模式 § 定義資料 init 後不可異動](../architecture/development-constraints.md)。

### 儲存體可抽換

上述檔案佈局是預設實作（`FileDefineStorage`）。定義也可存放於資料庫 —— 見 [ADR-018](../../../maintainers/adr/adr-018-db-define-storage.md)。兩種情況下 `IDefineAccess` 都是同一套介面，變的只是背後的儲存體。

## 7. `CustomizePath` 與租戶客製覆蓋層

`DefinePath` 放的是所有租戶共用的 base 定義。`CustomizePath` 是可選的第二個根目錄，讓單一公司在**不分叉 base** 的前提下覆蓋其中一部分 —— 設計背景見 [ADR-016](../../../maintainers/adr/adr-016-multitenant-customization-overlay.md)。

### 怎麼打開

由 host 自行算出路徑，與 `DefinePath` 一起傳給 `AddPolhemFramework`。框架**沒有組態綁定機制**，`PathOptions` 一向由 host 建構，`CustomizePath` 走的是同一條路：

```csharp
var paths = new PathOptions
{
    DefinePath = definePath,
    CustomizePath = Path.Combine(deployRoot, "Customize"),
};
builder.Services.AddPolhemFramework(settings.BackendConfiguration, paths);
```

**`CustomizePath` 留空即整層關閉** —— 所有消費端一律走 base，行為與這個功能不存在時逐位元相同。這是預設值。接線示範見 `samples/Polhem.Samples.Shared/DemoBackend.cs`。

### 檔案佈局

```
{CustomizePath}/{customizeId}/ProgramSettings.xml
{CustomizePath}/{customizeId}/MenuSettings.xml
{CustomizePath}/{customizeId}/PluginSettings.xml
{CustomizePath}/{customizeId}/FormLayout/{layoutId}.FormLayout.xml
{CustomizePath}/{customizeId}/Language/{lang}/{namespace}.Language.xml
```

目錄不必存在。某次查找若該租戶沒有對應檔案，就回退 base 層。

### 可客製的型別與其粒度

| 型別 | 覆蓋粒度 |
|------|---------|
| **LanguageResource** | 文字（`LanguageItem`）是 **key 級**。客製檔只放要改的 key，其餘全部來自 base —— 因此 base 日後新增的翻譯會自動傳播。**`LanguageEnum` 是例外：整組取代。** 客製檔有同名 enum 就整組換掉 base 的，因此客製檔必須列出該選項集要有的**全部** entry |
| **ProgramSettings** | **progId 級，其下再分屬性級**。同一個 progId 的客製項目勝過 base 項目；而該項目內每個綁定各自獨立——只指名 `BusinessObject` 的客製項，`Repository` 仍沿用 base 的值。**只寫你要改的那個** |
| **PluginSettings** | **progId 級，相加**。唯一「相加」而非「挑一個」的粒度：base 那條鏈先跑，接著跑客製那條。plugin 本來就是加一段而不是取代一段，所以兩層的 plugin 不互斥 |
| **MenuSettings** | **整檔級**。客製選單整份取代 base 選單 |
| **FormLayout** | **整檔級**。客製 layout 整份取代該 `layoutId` 的 base layout |

`ProgramSettings` 項目內的屬性級繼承，是為了讓「只改一部分的客製」不會把 base 的綁定弄不見。
一筆項目承載兩個彼此獨立的綁定，而**空值是合法的「用框架預設」而非錯誤**——整筆取代會讓 base 的
repository 就這樣消失，且不會有任何回報。若要**刻意**讓某個綁定退回框架自己的型別，請顯式指名該
型別，而不是把屬性清空：

```xml
<!-- 這個程式保留自己的客製 repository，但行為退回通用 CRUD。 -->
<ProgramItem ProgId="Order" BusinessObject="Polhem.Business.Form.FormBusinessObject, Polhem.Business" />
```

**粒度不同是刻意的**，分界線在於：這份東西是**一袋彼此獨立的值**，還是**一個組合起來才成立的整體**。而 `PluginSettings` 自成一類，因為它兩者都不是：它是**一條依序執行的鏈**，所以兩層相加才是對的語意。

文字 key 彼此獨立——「這個標題我們叫法不同」不影響其餘任何一個 key，所以逐 key 疊加既省成本又直覺。但 layout 是**一整個版面**：區塊、排列順序、欄寬與巢狀只有整體看才有意義，局部疊加會冒出無從直覺回答的問題（「這個區塊搬走了，底下的欄位跟著走嗎？」）。**enum 屬於後者而非前者**：它是一組**有順序的選項集**，逐 entry 合併會讓順序、以及「客製檔沒列到的 entry 是什麼意思」兩件事都變得曖昧。

所以 layout 與 enum 一樣：客製了就完整擁有那一份，沒客製的租戶則原封不動拿到 base 版本。

**完整擁有**是雙向的：日後 base `FormSchema` 新增的欄位，**不會**出現在已客製該 layout 的租戶畫面上，框架既不合併也不對這個差異提出警告。這是設計意圖而非限制 —— **layout 才是「畫面上有什麼」的權威來源**，schema 多了一個欄位並不等於每個租戶的表單從此都該顯示它。要讓新欄位出現在那個租戶的表單上是一個決定，而這個決定的執行方式就是去改那份客製 layout 檔。

> **FormSchema 與 TableSchema 永久排除。** 兩者同時驅動資料庫結構與驗證規則，不只驅動 UI；逐租戶分歧會讓實體 schema 裂開。這是裁決不是缺口 —— 見 ADR-016。

> 客製層**除 `PluginSettings` 外皆為唯讀**。客製檔由部署工具產生，檔案式覆蓋層上其餘所有 `SaveXxx` 一律拋出 `NotSupportedException`（`CustomizeOnlyStorage`）。plugin 綁定透過僅限本機的 `SystemBusinessObject.SaveCustomizePluginSettings` 維護 —— 見[租戶客製化](customization.md#除了-plugin-之外都是唯讀)。

### `customizeId` 從哪來

`CompanyInfo.CustomizeId`（欄位 `st_company.customize_id`）在 session 進入公司時被複製到 `SessionInfo.CustomizeId`，離開公司 / 登出時清除。伺服端消費者從 `SessionInfo` 讀取；唯一的例外是僅限本機的 plugin 維護 API，它會指名要維護的租戶。

兩個必須納入規劃的推論：

- **`EnterCompany` 之前沒有任何客製。** 登入畫面、公司選單、以及到那之前的所有訊息都走 base，因為此時還沒有 `CustomizeId`。
- **`SessionInfo.CustomizeId` 是快照不是即時值。** 它在進公司當下複製，與角色、employee context 的策略一致。事後改 `st_company.customize_id` 不會影響既有 session，要下次 `EnterCompany` 才會拿到新值。

> **安全界線：** 遠端呼叫者能觸及的 API 都不採信 client 傳來的 `customizeId` 作為查找依據 —— 那等於讓呼叫端自選要讀哪一家租戶的客製檔。確實接收 `customizeId` 的 plugin 維護呼叫都是 `LocalOnly`。client 從 `EnterCompany` 拿到的那份 `CustomizeId` 只供 client 自己的 UI 在地化使用；伺服端一律讀 `SessionInfo.CustomizeId`。

---

## 接下來讀什麼

| 你想 | 讀 |
|------|-----|
| 看這些拼圖如何組成架構 | [架構總覽](../architecture/architecture-overview.md) |
| 走完整條「定義 → API」流程 | [端到端開發指引](../guides/development-cookbook.md) |
| 理解 FormSchema 如何產生 SQL | [FormSchema 驅動的資料庫存取](formschema-data-access.md) |
| 以宣告方式做欄位運算與驗證 | [運算式與規則](expression-rules.md) |
| 知道哪些命名歸框架所有 | [框架保留命名](../reference/framework-reserved-names.md) |
| 設定資料庫與分類 | [資料庫設定指引](../database/database-settings-guide.md) |
