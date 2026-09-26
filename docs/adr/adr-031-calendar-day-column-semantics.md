# ADR-031: Calendar-day column semantics are carried by an explicit marker, not by changing the CLR type

[繁體中文](adr-031-calendar-day-column-semantics.zh-TW.md)

## Status

Accepted (2026-07-25)

> All three phases have been implemented (commits
> [`fddb38f6`](https://github.com/jeff377/bee-library/commit/fddb38f6) /
> [`c7782308`](https://github.com/jeff377/bee-library/commit/c7782308) /
> [`c5578a42`](https://github.com/jeff377/bee-library/commit/c5578a42)).
> For how consumers use it, see `docs/en/temporal-types.md`.

## Context

`FieldDbType` deliberately distinguishes `Date` from `DateTime`: the definition layer made "calendar day vs instant"
clear long ago. .NET itself has matching types too: `DateOnly` (.NET 6+) is a calendar day, and `DateTime` is an
instant.

The problem is not that the language lacks expressiveness, but that **the cross-layer DTO is `DataSet`, and
`DataColumn` has no usable storage type for a calendar day**. Only a handful of BCL types get native storage for
`DataColumn.DataType`, and `DateOnly` is not among them: it falls into `ObjectStorage`, which compares types strictly
and does no conversion at all. That in turn breaks the framework's string-in/string-out binding layer and permanently
loses `RowFilter` / `Compute` (measurements in "Options considered", option 1). A calendar day on a `DataSet` can
therefore only be carried as a `DateTime`, and the semantics the definition layer distinguishes are flattened at the
CLR level:

```csharp
case FieldDbType.Date:
case FieldDbType.DateTime:
    return typeof(DateTime);        // DbTypeConverter.ToType
```

As a consequence, the wire payload loses this information as well. `SerializableDataColumn.DataType` (MessagePack) and
JSON's `"type"` field carry a `FieldDbType` for each column, but both come from
`DbTypeConverter.ToFieldDbType(col.DataType)`, **keyed by the CLR type**. And the CLR type of a calendar-day column is
`DateTime`, so **a calendar-day column is always marked as `FieldDbType.DateTime` on the wire**.

`Date` is not the only thing flattened. Other pairs that share one CLR type and cannot be recovered are
`Text`/`String` (both `string`), `Currency`/`Decimal` (both `decimal`) and `AutoIncrement`/`Integer` (both `int`).

Note that **the DB parameter layer is not flattened**: `FieldDbType.Date` → `DbType.Date`, so the database always knows
it is a calendar day. Only the CLR representation and the wire marker are collapsed.

### Why it needs to be addressed

1. **Schema-less scenarios are the real gap.** Under the Repository dual-track strategy, the `DataTable` produced by
   reports / batch jobs (AnyCode) has no `FormSchema` behind it, so the consumer **cannot look up the column
   semantics**. The same goes for a pure JS client: once the payload describes itself, the front end does not need to
   fetch the schema separately.
2. **Cross-time-zone deployments need a safe default.** Without column semantics, a date column on a schema-less path
   is treated as an instant and converted between time zones, shifting it across a day boundary (see
   [ADR-032](adr-032-datetime-timezone.md)).
3. **The wire already has the slot; it is just filled in wrongly.** Changing only its **source**, from inferring it
   from the CLR type to reading an explicit marker first, restores the payload's ability to describe itself, **without
   adding any wire field**.

## Options considered

### 1. Let the CLR type carry the semantics: `ToType(Date)` returns `typeof(DateOnly)` (rejected)

The cleanest direction: "declaring the column type decides the semantics", which removes the "forgot to mark it"
failure mode at the root. The initial assessment put the blast radius inside the framework at 0 (no direct
`(DateTime)row[...]` cast anywhere in the repository).

**Rejected after measurement** (2026-07-25, `net10.0`). `DateOnly` is not a native storage type of `DataColumn`; it
goes through `ObjectStorage`, which **compares types strictly and does no conversion at all**:

| Operation | `DateTime` column (current) | `DateOnly` column |
|-----------|-----------------------------|-------------------|
| `row["d"] = "2026-07-25"` (string) | ✅ Parsed automatically | ❌ `ArgumentException` |
| `row["d"] = new DateTime(...)` | ✅ | ❌ `ArgumentException` |
| `row["dt"] = new DateOnly(...)` | — | ❌ `ArgumentException` (`DateOnly` does not implement `IConvertible`) |
| `Convert.ChangeType(v, typeof(DateTime))` | ✅ | ❌ `InvalidCastException` |
| `DataView.RowFilter = "d >= #...#"` | ✅ | ❌ `EvaluateException` |
| `DataTable.Compute("MAX(d)")` | ✅ | ❌ `DataException` |
| `DataView.Sort` / `PrimaryKey.Find` / `Expression` column | ✅ | ✅ |
| `DataColumn.DefaultValue` | `DateTime.MinValue` | `DBNull` |

Three reasons for rejecting it:

1. **It breaks the framework's own binding layer.** The UI binding layer works with strings in and out (`DateEdit`
   writes back `date.ToString("yyyy-MM-dd")`). It works today **purely because `DataColumn` automatically parses
   strings for a `DateTime` column**. Once the column becomes `DateOnly`, every date pick throws
   `ArgumentException`. The initial "blast radius of 0" only surveyed the read direction; **the write direction is
   where it breaks**.
2. **It permanently loses `RowFilter` / `Compute`.** The BCL `DataTable` expression engine does not know `DateOnly`,
   and there is no workaround. For an ERP, being unable to apply a `RowFilter` to `order_date` is a real functional
   regression.
3. **The reverse direction is blocked too**, so there is no runnable intermediate state during a phased rollout; it
   would have to ship in a single release.

### 2. An explicit marker in `ExtendedProperties` (adopted)

`DataColumn.DataType` stays exactly as it is, and the calendar-day semantics are carried by
`DataColumn.ExtendedProperties` instead. None of the three costs above exists. The cost is that the "forgot to mark
it" silent failure mode remains (see "Consequences").

### 3. A separate semantics field on the wire (rejected)

Add a field such as `IsDateOnly` / `Semantics` to `SerializableDataColumn` alongside `DataType`. The semantics would be
more explicit, but the wire gets fatter, and it overlaps with the existing `DataType` field, which is already a
`FieldDbType` and merely holds an inaccurate value. Fixing the source is smaller than adding a field.

## Decision

**Calendar-day semantics are carried by an explicit marker in `DataColumn.ExtendedProperties`;
`DataColumn.DataType` stays unchanged.**

| Layer | Decision |
|-------|----------|
| Storage | `DataColumn.DataType` stays `DateTime`; zero impact on the binding layer / `RowFilter` / `Sort` / `Compute` / existing casts |
| Semantics | `DataColumn.ExtendedProperties` records the declared `FieldDbType`, accessed through `ApplyFieldDbType` / `ResolveFieldDbType` in `DataColumnExtensions` |
| Wire | **No new field**: MessagePack's `SerializableDataColumn.DataType` and JSON's `"type"` are already a `FieldDbType`; only the value filled in becomes accurate |
| Reading values | `ValueUtilities.CDateOnly` returns `DateOnly`, so the consuming side can tell a calendar day from an instant |

### Who is responsible for the marker (two paths)

The rule in one sentence: **for SQL the framework generates, the framework marks; for SQL the caller writes, the
caller marks.**

| Path | Source of the SQL | Handling of the marker |
|------|-------------------|------------------------|
| **One** | Generated from the definitions (schema-driven queries such as `DataFormRepository`) | **Handled by the framework**: after the `DataTable` is fetched, the column types are replayed from `FormTable.Fields` |
| **Two** | The caller issues SQL directly (AnyCode / reports / batch jobs) | **Declared explicitly by the caller**: `table.SetDateColumns(...)` or `DbCommandSpec.DateColumns` |

**"Global detection through `DbDataReader.GetDataTypeName`" was evaluated and rejected**: measurements showed that
SQLite always returns `TEXT` for expression columns (it cannot tell), and SQLite is exactly the development / test
environment, so it would make **the development environment behave differently from production**; and to insert the
type check we would have to give up `adapter.Fill` for a hand-written reader loop, a cost **every query** would pay.

### The semantic decision has a single implementation

Wire serialization has two parallel implementations, MessagePack and JSON, living in different packages
(`Polhem.Api.Core` and `Polhem.Base`). The logic "read the marker first, infer only when unmarked" is extracted into a
single helper in `Polhem.Base.Data.DataColumnExtensions`, shared by both converters. Putting it in `Polhem.Base` is
**a necessity, not a preference**: it is the only lower layer the two have in common.

## Consequences

- **Positive**:
  - Schema-less payloads describe themselves; reports / AnyCode / JS clients can identify calendar-day columns without
    fetching the schema separately.
  - The cross-time-zone design (D4 of [ADR-032](adr-032-datetime-timezone.md)) gets a reliable basis for deciding "do
    not convert".
  - It also fixes the markers of the three other pairs flattened the same way: `Text`/`String`, `Currency`/`Decimal`
    and `AutoIncrement`/`Integer`. Because `ToType` gives the same result for these values, the CLR types rebuilt on
    the client are not affected at all.
  - Existing clients are not affected: the payload's structure and size do not change; only the `FieldDbType` values
    become accurate.
- **The silent failure mode that remains (the only cost of this decision compared with option 1)**: on path two (SQL
  written by the BO itself), a calendar-day column the caller forgot to declare is still marked `DateTime` on the
  wire. The mitigations: the helper takes a single line, a wrong column name and a wrong `DbCommandKind` both throw
  (rather than being silently skipped), and the documentation states this division of responsibility explicitly.
- **Breaking change**: `ValueUtilities.CDate` is renamed to `CDateOnly`, and its return type changes from `DateTime`
  to `DateOnly` (the `defaultValue` parameter changes to match). The rename makes `CDateOnly` / `CDateTime` named after
  their own return types, so callers know what they get without looking it up. An external caller's
  `DateTime d = ValueUtilities.CDate(x)` becomes a **compile error** rather than a runtime failure; to migrate, switch
  to `DateOnly` or use `CDateTime` instead. There is only one caller inside the framework.
- **Needs ongoing attention**: whether `ExtendedProperties` survive copy paths such as `DataTable.Merge()` /
  `DataView.ToTable()`. Where they are lost, the logic silently falls back to "infer from the CLR type", and the
  symptom is that the marker on the wire turns back into `DateTime`.

## Related

- ADR-026 (numeric semantics and rounding): the same family of "definition-layer semantics must reach through to the
  data layer".
- ADR-029 (field names are always lowercase): likewise a decision to "align the wire representation with the
  definition layer".
- ADR-030 (MessagePack name-based keys): another decision about the wire representation.
- `docs/en/temporal-types.md`: guidance for consumers (including JS/TS), and the cross-layer comparison of the three
  time semantics.
