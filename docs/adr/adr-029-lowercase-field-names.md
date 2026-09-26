# ADR-029: Field names are always lowercase (consistent across the definition, data and UI layers)

[繁體中文](adr-029-lowercase-field-names.zh-TW.md)

## Status

Accepted (2026-07-09)

> The principle takes effect immediately (field names are always lowercase `snake_case`). The column names of the
> in-memory `DataSet` have been **migrated from uppercase to lowercase** (`AddColumn` / `LowercaseColumnNames` are
> applied at the `DbAccess` read boundary). This is a breaking wire change, and the first-party client has been
> updated to match.

## Context

When the letter case of field names differs between the layers of the system, it causes problems again and again,
because **several subsystems compare field names as strings, case-sensitively**:

- **Data binding in the early UI controls** was case-sensitive. The framework's fix at the time was to normalize the
  column names of the in-memory `DataSet` to **uppercase** everywhere (`UppercaseColumnNames()` after reading from the
  database, and `ToUpper()` inside `DataTableExtensions.AddColumn`), so that binding was consistent.
- **The expression engine (ADR-028)** has case-sensitive identifiers (DynamicExpresso).
  `FormExpressionCalculator.BuildVariables` once used the uppercase `DataColumn.ColumnName` as the variable key, but
  expressions refer to the declared lowercase field names (such as `quantity`) → `UnknownIdentifierException`; when
  the server saved without handling it, it became a JSON-RPC `-32000`.

These two are **the same kind of problem**: a case-sensitive name comparison meets "the same field name has a
different case in different layers". The database naming conventions (see `docs/en/database-naming-conventions.md`
§1–2) already require all-lowercase `snake_case`, and `FormField.FieldName` is lowercase by convention as well. The
only inconsistency is the historical normalization "the in-memory `DataSet` stores column names in uppercase", and it
also leaks onto the wire through serialization.

## Options considered

1. **Decouple each case-sensitive consumer one by one** (the current patch-by-patch approach): for example, the
   expression engine now binds variables by `FormField.FieldName` (fixed in commit
   [`96821c04`](https://github.com/jeff377/bee-library/commit/96821c04)). **Partly adopted as the immediate
   stopgap**: it decouples the expression layer from the case the DataSet stores, and it is correct and carries zero
   risk; but it cannot cure "every future subsystem that does a case-sensitive comparison has to decouple itself
   again".

2. **Make everything case-insensitive**: leave the stored case alone and make every name comparison point
   case-insensitive instead. **Rejected as the long-term direction**: it does not achieve "field names that are
   literally identical across the three layers", and "where is it still case-sensitive" has to be watched
   continuously, so things easily slip through again.

3. **Normalize to lowercase in every layer (adopted)**: field names in the definition (`FormField.FieldName`), the
   data (the physical DB + the in-memory `DataSet`) and the UI are all lowercase `snake_case`, which is what the
   database already uses. A single canonical case removes the whole class of problems at the source. **Cost**:
   changing the in-memory `DataSet` column names from uppercase to lowercase changes the field names on the wire (the
   JSON / MessagePack payload keys) and breaks existing JS/TS front ends that read uppercase keys → a breaking change
   that has to be released in coordination at a major version boundary.

## Decision

**Field names are always lowercase `snake_case` in every layer; this is the single canonical spelling of the
system**:

| Layer | Carrier of the field name |
|-------|---------------------------|
| Definition | `FormField.FieldName`, `DbField.FieldName`, `TableSchema` fields |
| Data (physical) | Database table columns |
| Data (in memory) | `DataColumn.ColumnName` of a `DataSet` / `DataTable` (**target state; migration in progress**) |
| Expressions | Identifiers in `ValueExpression` / `FormRule.Condition` = the exact declared `FieldName` |
| UI | Binding keys of field editors / grid columns |

Accompanying principles:

- **Authoring**: field names are written in lowercase `snake_case` everywhere in schemas, layouts and expressions.
- **Code**: field name comparisons are always case-insensitive (the `DataColumnCollection` indexer already is);
  literal comparisons that depend on a particular case are **forbidden**.
- **Expression binding**: `BuildVariables` uses `FormField.FieldName` (the declared case) as the variable key,
  decoupled from the case `DataColumn` stores. Expressions stay correct even before the in-memory DataSet is migrated
  to lowercase (this is option 1's stopgap; in the long run it becomes consistent naturally once option 3 lands).

## Consequences

- **Positive**: field names have only one spelling across the whole system; the root of the whole class of
  "case-sensitive name comparison" bugs is removed; new subsystems do not need to decouple themselves; field names on
  the wire match the DB / schema, which is more intuitive for front-end consumers in the long run
  (`row.current.sys_rowid`).
- **Done (breaking)**: changing the in-memory `DataSet` column names from uppercase to lowercase is a **wire breaking
  change**. It affects the keys of both JSON and MessagePack payloads, first-party and third-party JS/TS front ends,
  and the existing DiffGram history of the change audit (whose field names are uppercase). How it landed:
  - The core switch (`AddColumn` / `LowercaseColumnNames` / `DbAccess`) + the first-party front end (`Web.Js.Demo`)
    have been updated; the wire converter emits `ColumnName` directly, so it is lowercase automatically.
  - Existing audit data is handled by "the parsing side accepts both the old and the new case" (downstream comparisons
    are case-insensitive anyway); the immutable audit history is not backfilled or rewritten.
  - **The preliminary audit is complete**: 0 literal uppercase comparisons on the C# side (all go through the
    case-insensitive `DataColumnCollection`); the Avalonia head binds case-insensitively; the other UI heads
    (WinForms / Blazor / MAUI) are not implemented yet, and migrating now makes them consistent from birth.
  - **Remaining**: a full regression across the multi-DB provider containers (SQLite verified); **at release time**,
    mark it as breaking in the CHANGELOG and attach a migration guide (per `releasing.md`, the CHANGELOG accumulates
    until the release is consolidated).

## Related

- ADR-028 (custom expression and rule engine): the source of the second time a case-sensitive comparison bit.
- `docs/en/database-naming-conventions.md` §1–2, §6: the lowercase field name convention and cross-layer
  consistency.
