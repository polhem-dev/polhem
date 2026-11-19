<!-- source: en/definitions/expression-rules.md blob: f404f8bae1279794b9d03603ce6125438e30df85 -->
# 運算式與規則（欄位運算與存檔/刪除前驗證）

[English](../../en/definitions/expression-rules.md) · [← 文件索引](../README.md)

用**宣告式運算式**在 `FormSchema` 定義檔裡做欄位運算與驗證，取代手寫 BO 程式碼。客戶/顧問於設計期即可自訂，不需改程式、重編、重佈。

設計背景與決策見 [ADR-028](../../../maintainers/adr/adr-028-expression-rule-engine.md)。

## 三種能力

| 能力 | 載體 | 時機 |
|------|------|------|
| 計算欄 | `FormField.ValueExpression` | 存檔前對新增/異動列重算回填（主表列所加總的明細有變動時，主表列也重算） |
| 欄位預設值 | `FormField.DefaultValueExpression` | 建立新列時（蓋過字面預設值）；存檔時只填新列中仍為空的欄位 |
| 驗證 / 前置檢查 | `FormSchema` 下的 `FormRule` | `BeforeSave` / `BeforeDelete` |

> **後端為權威**：存檔時 `FormBusinessObject.DoBeforeSave` 依定義重算計算欄並覆蓋前端送來的值，接著執行驗證規則；`DoBeforeDelete` 執行刪除規則。Avalonia 與 Blazor（Server 與 WebAssembly）head 的即時運算（`Polhem.UI.Avalonia` 與 `Polhem.Web.Blazor` 各自的 `FormLiveComputation`，底層共用 `Polhem.UI.Core` 的引擎）會在使用者編輯時重算欄位，但它只是 UX 預覽：捨入用的是框架預設的小數位數，存檔時由伺服端修正。

## 運算式語法

- **變數 = 欄位名**：直接寫欄位名即可，如 `unit_price * qty`。同列所有欄位都可用。主表的運算式還能加總明細表，見[明細加總](#明細加總)。
- **運算子**：C# 語法子集（`+ - * /`、`> >= < <= == !=`、`&& || !`、三元 `? :`、字串 `==`）。
- **可用函式/型別**：輔助函式 `Today()`、`Now()`、`UtcNow()`、`IsNullOrEmpty(s)`、`IsNullOrWhiteSpace(s)`，以及下方的業務函式；運算式可直接指名的型別，例如 `Math`（`Math.Round`、`Math.Abs`…）、`Convert`、`DateTime`、`TimeSpan`、`Guid`（如 `customer_rowid != Guid.Empty`）；以及欄位值本身的成員（`name.Length`、`Today().AddDays(1)`）。這些型別來自 DynamicExpresso 的預設集合，加上 `DynamicExpressoEvaluator` 自行加入的；[`ILLink.Descriptors.xml`](../../../src/Polhem.Expressions/ILLink.Descriptors.xml) 列出運算式能觸及其成員的每個型別，並由 `TrimmerDescriptorGateTests` 確保這份清單與直譯器一致。

  **時間函式的語意**（見 [ADR-032](../../../maintainers/adr/adr-032-datetime-timezone.md)）：

  | 函式 | 回傳 | 基準 |
  |------|------|------|
  | `Today()` | `DateOnly` | **使用者所在時區**的今天。請假日期預設當天這類用途要的就是它——使用者在紐約登打台北公司的假單，仍會拿到台北的日期 |
  | `Now()` | `DateTime`（`Kind` 為 `Unspecified`） | 當下，與所在 `DataSet` 的時間值同一基準：用戶端即時預覽時為使用者時區，伺服端存檔前的計算與驗證為 UTC。寫進 `DateTime` 欄位或與之比較時用它 |
  | `UtcNow()` | `DateTime`（`Kind` 為 `Unspecified`） | UTC 當下的原始讀數，不隨所在的那一側變動 |

  `Today()` 回傳 `DateOnly` 而非 `DateTime`：日期在框架中一律以 `DateOnly` 表達，`DataSet`
  儲存格是唯一例外（`DataColumn` 只能以 `DateTime` 承載日曆日）。把 `Today()` 寫進 `Date` 或
  `DateTime` 欄位都可以，框架會在寫入儲存格時完成轉換。

  > **寫進 `DateTime` 欄位或與之比較，請用 `Now()`，不要用 `UtcNow()`。** 用戶端的 `DataSet`
  > 以使用者時區呈現，即時預覽時把 `UtcNow()` 寫進儲存格，畫面上的值會差一個時差。存檔時伺服端
  > 不採用用戶端送來的 `DateTime`，會重新求值或保留資料庫的值，所以存下的資料不受影響；
  > 錯的是存檔前畫面上的值。
- **數值型別不會隱含混算**：變數的型別是其欄位 `DbType` 對應的 CLR 型別（對照見 [`DbTypeConverter.ToType`](../../../src/Polhem.Core/Data/DbTypeConverter.cs)），運算子規則與 C# 相同：`decimal` 不能與 `double` 一起運算。帶小數點的字面值是 `double`，所以當 `amount` 是 `Currency` 或 `Decimal` 欄位時，`amount * 1.05` 會解析失敗。請把字面值寫成 decimal：`amount * 1.05m`，或只用整數：`amount * 105 / 100`。
- **運算式解析失敗時**：未開放的型別名（`File`、`Process`…）、拼錯的欄位名，或無法一起運算的運算元型別（見上一點）。在伺服端，該次存檔或刪除隨之失敗。在用戶端，該表單在開啟期間停止即時運算：head 會顯示一則訊息，指出欄位與原因，計算欄位則在存檔時由伺服端計算。Avalonia 的 `FormView` 會觸發 `LiveComputationDegraded`，Blazor 的 `FormPage` 會寫一筆 warning 到它的 `ILogger`，兩者都帶著運算式，讓 host 記錄需要修正的地方。DefineEditor 會在它們到達任一端之前先擋下：驗證或存檔 FormSchema 時，它以同樣的方式解析每個欄位與規則運算式（`FormExpressionCalculator.ValidateExpressions`），列出失敗的那些。
- **解析不是安全沙箱，不受信任的文字要靠允許清單把關。** 值的成員是以反射解析的，光靠解析擋不住運算式往更深處觸及。定義檔裡的運算式之所以安全，是因為它們的來源：遠端呼叫者無法寫入的定義檔（`SaveDefine` 為 `LocalOnly`）。其他來源的運算式（例如由 AI 模型撰寫的）必須通過 `FormExpressionCalculator.ValidateUntrustedExpression`：它會先檢查長度與巢狀深度上限再解析，然後只接受表單欄位、輔助函式與業務函式、彙總、運算子、字面值，以及一份固定的成員清單（文字、日期與數字的成員，`Math`、`Convert` 等；不含 `GetType`、`typeof`、`DateTime.Now`、LINQ，也不含像 `PadLeft` 這種依參數配置記憶體的成員；數字只能用 `ToString()` 格式化，文字最多串接 8 次；`RegexMatch` 完全不允許，因為 pattern 的比對成本無法事先限制）。在定義檔中，同樣的檢查只會警告：DefineEditor 會把超出允許清單的運算式列為警告，運算式照常執行。沒有經過這項檢查，絕對不要用使用者輸入組出運算式文字。
- **經裁剪的應用程式**：運算式呼叫的成員是以反射找到的，裁剪器看不到。`Polhem.Expressions` 內附一份保留這些成員的裁剪描述檔，見[平台支援](../getting-started/platform-support.md#套件內附的裁剪描述檔)。
- **空值**：空欄（`DBNull`）以型別預設值代入（數值 0、字串空字串、`Guid.Empty`…），所以 `unit_price * qty` 遇空值算 0 而不會出錯。

### 業務函式

這些函式讀起來就像它要表達的規則，而且只用傳入的值計算：不讀資料庫，也不讀表單以外的任何東西，也不拿值和編輯前存的值比較（原因見 [ADR-067](../../../maintainers/adr/adr-067-form-rules-from-plain-language.md)）。DefineEditor 的運算式編輯器會列出每個函式的說明與範例。這張表加上前面的時間函式，就是運算式能呼叫的全部函式：函式目錄有它們沒列的函式，或它們列了目錄沒有的函式時，`HelperFunctionDocsTests`（`tests/Polhem.Expressions.UnitTests`）會失敗。

| 函式 | 傳回 | 範例 |
|------|------|------|
| `IsBlank(value)` | 值是否沒有填：空白或只有空格的文字、`Guid.Empty`、0，或沒有日期。文字與數值欄位不會是 null（見[為什麼文字與數值欄位預設 `NOT NULL`](../database/database-dialect-differences.md#1-為什麼文字與數值欄位預設-not-null)），所以沒填的金額是 `0`，算作空白 | `!IsBlank(approver_id)` |
| `Between(value, low, high)` | 值是否介於另外兩個值之間（含兩端） | `Between(amount, 1, 100000)` |
| `In(value, a, b, …)` | 值是否等於後面列出的其中一個值 | `In(status, "Draft", "Rejected")` |
| `DaysBetween(start, end)` | 從 `start` 到 `end` 的天數；`end` 較早時為負數 | `DaysBetween(order_date, Today()) <= 30` |
| `MonthEnd(date)` | 日期所在月份的最後一天 | `due_date <= MonthEnd(order_date)` |
| `RegexMatch(text, pattern)` | 文字是否符合正規表示式；空的文字一律不符合。比對不使用回溯，所以不支援反向參照與環視。不受信任的作者（例如下方的 AI 助手）不能使用 | `RegexMatch(phone, "^[0-9-]+$")` |
| `IsNullOrEmpty(text)` | 文字是否為空。為既有定義檔保留；`IsBlank` 已涵蓋 | `!IsNullOrEmpty(remark)` |
| `IsNullOrWhiteSpace(text)` | 文字是否為空或只有空格。為既有定義檔保留；`IsBlank` 已涵蓋 | `!IsNullOrWhiteSpace(remark)` |

- **不同型別依種類比較。** 日期欄位是 `DateTime`，`Today()` 是 `DateOnly`；數量是 `int`，金額是 `decimal`。`Between`、`In`、`DaysBetween` 兩種都接受：數字以數字比較、日期以日期比較、文字以序數比較。不同種類的值（例如文字和數字）無法比較，運算式執行時會失敗，訊息會寫出兩邊的型別。

## 計算欄：`ValueExpression`

```xml
<FormField FieldName="amount" Caption="Amount" DbType="Currency"
           NumberKind="Amount" ReadOnly="true"
           ValueExpression="quantity * unit_price * (1 - discount)" />
```

- 存檔前對 `Added` / `Modified` 列重算（`Unchanged` 列不動，避免誤標為已異動）。
- **捨入**：數值結果依欄位 `NumberKind` 捨入（框架預設：`Amount`→2 位、`Quantity`→0、`UnitPrice`→保留精度…；小數位數取自幣別、單位或公司，見 [ADR-026](../../../maintainers/adr/adr-026-numeric-semantics-rounding.md)）。`Quantity` 或 `Weight` 的計算欄必須宣告 `UnitField`，否則運算時拋出例外。每筆明細先各自捨入，因此由捨入後明細加總而得的合計（round-then-sum）會與明細對得上。
- 計算欄通常搭配 `ReadOnly="true"`。
- 同列多個計算欄可相依：**依宣告順序**求值，後面的看得到前面剛算好的值。

## 明細加總

**主表**的計算欄可以彙總同一表單中某個明細表的列：

```xml
<FormField FieldName="total_amount" Caption="Total" DbType="Currency"
           NumberKind="Amount" ReadOnly="true"
           ValueExpression="Sum(OrderDetail.amount)" />
```

| 彙總 | 讀取 | 結果 | 沒有任何列時 |
|------|------|------|-------------|
| `Sum(Table.field)` | 數值欄位 | `decimal` | `0` |
| `Count(Table)` | 列數 | `int` | `0` |
| `Min(Table.field)`、`Max(Table.field)` | 數值、日期或時間欄位 | 該欄位的型別 | 該欄位的空值 |
| `Count(Table, condition)` | 條件成立的列數 | `int` | `0` |
| `Any(Table, condition)` | 是否至少有一列條件成立 | `bool` | `false` |
| `All(Table, condition)` | 是否每一列條件都成立 | `bool` | `true` |

- `Table` 是 FormSchema 裡明細表的 `TableName`，不是資料庫表名。標記為刪除的列不計入。
- 彙總是運算式的一部分，可以和其他部分組合：`Sum(OrderDetail.amount) * 1.05m`、`Sum(OrderDetail.amount) - discount`。
- **條件讀取的是一筆明細列。** 它是以明細表欄位寫成的 `bool` 運算式，寫法和目標為該明細表的規則相同，也可以呼叫輔助函式：`All(OrderDetail, quantity > 0)`、`Count(OrderDetail, IsBlank(product_id))`。條件不能指名主表欄位，也不能再包含彙總。
- **先捨入再加總。** 每筆明細值先依該欄位自己的小數位數捨入（依它的 `NumberKind`，以及該列的幣別或單位），所以合計等於各列顯示金額的和；結果再照常依主表欄位的 `NumberKind` 捨入。明細欄位本身若是計算欄（`amount = quantity * unit_price`），會先算好再被主表加總。
- **存檔時，合計涵蓋整筆記錄。** 只要主表加總的明細表有新增、異動或刪除的列，主表列就會重算。API 呼叫端若只送出有改的列，合計仍然正確：伺服端計算前會先補上 payload 沒帶的已存明細列。目標為主表、彙總明細表的存檔前規則也一樣，所以 `All(OrderDetail, quantity > 0)` 會檢查整筆記錄的每一列，而不只是送來的列。這種 payload 必須帶著主表列；若只改了合計或規則會讀取的明細卻沒帶主表列，存檔會被拒絕。它也只能帶一筆記錄，而且在這些明細表中帶的每一列都必須有 `sys_rowid` 並屬於這筆記錄：屬於其他記錄的列，不論是新增或修改都會被拒絕，`sys_rowid` 有變更的列也會被拒絕。刪除主表列的 payload 只能一併刪除這筆記錄自己的明細列。未變更的列（主表列也一樣）不會寫入，所以以已存的值計算，而不是呼叫端送來的值。
- **用戶端**：Avalonia 與 Blazor head 在加總的明細列被編輯、新增或刪除時重算主表列。條件指名的欄位也算在加總讀取的欄位內。
- 彙總只能用在主表欄位的 `ValueExpression`，以及目標為主表的 `FormRule`。明細表的運算式與 `DefaultValueExpression` 不能用；`ValidateExpressions`（以及 DefineEditor）會指出這種情形，也會指出不存在的表或欄位，以及彙總無法讀取的欄位型別。

## 欄位預設值：`DefaultValueExpression`

```xml
<FormField FieldName="order_date" Caption="Order Date" DbType="Date"
           DefaultValueExpression="Today()" />
```

- **建立新列時以運算式為準。** 伺服器的 `GetNewData` 與 UI 用戶端新增的列都會求值，並把結果寫過該列原本的初值：依型別的初值（數值為 `0`、文字為空字串、`Guid.Empty`、`Date` 為今天）以及該欄位的字面值 `DefaultValue`。沒有運算式的欄位保留初值或 `DefaultValue`。
- **存檔時只填空欄位。** 伺服器的存檔前處理只對新列中仍為空（沒有值，或為空字串）的欄位再求值一次，所以使用者輸入的值會保留。例外是 `DateTime` 欄位：伺服器會捨棄呼叫端在新列上送來的值，重新以運算式求值（見[時區處理](../database/datetime-timezone.md)）。

## 驗證與前置檢查：`FormRule`

```xml
<Rules>
  <FormRule RuleId="customer_required"
            Condition="customer_rowid != Guid.Empty"
            Message="Please select a customer." />
  <FormRule RuleId="quantity_positive" TargetTable="OrderDetail"
            Condition="quantity &gt; 0"
            Message="Quantity must be greater than zero." />
  <FormRule RuleId="approved_amount"
            When="status == &quot;Approved&quot;"
            Condition="total_amount &gt; 0"
            Message="An approved order must have a positive total." />
</Rules>
```

| 屬性 | 說明 |
|------|------|
| `Condition` | **必須成立**的條件（回傳 bool）；為 `false` 即違規，中斷動作並顯示 `Message` |
| `When` | 選填的**適用條件**；空＝一律套用，`false`＝略過整條規則（視同通過），`true` 才檢查 `Condition` |
| `Message` | 不通過時顯示給使用者的訊息。這是基底文字；表單語系資源中的 `Rule.{RuleId}.Message` 項目負責翻譯，由伺服端依 session 的語系解析 |
| `Trigger` | `BeforeSave`（預設）或 `BeforeDelete` |
| `TargetTable` | 空＝主檔；填明細表名＝對該表**逐列**檢查 |
| `Order` | 同一 trigger 內的求值順序（小的先） |
| `Enabled` | 是否啟用（預設 true） |

目標為主表的規則可以使用[明細加總](#明細加總)的彙總，例如「至少一筆明細」：`Condition="Count(OrderDetail) &gt; 0"`，或「每一筆明細都有數量」：`Condition="All(OrderDetail, quantity &gt; 0)"`。若要指出是哪一筆明細不符，改把規則的目標設為明細表：`TargetTable="OrderDetail"` 搭配 `Condition="quantity &gt; 0"`，會逐列檢查。在規則裡，`Sum` 直接加總明細的儲存值，不先逐列捨入；要和捨入後的合計比較，請比較計算該合計的主表欄位。

> **兩段式判斷**：`When` 決定「這條規則現在該不該檢查」、`Condition` 是「必須成立的驗證」。例：「訂單已核准時，總額必須 > 0」→ `When = status == "Approved"`、`Condition = total_amount > 0`。狀態非 Approved 的單據自動略過。
>
> XML 裡 `>` 要寫 `&gt;`、字串引號要寫 `&quot;`。

### 規則可以信任什麼

存檔前規則依據存檔 payload 裡的值判斷。框架的前端送出的是使用者編輯後的記錄，所以對它們而言，規則看到的就是將要存入的值。能儲存這張表單的 API 呼叫端則可以送出別的值，規則便會依那些值判斷：

- **標成「未變更」的列**不會寫入，但規則仍會讀它。呼叫端標成未變更的主檔列，可以帶著資料庫裡並不存在的值。
- **關聯欄位**（`ref_*`）在選取紀錄時由前端填入，不會儲存，所以規則讀到的是呼叫端送來的值。
- **沒有權限模型的表單**，沒有任何檢查把明細列綁定到正在儲存的記錄：payload 可以改動別筆記錄的明細列，而那筆記錄自己的規則不會執行。

例外是彙總讀取的明細表：伺服端會以儲存的記錄補齊（見[明細加總](#明細加總)）。除此之外，規則防的是經由前端操作的失誤，不是能儲存這張表單的呼叫端。必須對這類呼叫端成立的檢查（例如允許的狀態變更），請寫在 BO 的 `DoBeforeSave`，讀取儲存的記錄判斷。payload 加密保護的是傳輸途中，擋不了持有 session 金鑰的已登入使用者。理由記錄在 [#577](https://github.com/polhem-dev/polhem/issues/577)。

## 用白話撰寫規則

DefineEditor 新增與編輯規則用的規則對話框有 **AI 助手** 分頁。用自己的話描述規則，例如「已出貨的訂單，貨運商一定要填，而且運費不能是 0」，助手會透過 Claude 寫出規則：檢查的資料表、`When`、`Condition`、訊息與訊息的翻譯，並以白話說明規則做什麼，附上測試資料。

- **測試資料由引擎計算，不是模型說了算。** 每一筆都由存檔時執行的同一個計算器求值，標示規則讓該列通過、擋下或不適用。引擎算出的結果與模型宣稱不符時，草稿會退回給模型重寫，所以畫面上的測試資料就是規則實際的行為。
- **草擬的運算式必須通過允許清單。** 以 `FormExpressionCalculator.ValidateUntrustedExpression` 檢查，定義檔的運算式不受此限：在長度與巢狀上限內，只能用欄位、`RegexMatch` 以外的函式、彙總、運算子、字面值與列入清單的成員。
- **修改** 以修正的話延續同一段對話，也用來回答助手的提問。**說明這條規則** 說明對話框中目前的規則並附上測試資料，讓不會讀運算式的人也能檢查既有規則。
- **填入規則** 以草稿填入對話框的資料表、`When`、`Condition` 與訊息；規則使用對話框中的 `RuleId`。按確定之前不會寫入任何東西：按確定後規則存入表單，訊息的翻譯寫入表單語系檔的 `Rule.{RuleId}.Message`：只寫表單已有的英文以外語系，不新增檔案，也不寫客製目錄。
- 你輸入的文字，以及表單的欄位名稱、標題與清單值，會以分頁上顯示的憑證送給 Claude：`ant auth login` 或 `ANTHROPIC_API_KEY`。沒有憑證時，分頁不會送出任何東西。

背後的服務是 [Polhem.Definition.Drafting](../../../src/Polhem.Definition.Drafting/README.zh-TW.md#用白話產生規則) 的 `RuleDrafting`。

## 何時仍需寫 BO（當前邊界）

運算式引擎是**逐列（per-row）**模型。以下情境還不能純宣告，需在自訂 BO 覆寫 `DoBeforeSave` / `DoBeforeDelete`：

- **[明細加總](#明細加總)表格沒有列出的彙總**：如平均、只加總部分列、明細底下再一層的表。
- **需查資料庫**：如「狀態轉移須比對資料庫既存狀態」「自動單號取序列」。

`apps/Polhem.Northwind` 的 `OrderBO` 是實例：明細金額、訂單合計、「至少一筆明細」與必填檢查都已宣告化，只有需查資料庫的邏輯留在 `DoBeforeSave`。

### 自訂 BO 覆寫慣例

```csharp
protected override void DoBeforeSave(SaveContext context)
{
    base.DoBeforeSave(context);   // 先跑規則引擎（預設值 / 計算欄 / BeforeSave 驗證）
    // 再疊上宣告式表達不了的邏輯（DB 查詢…）
}
```

`Save` / `Delete` 已重構為模板方法：授權、記錄範圍、稽核由框架編排層固定處理，你只覆寫 `DoBeforeSave` / `DoSave` / `DoAfterSave`（及 Delete 對應）需要的那一段。
