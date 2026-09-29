# Expressions and Rules (Field Computation and Pre-Save / Pre-Delete Validation)

[繁體中文](../zh-TW/expression-rules.md) · [← Docs Index](README.md)

Use **declarative expressions** inside the `FormSchema` definition file for field computation and validation, instead of hand-written business object code. Customers and consultants can customise the behaviour at design time — no code change, no rebuild, no redeployment.

For the background and the decision itself, see [ADR-028](../../maintainers/adr/adr-028-expression-rule-engine.md).

## Three Capabilities

| Capability | Carrier | When it runs |
|------------|---------|--------------|
| Computed field | `FormField.ValueExpression` | Before save, recomputed and written back for added / modified rows |
| Field default | `FormField.DefaultValueExpression` | When a new row is created (over the literal default); at save, only where a new row's field is still empty |
| Validation / precondition | `FormRule` under `FormSchema` | `BeforeSave` / `BeforeDelete` |

> **The backend is authoritative.** On save, `FormBusinessObject.DoBeforeSave` recomputes computed fields from the definition and overwrites whatever the client submitted, then runs the validation rules; `DoBeforeDelete` runs the delete rules. The Avalonia UI's live computation (`FormLiveComputation`; the Blazor components have none) recomputes fields as the user edits, but it is a UX preview only: it rounds with the framework's default decimal places, and the server corrects the values on save.

## Expression Syntax

- **A variable is a field name.** Write the field name directly, e.g. `unit_price * qty`. Every field on the same row is available.
- **Operators**: a subset of C# syntax (`+ - * /`, `> >= < <= == !=`, `&& || !`, the ternary `? :`, and string `==`).
- **Available functions and types**: the helper functions `Today()`, `Now()`, `UtcNow()`, `IsNullOrEmpty(s)` and `IsNullOrWhiteSpace(s)`; the types an expression can name, such as `Math` (`Math.Round`, `Math.Abs`, …), `Convert`, `DateTime`, `TimeSpan` and `Guid` (e.g. `customer_rowid != Guid.Empty`); and the members of a field's value (`name.Length`, `Today().AddDays(1)`). The types come from DynamicExpresso's default set plus the ones `DynamicExpressoEvaluator` adds; [`ILLink.Descriptors.xml`](../../src/Polhem.Expressions/ILLink.Descriptors.xml) lists every type whose members an expression can reach, and `TrimmerDescriptorGateTests` keeps that list in line with the interpreter.

  **Semantics of the time functions** (see [ADR-032](../../maintainers/adr/adr-032-datetime-timezone.md)):

  | Function | Returns | Basis |
  |----------|---------|-------|
  | `Today()` | `DateOnly` | Today **in the user's time zone**. This is what you want for cases like defaulting a leave date to today — a user in New York filing against a Taipei company still gets the Taipei date |
  | `Now()` | `DateTime` (`Kind` is `Unspecified`) | The current moment, on the same basis as the instants in the surrounding `DataSet`: the user's time zone during client-side live preview, UTC in the server's pre-save computation and validation. Use it to write into or compare with a `DateTime` field |
  | `UtcNow()` | `DateTime` (`Kind` is `Unspecified`) | The raw current UTC reading, which does not change with the side it runs on |

  `Today()` returns `DateOnly` rather than `DateTime` because a calendar day is always expressed as `DateOnly` in the framework. The `DataSet` cell is the sole exception, since a `DataColumn` can only carry a calendar day as `DateTime`. You may write `Today()` into either a `Date` or a `DateTime` field; the framework performs the conversion when writing the cell.

  > **To write into or compare with a `DateTime` field, use `Now()`, not `UtcNow()`.** A client-side `DataSet` is held in the user's time zone, so a `UtcNow()` value written during live preview shows up off by the user's offset. On save the server does not take `DateTime` values from the client — it evaluates the expression again or keeps the stored value — so the saved data is unaffected; what is wrong is the value on screen before saving.
- **Unknown identifiers**: a type name that is not exposed (`File`, `Process`, …) or a misspelled field name fails to parse. On the server the save or delete then fails; on the client, live computation switches itself off for that form.
- **Not a security sandbox.** Members of a value are resolved by reflection, so the parser does not stop an expression from reaching further. What keeps expressions safe is where they come from: definition files, which remote callers cannot write (`SaveDefine` is `LocalOnly`). Never build expression text from user input.
- **Trimmed apps**: the members an expression calls are found by reflection, which the trimmer cannot see. `Polhem.Expressions` ships a trimmer descriptor that keeps them; see [Platform Support](platform-support.md#trimmer-descriptors-shipped-in-the-packages).
- **Null handling**: an empty field (`DBNull`) is substituted with its type default (`0` for numbers, an empty string for text, `Guid.Empty`, …), so `unit_price * qty` evaluates to `0` on empty input rather than failing.

## Computed Fields: `ValueExpression`

```xml
<FormField FieldName="amount" Caption="Amount" DbType="Currency"
           NumberKind="Amount" ReadOnly="true"
           ValueExpression="quantity * unit_price * (1 - discount)" />
```

- Recomputed before save for `Added` / `Modified` rows. `Unchanged` rows are left alone, so they are never falsely marked as modified.
- **Rounding** follows the field's `NumberKind` (framework defaults: `Amount` → 2 decimals, `Quantity` → 0, `UnitPrice` → full precision, …; the decimals come from the currency, the unit or the company — see [ADR-026](../../maintainers/adr/adr-026-numeric-semantics-rounding.md)). A computed `Quantity` or `Weight` field must declare its `UnitField`; without one the computation throws. Each detail row is rounded first, so a total summed from the rounded rows (round-then-sum) reconciles with them.
- Computed fields are usually paired with `ReadOnly="true"`.
- Several computed fields on the same row may depend on each other: evaluation follows **declaration order**, so a later expression sees the values just computed by earlier ones.

## Field Defaults: `DefaultValueExpression`

```xml
<FormField FieldName="order_date" Caption="Order Date" DbType="Date"
           DefaultValueExpression="Today()" />
```

- **When a new row is created, the expression wins.** The server's `GetNewData` and the UI client's new row both evaluate it and write the result over whatever the row was seeded with: the per-type seed (`0` for numbers, an empty string for text, `Guid.Empty`, today for a `Date`) and the field's literal `DefaultValue`. A field without an expression keeps its seed or `DefaultValue`.
- **At save, it fills only empty fields.** The server's before-save pass evaluates the expression again for a new row only where the field is still empty (no value, or an empty string), so a value the user entered is kept. The exception is a `DateTime` field: the server discards the caller's value on a new row and evaluates the expression again (see [Time Zones](datetime-timezone.md)).

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

> **The two-part test**: `When` decides whether this rule should be checked at all right now, and `Condition` is the validation that must hold. For example, "an approved order must have a positive total" becomes `When = status == "Approved"` with `Condition = total_amount > 0`. Orders in any other status are skipped automatically.
>
> Inside XML, write `>` as `&gt;` and a string quote as `&quot;`.

## When a Business Object Is Still Required (Current Boundary)

The expression engine is a **per-row** model. The following cases cannot yet be expressed declaratively and require overriding `DoBeforeSave` / `DoBeforeDelete` in a custom business object:

- **Cross-row aggregation**, such as "the header total is the sum of the detail amounts" or "at least one detail row is required" — both need computation across rows.
- **Database lookups**, such as "a status transition must be checked against the state already stored" or "fetch the next number from a sequence".

`OrderBO` in `apps/Polhem.Northwind` is a worked example: the detail amounts and required-field checks have been made declarative, leaving only the aggregation and database-dependent logic in `DoBeforeSave`.

### Custom Business Object Override Convention

```csharp
protected override void DoBeforeSave(SaveContext context)
{
    base.DoBeforeSave(context);   // Run the rule engine first (defaults, computed fields, BeforeSave validation).
    // Then layer on the logic that declarations cannot express, such as aggregation or database queries.
}
```

`Save` and `Delete` have been refactored into template methods: authorization, record scope and auditing are orchestrated by the framework, and you override only the part you need — `DoBeforeSave` / `DoSave` / `DoAfterSave`, and the matching Delete hooks.
