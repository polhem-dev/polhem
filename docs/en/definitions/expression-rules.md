# Expressions and Rules (Field Computation and Pre-Save / Pre-Delete Validation)

[繁體中文](../../zh-TW/definitions/expression-rules.md) · [← Docs Index](../README.md)

Use **declarative expressions** inside the `FormSchema` definition file for field computation and validation, instead of hand-written business object code. Customers and consultants can customise the behaviour at design time — no code change, no rebuild, no redeployment.

For the background and the decision itself, see [ADR-028](../../../maintainers/adr/adr-028-expression-rule-engine.md).

## Three Capabilities

| Capability | Carrier | When it runs |
|------------|---------|--------------|
| Computed field | `FormField.ValueExpression` | Before save, recomputed and written back for added / modified rows (and for the master row when the detail rows it adds up change) |
| Field default | `FormField.DefaultValueExpression` | When a new row is created (over the literal default); at save, only where a new row's field is still empty |
| Validation / precondition | `FormRule` under `FormSchema` | `BeforeSave` / `BeforeDelete` |

> **The backend is authoritative.** On save, `FormBusinessObject.DoBeforeSave` recomputes computed fields from the definition and overwrites whatever the client submitted, then runs the validation rules; `DoBeforeDelete` runs the delete rules. The live computation of the Avalonia and Blazor heads, Server and WebAssembly alike (`FormLiveComputation` in `Polhem.UI.Avalonia` and in `Polhem.Web.Blazor`, over the shared `FormLiveComputationEngine`) recomputes fields as the user edits, but it is a UX preview only: it rounds with the framework's default decimal places, and the server corrects the values on save.

## Expression Syntax

- **A variable is a field name.** Write the field name directly, e.g. `unit_price * qty`. Every field on the same row is available. A master table's expressions can also add up a detail table; see [Totals Over Detail Rows](#totals-over-detail-rows).
- **Operators**: a subset of C# syntax (`+ - * /`, `> >= < <= == !=`, `&& || !`, the ternary `? :`, and string `==`).
- **Available functions and types**: the helper functions `Today()`, `Now()`, `UtcNow()`, `IsNullOrEmpty(s)` and `IsNullOrWhiteSpace(s)`; the types an expression can name, such as `Math` (`Math.Round`, `Math.Abs`, …), `Convert`, `DateTime`, `TimeSpan` and `Guid` (e.g. `customer_rowid != Guid.Empty`); and the members of a field's value (`name.Length`, `Today().AddDays(1)`). The types come from DynamicExpresso's default set plus the ones `DynamicExpressoEvaluator` adds; [`ILLink.Descriptors.xml`](../../../src/Polhem.Expressions/ILLink.Descriptors.xml) lists every type whose members an expression can reach, and `TrimmerDescriptorGateTests` keeps that list in line with the interpreter.

  **Semantics of the time functions** (see [ADR-032](../../../maintainers/adr/adr-032-datetime-timezone.md)):

  | Function | Returns | Basis |
  |----------|---------|-------|
  | `Today()` | `DateOnly` | Today **in the user's time zone**. This is what you want for cases like defaulting a leave date to today — a user in New York filing against a Taipei company still gets the Taipei date |
  | `Now()` | `DateTime` (`Kind` is `Unspecified`) | The current moment, on the same basis as the instants in the surrounding `DataSet`: the user's time zone during client-side live preview, UTC in the server's pre-save computation and validation. Use it to write into or compare with a `DateTime` field |
  | `UtcNow()` | `DateTime` (`Kind` is `Unspecified`) | The raw current UTC reading, which does not change with the side it runs on |

  `Today()` returns `DateOnly` rather than `DateTime` because a calendar day is always expressed as `DateOnly` in the framework. The `DataSet` cell is the sole exception, since a `DataColumn` can only carry a calendar day as `DateTime`. You may write `Today()` into either a `Date` or a `DateTime` field; the framework performs the conversion when writing the cell.

  > **To write into or compare with a `DateTime` field, use `Now()`, not `UtcNow()`.** A client-side `DataSet` is held in the user's time zone, so a `UtcNow()` value written during live preview shows up off by the user's offset. On save the server does not take `DateTime` values from the client — it evaluates the expression again or keeps the stored value — so the saved data is unaffected; what is wrong is the value on screen before saving.
- **Numeric types do not mix implicitly.** A variable has the CLR type of its field's `DbType` (the mapping is [`DbTypeConverter.ToType`](../../../src/Polhem.Core/Data/DbTypeConverter.cs)), and the operators follow C#: a `decimal` does not combine with a `double`. A literal with a decimal point is a `double`, so `amount * 1.05` fails to parse when `amount` is a `Currency` or `Decimal` field. Write the literal as a decimal, `amount * 1.05m`, or keep to integers, `amount * 105 / 100`.
- **When an expression fails to parse**: a type name that is not exposed (`File`, `Process`, …), a misspelled field name, or operand types that do not combine (above). On the server the save or delete then fails. On the client, live computation stops for that form for as long as it is open: the head shows a message naming the field and the reason, and the computed fields are calculated by the server on save. The Avalonia `FormView` raises `LiveComputationDegraded` and the Blazor `FormPage` writes a warning to its `ILogger`, both with the expression, so a host can log what to fix. DefineEditor catches these before they reach either side: when it validates or saves a FormSchema it parses every field and rule expression this way (`FormExpressionCalculator.ValidateExpressions`) and lists the ones that fail.
- **Not a security sandbox.** Members of a value are resolved by reflection, so the parser does not stop an expression from reaching further. What keeps expressions safe is where they come from: definition files, which remote callers cannot write (`SaveDefine` is `LocalOnly`). Never build expression text from user input.
- **Trimmed apps**: the members an expression calls are found by reflection, which the trimmer cannot see. `Polhem.Expressions` ships a trimmer descriptor that keeps them; see [Platform Support](../getting-started/platform-support.md#trimmer-descriptors-shipped-in-the-packages).
- **Null handling**: an empty field (`DBNull`) is substituted with its type default (`0` for numbers, an empty string for text, `Guid.Empty`, …), so `unit_price * qty` evaluates to `0` on empty input rather than failing.

## Computed Fields: `ValueExpression`

```xml
<FormField FieldName="amount" Caption="Amount" DbType="Currency"
           NumberKind="Amount" ReadOnly="true"
           ValueExpression="quantity * unit_price * (1 - discount)" />
```

- Recomputed before save for `Added` / `Modified` rows. `Unchanged` rows are left alone, so they are never falsely marked as modified.
- **Rounding** follows the field's `NumberKind` (framework defaults: `Amount` → 2 decimals, `Quantity` → 0, `UnitPrice` → full precision, …; the decimals come from the currency, the unit or the company — see [ADR-026](../../../maintainers/adr/adr-026-numeric-semantics-rounding.md)). A computed `Quantity` or `Weight` field must declare its `UnitField`; without one the computation throws. Each detail row is rounded first, so a total summed from the rounded rows (round-then-sum) reconciles with them.
- Computed fields are usually paired with `ReadOnly="true"`.
- Several computed fields on the same row may depend on each other: evaluation follows **declaration order**, so a later expression sees the values just computed by earlier ones.

## Totals Over Detail Rows

A computed field of the **master table** can aggregate the rows of a detail table of the same form:

```xml
<FormField FieldName="total_amount" Caption="Total" DbType="Currency"
           NumberKind="Amount" ReadOnly="true"
           ValueExpression="Sum(OrderDetail.amount)" />
```

| Aggregate | Reads | Result | Over no rows |
|-----------|-------|--------|--------------|
| `Sum(Table.field)` | A numeric field | `decimal` | `0` |
| `Count(Table)` | The rows | `int` | `0` |
| `Min(Table.field)`, `Max(Table.field)` | A numeric, date or time field | The field's type | The field's empty value |
| `Count(Table, condition)` | The rows the condition holds for | `int` | `0` |
| `Any(Table, condition)` | Whether the condition holds for at least one row | `bool` | `false` |
| `All(Table, condition)` | Whether the condition holds for every row | `bool` | `true` |

- `Table` is the detail table's `TableName` in the FormSchema, not the database table. Rows marked deleted are not counted.
- An aggregate is part of the expression, so it combines with the rest: `Sum(OrderDetail.amount) * 1.05m`, `Sum(OrderDetail.amount) - discount`.
- **A condition reads one detail row.** It is a `bool` expression over the detail table's fields, written the way a rule that targets that table is, and it may call the helper functions: `All(OrderDetail, quantity > 0)`, `Count(OrderDetail, IsBlank(product_id))`. It cannot name a master field or hold another aggregate.
- **Round, then sum.** Each detail value is first rounded to its own field's decimals (its `NumberKind`, with the currency or unit of its row), so the total equals the sum of the amounts the rows show. The result is then rounded by the master field's `NumberKind` as usual. A detail field that is itself computed (`amount = quantity * unit_price`) is computed before the master adds it up.
- **On save, the total covers the whole record.** The master row is recomputed whenever a detail table it adds up has an added, modified or deleted row. A caller of the API that saves only the rows it changed still gets the right total: the server adds the stored detail rows the payload does not carry before it computes. The same holds for a before-save rule on the master table that aggregates a detail table, so `All(OrderDetail, quantity > 0)` checks every row of the record, not only the rows sent. Such a payload must carry the master row; one that changes detail rows a total or rule reads without it is refused. It must also carry a single record, and every detail row it carries in those tables must have its `sys_rowid` and belong to that record: a row of another record is refused, whether added or modified. An unchanged row counts with its stored values, not with the values the caller sent, since it is not written.
- **On the client**, the Avalonia and Blazor heads recompute the master row when a detail row it adds up is edited, added or deleted. A field a condition names counts as one it adds up.
- Aggregates are available only in a master field's `ValueExpression` and in a `FormRule` that targets the master table. A detail table's expressions and `DefaultValueExpression` cannot use them; `ValidateExpressions` (and DefineEditor) report it, along with a table or field that does not exist and a field type the aggregate cannot read.

## Field Defaults: `DefaultValueExpression`

```xml
<FormField FieldName="order_date" Caption="Order Date" DbType="Date"
           DefaultValueExpression="Today()" />
```

- **When a new row is created, the expression wins.** The server's `GetNewData` and the UI client's new row both evaluate it and write the result over whatever the row was seeded with: the per-type seed (`0` for numbers, an empty string for text, `Guid.Empty`, today for a `Date`) and the field's literal `DefaultValue`. A field without an expression keeps its seed or `DefaultValue`.
- **At save, it fills only empty fields.** The server's before-save pass evaluates the expression again for a new row only where the field is still empty (no value, or an empty string), so a value the user entered is kept. The exception is a `DateTime` field: the server discards the caller's value on a new row and evaluates the expression again (see [Time Zones](../database/datetime-timezone.md)).

## Validation and Preconditions: `FormRule`

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

| Attribute | Description |
|-----------|-------------|
| `Condition` | The condition that **must hold** (returns bool). A `false` result is a violation: the action is aborted and `Message` is shown |
| `When` | Optional **applicability** condition. Empty means always apply; `false` skips the whole rule (treated as passing); only `true` proceeds to check `Condition` |
| `Message` | The message shown to the user when the rule fails. It is the base text; a language resource entry `Rule.{RuleId}.Message` of the form translates it, resolved on the server in the session's culture |
| `Trigger` | `BeforeSave` (default) or `BeforeDelete` |
| `TargetTable` | Empty targets the master table; a detail table name checks that table **row by row** |
| `Order` | Evaluation order within the same trigger (lower runs first) |
| `Enabled` | Whether the rule is active (default true) |

A rule on the master table can use the aggregates of [Totals Over Detail Rows](#totals-over-detail-rows), such as "at least one line": `Condition="Count(OrderDetail) &gt; 0"`, or "every line has a quantity": `Condition="All(OrderDetail, quantity &gt; 0)"`. To say which line fails, target the detail table instead: `TargetTable="OrderDetail"` with `Condition="quantity &gt; 0"` checks each row on its own. In a rule, `Sum` adds the detail values as they are stored, without rounding each one first; to compare with a rounded total, compare with the master field that computes it.

> **The two-part test**: `When` decides whether this rule should be checked at all right now, and `Condition` is the validation that must hold. For example, "an approved order must have a positive total" becomes `When = status == "Approved"` with `Condition = total_amount > 0`. Orders in any other status are skipped automatically.
>
> Inside XML, write `>` as `&gt;` and a string quote as `&quot;`.

## When a Business Object Is Still Required (Current Boundary)

The expression engine is a **per-row** model. The following cases cannot yet be expressed declaratively and require overriding `DoBeforeSave` / `DoBeforeDelete` in a custom business object:

- **Aggregates beyond `Sum`, `Count`, `Min` and `Max` of a detail table**, such as an average, a sum over only some of the rows, or a table below a detail table.
- **Database lookups**, such as "a status transition must be checked against the state already stored" or "fetch the next number from a sequence".

`OrderBO` in `apps/Polhem.Northwind` is a worked example: the detail amounts, the order total, "at least one line" and the required-field checks are declarative, leaving only the database-dependent logic in `DoBeforeSave`.

### Custom Business Object Override Convention

```csharp
protected override void DoBeforeSave(SaveContext context)
{
    base.DoBeforeSave(context);   // Run the rule engine first (defaults, computed fields, BeforeSave validation).
    // Then layer on the logic that declarations cannot express, such as database queries.
}
```

`Save` and `Delete` have been refactored into template methods: authorization, record scope and auditing are orchestrated by the framework, and you override only the part you need — `DoBeforeSave` / `DoSave` / `DoAfterSave`, and the matching Delete hooks.
