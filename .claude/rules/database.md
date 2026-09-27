# Database rules

> Per-provider pitfall details and the reasoning behind them are in `docs/repo-ops/gotchas/database.md`
> (read on demand, not always loaded).

## Two orthogonal dimensions: table prefix vs CategoryId

| Axis | Values | Meaning |
|----|----|------|
| **Table prefix** | `st_` / `ft_` | Framework mechanism vs business data (**who uses it**) |
| **CategoryId** | `common` / `company` / `log` | Which DB scope the data lives in (**where**) |

**The prefix is not tied to a DB location.** The decisive example: `st_department` / `st_employee` are owned by the
framework (needed by the record-scope and organization tree features) yet live in the **company database**; the
permission tables `st_role` / `st_role_grant` / `st_user_role` are the same, and `user_rowid` logically points across
databases to `st_user.sys_rowid` in common. "st_ in common, ft_ in company" is only a common combination, not a rule.
The authoritative list is `docs/en/framework-reserved-names.md`.

`FormSchema.CategoryId` (and `DbCategory.Id`, `DatabaseItem.CategoryId`) **is not a free-form string**:
`RepositoryFactory.ParseCategoryId` (`src/Polhem.Repository/Factories/RepositoryFactory.cs`) accepts only the three
`DbCategoryIds` values and throws `Unknown schema.CategoryId` for anything else. Analyzers `POLHEM1001` / `POLHEM1002`
report an invalid value in a definition file at build time.

- **`company`** = data that is separate per company. **Business tables (`ft_*`) and the application organization
  tables (`st_department`/`st_employee`) must all be company.** The router goes
  `session.CompanyId → ICompanyInfoService.Get → CompanyInfo.CompanyDatabaseId`.
- **`common`** = framework tables shared across companies (`st_session`, `st_cache_notify`). The router resolves the
  common scope to the literal database id `common` (`RepositoryDatabaseRouter`), so `DatabaseSettings` needs a
  `DatabaseItem` whose `Id` is `common`. `IDatabaseSettingsProvider.ValidateRequired` checks that, but nothing in the
  framework calls it at startup today: a missing item surfaces only on first use, as an `InvalidOperationException`
  from the connection manager.
  **Putting a business table in common is wrong.**
- The `TableSchema/{categoryId}/` folder name = CategoryId (used by the seeder; the form runtime's DML reads only
  FormSchema).

## Column nullability: text and numeric columns are always NOT NULL

Without a specified default, the value is an empty string or `0`; **do not use nullable**. Reason: once the DB holds
NULLs, every future hand-written SQL has to guard against null everywhere (`WHERE col=''` does not match NULL rows).
The framework already has this built in for SQL Server / MySQL / PostgreSQL / SQLite (each dialect's
`<Dialect>SchemaSyntax.GetDefaultValueExpression`); Oracle is the exception in item 4 below. **When adding a column, mark it `AllowNull=false` by default; do not reflexively add
`AllowNull="true"`.**

**Add-column checklist**:

1. Mark it `AllowNull=false`.
2. Confirm that **every** INSERT (including the `SharedDatabaseState` seed and test helpers) supplies a value.
3. For a `DbType="Text"` column: MySQL TEXT/BLOB **cannot have a DEFAULT**, so the framework emits no DEFAULT
   → every hand-written INSERT must supply the value explicitly (`''`). **Do not make it nullable because of MySQL.**
4. Oracle needs nothing in the definition: `''` == `NULL` there, so `OracleSchemaSyntax.GetNullabilityClause` emits
   every `String` / `Text` / `Time` column as nullable and without `DEFAULT ''`, whatever `AllowNull` says, and
   `ValueUtilities.CStr` turns the `NULL` back into `""` on read. **Keep `AllowNull=false`; do not mark a column
   nullable because of Oracle.** Hand-written SQL that must run on Oracle cannot match these columns with
   `col = ''` (see `docs/en/database-dialect-differences.md` §3.1).
5. **Do not rely on local results alone**: the persistent local container reaches the new column through the upgrade
   path (`ALTER TABLE ... ADD`, which emits the same column definition, `NOT NULL` and `DEFAULT` included, and
   backfills existing rows), while CI creates every table fresh. An INSERT that omits the column can behave
   differently on the two paths (MySQL `TEXT` has no default, Oracle strings are nullable), so only CI's fresh
   `CREATE` proves the seed and the test helpers.

## Numeric precision: round-then-sum, and the framework must round explicitly

**ERP iron rule: the sum of the detail lines = the total, with no discrepancy.** Each detail line is rounded to the
column's scale **first** and **then** summed; summing at full precision and rounding the total afterwards is
**forbidden**.

- **Not rounded (unit price / cost / exchange rate)**: stored as-is at input precision; the framework applies no
  rounding (rounding a source value injects error downstream). The scale is for display only.
- **Rounded (quantity / weight / amount / percentage)**: rounded `AwayFromZero` to the column's scale on write;
  summable ones use round-then-sum. Compute an amount by multiplying with the unit price's full precision, and round
  once the amount is computed. The scale is customized at the **company level**.

**The framework does not set Precision/Scale on parameters and does not round before writing.**
`DbCommandSpec.CreateCommand` sets only `Value`/`DbType`/`Size`/`IsNullable`; `DbField.Scale` is used only for DDL.
Implicit DB conversion is inconsistent (the 4 major DBs round; **SQLite enforces nothing and keeps full precision
as-is**).
→ Any "round to a fixed scale before writing" semantics **must be done explicitly in the Repository write layer with
`decimal.Round(value, dbField.Scale, MidpointRounding.AwayFromZero)`**. Do not rely on the DB.

## Cross-provider type adjustments go through `NormalizeDbType`

The parameter layer is where cross-provider precision / type is actually decided, and drivers differ widely (Npgsql
has its own rules for `DateTimeKind`, Oracle for types). **Every provider-specific rewrite of a parameter type goes
in `DbCommandSpec.NormalizeDbType` (provider-gated). Do not touch the global `DbTypeMapper.Infer`.**

Existing examples: SQL Server-only `DateTime → DateTime2` (to get datetime2(7)'s sub-millisecond precision and the
pre-1753 range), and Oracle `Guid → Binary`. Changing `Infer` globally was tried once, and it broke the
PostgreSQL/Oracle seed.
