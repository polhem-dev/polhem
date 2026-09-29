# ADR-033: Time-of-day semantics (`FieldDbType.Time`) carried as a fixed-width string

## Status

**Accepted (2026-07-27)**. The decision has been carried out.

## Context

The framework originally provided only two time semantics: **calendar day** (`FieldDbType.Date`,
[ADR-031](adr-031-calendar-day-column-semantics.md)) and **instant** (`FieldDbType.DateTime`,
[ADR-032](adr-032-datetime-timezone.md)).

A third semantics was missing: **time of day**, a wall-clock position within a day that is not tied to a particular
date. Shift start and end times, business hours and reminder times all belong to this kind. The consequence of lacking
a type is not only expressiveness: the UI has no way of knowing it should provide a time-of-day editor, and reports and
schema-less consumers cannot tell that a string column is actually a time of day.

The terms in this ADR are fixed as four mutually exclusive words:

| Term | Meaning | Carrier |
|----|------|------|
| Calendar day | Which day | `DateOnly` / `Date` |
| **Time of day** | What time (within a day) | Semantically `TimeOnly`, stored as a fixed-width string / `Time` |
| Instant | What time on which day | `DateTime` / `DateTime` |
| Duration | How long | `TimeSpan` (no corresponding `FieldDbType` yet) |

## Decision

**The value of `FieldDbType.Time` is carried as a fixed-width 5-character string `"HH:mm"`, in both the database and
the `DataSet`; code obtains a `TimeOnly` through `ValueUtilities.CTimeOnly`.**

| Layer | Type |
|----|------|
| DB column | `nchar(5)` (SQL Server) / `char(5)` (PostgreSQL) / `CHAR(5)` (MySQL) / `VARCHAR(5)` (SQLite) / `VARCHAR2(5)` (Oracle) |
| `DataColumn.DataType` | `typeof(string)` |
| Value access layer | `ValueUtilities.CTimeOnly(object) → TimeOnly?` |
| Normalization | `ValueUtilities.CTimeString(object) → "HH:mm"` (an empty string when not filled in or malformed) |

**The value range is `00:00`–`23:59`, with minute precision.** Anything that needs seconds, such as clock-in records,
is an **instant** and should use `DateTime`.

Five accompanying decisions:

1. **`FieldDbType.Time` must exist; do not fall back to "a `String` column with a format agreed by convention".**
   The semantic marker is exactly why this type exists; what is stored underneath has nothing to do with the marker.
2. **An empty value is an empty string, and the column stays NOT NULL.** A time of day has no usable sentinel: `00:00`
   is a legitimate midnight. `GetDefaultValue(Time)` therefore returns an empty string rather than `"00:00"`.
3. **Range and format are checked by the value access layer; no DB CHECK is enforced.** A single
   `TimeOnly.TryParseExact` is enough; the CHECK syntax differs across the five databases, the maintenance cost is high,
   and it cannot stop direct SQL that bypasses the framework.
4. **Display format = storage format.** The UI does no locale-aware formatting; it is responsible only for the input
   mask and normalization on losing focus.
5. **The enum value is appended at the end.** `FieldDbType` travels on the MessagePack wire as its underlying integer,
   and inserting in the middle would shift existing values.

## Rationale

### Why not use the database's native time-of-day type

The original proposal was "native `time` in the DB, `TimeSpan` in `DataColumn`, `TimeOnly` in the value access layer",
and it was rejected after measurement. Measurement environment: `Microsoft.Data.SqlClient` 7.0.0, `Npgsql` 9.0.4,
`MySqlConnector` 2.4.0, `Oracle.ManagedDataAccess.Core` 23.26.200, `MessagePack` 3.1.7.

**1. `DataSet` rejects `TimeOnly`.** `DataColumn(typeof(TimeOnly))` can be created and assigned, and `WriteXml` can
write it out, but `ReadXml` throws `InvalidOperationException: Type 'System.TimeOnly' is not allowed here` (the
allowlist of types permitted by .NET's `DataSet`). The framework persists `DataSet` as XML, so this path is simply
closed.

**2. Providers always return `TimeSpan` on the read side.** On SQL Server / PostgreSQL / MySQL the parameter layer
accepts both `TimeOnly` and `TimeSpan`, but the column type read back into a `DataTable` is `TimeSpan` on all three.
The original proposal's `DataColumn` could therefore only be `TimeSpan`, and `TimeSpan` is unreadable both in a raw
SELECT and in XML (the ISO 8601 duration `PT8H30M15S`).

**3. Oracle has no `TIME`, and the framework cannot bind an interval.** Writing `INTERVAL DAY(0) TO SECOND(6)` through a
parameter throws `ORA-50028: Invalid parameter binding`: `DbCommandSpec` goes through the generic `DbType`, while
binding an Oracle interval needs an explicit `OracleDbType.IntervalDS`. It is fixable, but it is a cost unique to the
original proposal.

**4. The semantics of each database's native `TIME` is itself inconsistent.** SQL Server `time(7)` and PostgreSQL `time`
are times of day, but **MySQL `TIME` is a duration** (`-838:59:59` – `838:59:59`). The original proposal would have to
pin down the range in the abstraction layer and converge the behavior itself.

### What the fixed-width string buys

| Cost of the original proposal | String carrier |
|-----------|---------|
| Oracle binding special case | Gone; no special case on any of the five databases |
| Each of the MessagePack / JSON / XML pipelines needs an extra branch | Gone; `string` passes through everything |
| Tug-of-war between `TimeSpan` / `TimeOnly` as the carrier type | Gone |
| No empty-value sentinel, forced to allow NULL | Gone; an empty string means not filled in |
| Unreadable in a raw SELECT | Solved |

And **sorting and range queries work as usual**: the lexicographic order of zero-padded fixed-width `"HH:mm"` is the
chronological order, so `BETWEEN '08:00' AND '17:00'` works directly; digit strings sort the same under any collation.
Normalization (`"8:30"` → `"08:30"`) is the precondition of this guarantee, so it is performed uniformly in
`FieldDbTypeExtensions.ToFieldValue`.

**Precedent**: SAP's `TIMS` is `CHAR(6)` (`HHMMSS`) and `DATS` is `CHAR(8)`. Carrying dates and times of day as
fixed-width strings is a long-established practice in ERP.

### Why the value access layer returns `TimeOnly?` instead of following the shape of the `Cxxx` family

`CDateOnly(object, DateOnly defaultValue = default)` returns `0001-01-01` for an empty value, which is safe because it
is not a legitimate business value. But `default(TimeOnly)` = `00:00` **is** a perfectly legitimate time of day, and
copying the shape would silently turn unfilled columns into midnight. `CTimeOnly` therefore returns a nullable, letting
the type force callers to handle the unfilled case.

## Trade-offs

- **Reverse-engineering the schema hits a wall (the only new cost)**: the database reports the column as a string of
  length 5 and will never report it as `Time`. Without handling, `TableSchemaComparer` would judge a difference on every
  comparison and reissue ALTER endlessly. The solution is for `DbField.Compare` to **reduce both sides to the physical
  shape** (`Time` → `String(5)`) before comparing. "Storing the marker as a DB extended property" was not adopted:
  SQL Server has a ready-made mechanism, but MySQL / SQLite have no equivalent, and a mechanism not all five databases
  can support would become a provider special case.
- **Precision stops at minutes**: scenarios that need seconds use `DateTime` instead.
- **Time functions cannot be used on the DB side**: this is almost never needed for "declarative" time-of-day data
  (shifts, business hours); when arithmetic is really needed, the caller does it in C# after `CTimeOnly`.
- **Old client gap**: when a new server returns `Time` to an old client, the old client's `DbTypeConverter.ToType`
  goes to `default:` and throws. **Accepted and handled with a breaking marker**, for the same reason as
  [ADR-030](adr-030-messagepack-name-based-keys.md): client and server are released in the same version, and there are
  no external consumers. Writing a version negotiation mechanism for a single enum value would be disproportionate.

## Consequences

- `FieldDbType` gains `Time` (appended at the end); `DbTypeConverter` maps it to `typeof(string)` / `DbType.String`.
- `FieldDbTypeExtensions`: `GetDefaultValue` returns an empty string, and `ToFieldValue` normalizes to the fixed-width
  `"HH:mm"`.
- `ValueUtilities`: adds `CTimeOnly` / `CTimeString` and `TimeOnlyFormat` / `TimeOnlyLength`.
- `DbField.Compare`: reduces both sides to the physical shape before comparing (see "Trade-offs").
- The five providers: type mapping, default value expressions and literals, and the string family classification in
  `AlterCompatibilityRules`.
- `ExpressionPolicy.CoerceValue`: the boundary conversion `TimeOnly` → `string` (`TimeOnly` is not `IConvertible`, so
  otherwise `Convert.ChangeType` would throw).
- **The UI layer and the public documents are not implemented yet**; see the later phases.

**Regression guard**: `tests/Polhem.Db.UnitTests/TimeOfDayColumnIntegrationTests.cs` creates tables on all five
databases, round-trips values, and asserts that the schema comparison of time-of-day columns converges; as soon as the
physical shape reduction is lost, that assertion fails. Unit tests cannot catch this regression.

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **2026-09-27: the UI layer and the public documents exist.** "The UI layer and the public documents are not
  implemented yet" in "Consequences" describes the time of the decision. The Avalonia editor is
  `src/Polhem.UI.Avalonia/Controls/Editors/TimeEdit.cs`, and the Blazor Server form handles `ControlType.TimeEdit` in
  `src/Polhem.Web.Blazor.Server/Components/DynamicForm.razor` and `DynamicForm.razor.cs`; both normalize input with
  `ValueUtilities.CTimeString` and store an empty string for an emptied box. The consumer guidance is
  [Temporal Types](../../docs/en/temporal-types.md).

## Related

- [ADR-031: Calendar day column semantics](adr-031-calendar-day-column-semantics.md): the first time semantics, using
  "CLR type + an `ExtendedProperties` marker"; `Time` needs no means other than the marker to be distinguished,
  because it has its own CLR representation.
- [ADR-032: DateTime time zone handling](adr-032-datetime-timezone.md): time of day sits with calendar day under "never
  converted between time zones". Carrying it as a string makes this even safer: a string can never be mistaken for an
  instant and shifted.
- [ADR-030: MessagePack contracts switch to property-name keys](adr-030-messagepack-name-based-keys.md): the handling of
  the old client gap reuses its reasoning.
