<!-- source: en/expression-rules.md blob: 2586069e3cfe5ad934aaa4d739447b477e20e706 -->
# 運算式與規則（欄位運算與存檔/刪除前驗證）

[English](../en/expression-rules.md) · [← 文件索引](README.md)

用**宣告式運算式**在 `FormSchema` 定義檔裡做欄位運算與驗證，取代手寫 BO 程式碼。客戶/顧問於設計期即可自訂，不需改程式、重編、重佈。

設計背景與決策見 [ADR-028](../adr/adr-028-expression-rule-engine.zh-TW.md)。

## 三種能力

| 能力 | 載體 | 時機 |
|------|------|------|
| 計算欄 | `FormField.ValueExpression` | 存檔前對新增/異動列重算回填 |
| 欄位預設值 | `FormField.DefaultValueExpression` | 建立新列時（蓋過字面預設值）；存檔時只填新列中仍為空的欄位 |
| 驗證 / 前置檢查 | `FormSchema` 下的 `FormRule` | `BeforeSave` / `BeforeDelete` |

> **後端為權威**：存檔時 `FormBusinessObject.DoBeforeSave` 依定義重算計算欄並覆蓋前端送來的值，接著執行驗證規則；`DoBeforeDelete` 執行刪除規則。Avalonia UI 的即時運算（`FormLiveComputation`；Blazor 元件沒有這項功能）會在使用者編輯時重算欄位，但它只是 UX 預覽：捨入用的是框架預設的小數位數，存檔時由伺服端修正。

## 運算式語法

- **變數 = 欄位名**：直接寫欄位名即可，如 `unit_price * qty`。同列所有欄位都可用。
- **運算子**：C# 語法子集（`+ - * /`、`> >= < <= == !=`、`&& || !`、三元 `? :`、字串 `==`）。
- **可用函式/型別**：輔助函式 `Today()`、`Now()`、`UtcNow()`、`IsNullOrEmpty(s)`、`IsNullOrWhiteSpace(s)`；運算式可直接指名的型別，例如 `Math`（`Math.Round`、`Math.Abs`…）、`Convert`、`DateTime`、`TimeSpan`、`Guid`（如 `customer_rowid != Guid.Empty`）；以及欄位值本身的成員（`name.Length`、`Today().AddDays(1)`）。這些型別來自 DynamicExpresso 的預設集合，加上 `DynamicExpressoEvaluator` 自行加入的；[`ILLink.Descriptors.xml`](../../src/Polhem.Expressions/ILLink.Descriptors.xml) 列出運算式能觸及其成員的每個型別，並由 `TrimmerDescriptorGateTests` 確保這份清單與直譯器一致。

  **時間函式的語意**（見 [ADR-032](../adr/adr-032-datetime-timezone.zh-TW.md)）：

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
- **未知識別字**：未開放的型別名（`File`、`Process`…）或拼錯的欄位名會解析失敗。在伺服端，該次存檔或刪除隨之失敗；在用戶端，該表單的即時運算會自行關閉。
- **這不是安全沙箱。** 值的成員是以反射解析的，解析器擋不住運算式往更深處觸及。運算式之所以安全，是因為它們的來源：遠端呼叫者無法寫入的定義檔（`SaveDefine` 為 `LocalOnly`）。絕對不要用使用者輸入組出運算式文字。
- **經裁剪的應用程式**：運算式呼叫的成員是以反射找到的，裁剪器看不到。`Polhem.Expressions` 內附一份保留這些成員的裁剪描述檔，見[平台支援](platform-support.md#套件內附的裁剪描述檔)。
- **空值**：空欄（`DBNull`）以型別預設值代入（數值 0、字串空字串、`Guid.Empty`…），所以 `unit_price * qty` 遇空值算 0 而不會出錯。

## 計算欄：`ValueExpression`

```xml
<FormField FieldName="amount" Caption="Amount" DbType="Currency"
           NumberKind="Amount" ReadOnly="true"
           ValueExpression="quantity * unit_price * (1 - discount)" />
```

- 存檔前對 `Added` / `Modified` 列重算（`Unchanged` 列不動，避免誤標為已異動）。
- **捨入**：數值結果依欄位 `NumberKind` 捨入（框架預設：`Amount`→2 位、`Quantity`→0、`UnitPrice`→保留精度…；小數位數取自幣別、單位或公司，見 [ADR-026](../adr/adr-026-numeric-semantics-rounding.zh-TW.md)）。`Quantity` 或 `Weight` 的計算欄必須宣告 `UnitField`，否則運算時拋出例外。每筆明細先各自捨入，因此由捨入後明細加總而得的合計（round-then-sum）會與明細對得上。
- 計算欄通常搭配 `ReadOnly="true"`。
- 同列多個計算欄可相依：**依宣告順序**求值，後面的看得到前面剛算好的值。

## 欄位預設值：`DefaultValueExpression`

```xml
<FormField FieldName="order_date" Caption="Order Date" DbType="Date"
           DefaultValueExpression="Today()" />
```

- **建立新列時以運算式為準。** 伺服器的 `GetNewData` 與 UI 用戶端新增的列都會求值，並把結果寫過該列原本的初值：依型別的初值（數值為 `0`、文字為空字串、`Guid.Empty`、`Date` 為今天）以及該欄位的字面值 `DefaultValue`。沒有運算式的欄位保留初值或 `DefaultValue`。
- **存檔時只填空欄位。** 伺服器的存檔前處理只對新列中仍為空（沒有值，或為空字串）的欄位再求值一次，所以使用者輸入的值會保留。例外是 `DateTime` 欄位：伺服器會捨棄呼叫端在新列上送來的值，重新以運算式求值（見[時區處理](datetime-timezone.md)）。

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

> **兩段式判斷**：`When` 決定「這條規則現在該不該檢查」、`Condition` 是「必須成立的驗證」。例：「訂單已核准時，總額必須 > 0」→ `When = status == "Approved"`、`Condition = total_amount > 0`。狀態非 Approved 的單據自動略過。
>
> XML 裡 `>` 要寫 `&gt;`、字串引號要寫 `&quot;`。

## 何時仍需寫 BO（當前邊界）

運算式引擎是**逐列（per-row）**模型。以下情境還不能純宣告，需在自訂 BO 覆寫 `DoBeforeSave` / `DoBeforeDelete`：

- **跨列聚合**：如「表頭合計 = 明細金額加總」「至少一筆明細」——需跨列運算。
- **需查資料庫**：如「狀態轉移須比對資料庫既存狀態」「自動單號取序列」。

`apps/Polhem.Northwind` 的 `OrderBO` 是實例：明細金額與必填檢查已宣告化，只有上述聚合/DB 相依留在 `DoBeforeSave`。

### 自訂 BO 覆寫慣例

```csharp
protected override void DoBeforeSave(SaveContext context)
{
    base.DoBeforeSave(context);   // 先跑規則引擎（預設值 / 計算欄 / BeforeSave 驗證）
    // 再疊上宣告式表達不了的邏輯（聚合、DB 查詢…）
}
```

`Save` / `Delete` 已重構為模板方法：授權、記錄範圍、稽核由框架編排層固定處理，你只覆寫 `DoBeforeSave` / `DoSave` / `DoAfterSave`（及 Delete 對應）需要的那一段。
