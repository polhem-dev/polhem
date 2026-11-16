# ADR-066: A master field's expression can add up its detail rows

## Status

**Accepted (2026-11-16)**

Extends [ADR-028](adr-028-expression-rule-engine.md), which left cross-row aggregation outside the expression engine.

## Context

ADR-028 made field computation and validation declarative, one row at a time. A header total, the sum of the line
amounts, stayed in code: `OrderBO` in Northwind overrides `DoBeforeSave` to add up its lines and to require at least
one. The clients never showed the total while the user edited the lines, because only the server computed it.

Document drafting (ADR-064) made the gap visible. A purchase request has a total at the bottom; the drafting tool
told the model that an expression cannot add up detail rows, so the model left the total out or made it a field the
user types, and a saved request showed a total of 0 beside lines that added up to 65.

## Decision

### 1. Aggregates are written in the expression

A master table's `ValueExpression`, and a rule that targets the master table, may call `Sum(Table.field)`,
`Count(Table)`, `Min(Table.field)` and `Max(Table.field)` over a detail table of the same form.

Two other shapes were considered:

- **Declared properties** (`AggregateFunction`, `AggregateTable`, `AggregateField` on `FormField`). Easier to validate,
  but three new attributes in the schema, three new workbook columns and three new drafting parameters, and a total
  with tax would need a hidden field plus a second expression.
- **A rule that assigns a value.** Closest to "do it with a rule", but a `FormRule` means "this must hold, or the save
  stops with this message"; giving it a second behavior blurs that, rules do not take part in the clients' live
  computation, and a field's value would be defined away from the field.

Writing aggregates in the expression needs no new attribute: the schema, the wire, the workbook's formula column and
the drafting tool's parameter carry it unchanged, and an aggregate combines with the rest of the expression
(`Sum(Item.amount) * 1.05m`). Rules use the same functions for validation (`Count(Item) > 0`).

### 2. The calculator computes the aggregate; the engine sees a variable

`DetailAggregateRewriter` replaces each aggregate call with a variable before the expression reaches the engine, and
`FormExpressionCalculator` computes the value in C#. The engine registers no function by these names, so the match is
syntactic and cached per expression; `Math.Max(a, b)`, `Max(a, b)` and text inside a literal are left alone.

The engine still compiles every expression to the one invoker shape the AOT rules require, and reaches no new member
by reflection, so neither `InterpretedInvokerGateTests` nor the trimmer descriptor of `Polhem.Expressions` changes.

### 3. Round, then sum

Each detail value is rounded to its own field's decimals, with the currency or unit of its row, before it is added;
the result is then rounded by the master field as any computed value is. The total is therefore the sum of the
amounts the rows show (the round-then-sum rule of ADR-026). A rule's aggregate adds the stored values as they are: a
rule has no rounding context, and a rule that compares with the total can compare with the master field.

### 4. On save, the total covers the whole record

The save pass computes the detail tables first, then the master, and recomputes an unchanged master row when a detail
table it adds up has a changed row.

The framework's clients send the whole record, but `Save` is an API, and a caller may send only the rows it changed.
Before `DoBeforeSave`, `FormBusinessObject` reads the stored record and adds the detail rows the payload does not
carry, unchanged, so the total adds up every row. The repository writes no unchanged row and the change audit reads
only changes, so the added rows only complete what the aggregates read. A payload that changes rows of an aggregated
detail table without the master row is refused (`PolhemMessages.SaveMasterRowMissing`).

Rejected: recomputing the total in SQL after the write. It would need one statement per dialect, `DoSave`'s
transaction takes no further command, SQLite may store unrounded values so the result would not be round-then-sum,
and it could not support an aggregate inside a larger expression.

### 5. The clients recompute the master row

`FormLiveComputationEngine` recomputes the master row when a detail row is edited in a field the master adds up, or
when a row of such a table is added or deleted. `FormExpressionCalculator.BuildAggregateDependencyMap` says which
tables and fields do.

## Consequences

- A total over detail rows, and "at least one line", no longer need a business object. Northwind's `OrderBO` keeps only
  the logic that reads the database.
- A save of an existing record of a form with totals costs one more read of the record.
- Out of scope: an average, a sum over some of the rows, and a table below a detail table. A business object can still
  compute them in `DoBeforeSave`.
