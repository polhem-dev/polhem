# Pitfall log: databases and provider differences

The matching hard rules are in `.claude/rules/database.md`. This file records symptoms, root causes and the reasoning.

## Oracle: `''` == `NULL`, which breaks String NOT NULL columns whose "normal value is empty"

**Symptom**: `./test.sh` is all green locally, CI fails, and **only Oracle** throws `ORA-01400` (cannot insert NULL).

**Root cause**: Oracle has no "non-null empty string". The `VARCHAR2(n) DEFAULT '' NOT NULL` that the framework
generates for an Oracle String column with `AllowNull=false` **contradicts itself** (`DEFAULT ''` is `DEFAULT NULL`,
which conflicts with `NOT NULL`). Columns whose business value is always non-empty (`sys_id`, `sys_name`) are not
affected, because every INSERT supplies a non-empty value and never relies on the default; for **columns whose
normal value is empty** (such as the multi-tenant `customize_id`), under a **fresh CREATE TABLE** every INSERT that
omits the column or supplies an empty string fails.

**Why it does not reproduce locally**: the persistent local container goes through **ALTER ADD** for existing tables,
and ALTER ADD forces a column added to an existing table to be nullable, so the column has no NOT NULL constraint at
all. Only CI, with a fresh CREATE every time, reaches the real definition.

**Fix (planned)**: fix the Oracle dialect. String/VARCHAR2 columns are always created nullable regardless of
`AllowNull`, without `DEFAULT ''` (Oracle represents the empty string as NULL anyway); on the read side,
`ValueUtilities.CStr(null)→""` already means the upper layers only ever see an empty string. This is a set of changes
(DDL generation + the risk of repeated ALTERs in the schema diff + 10+ Oracle DDL tests).
**Until that fix lands**, String columns that are "normally empty and must support Oracle" use `AllowNull="true"` as
a stopgap.

## `DefaultValue` written as the type's built-in default → the schema comparison always reports "needs upgrade"

**Symptom**: after adding a column with `DbType="Boolean" DefaultValue="0"`, `TableSchemaBuilder` **always** returns
`DbUpgradeAction.Upgrade` for that table, and `GetCommandText` always emits SQL that drops and re-adds the default
constraint, even right after the upgrade has run and the column and default in the DB are exactly right. Three tests
that expect "structure in sync, so None / empty string / false" (`TableSchemaBuilderTests`) failed in a row because
of it.

**Root cause**: the read-back side **normalizes a default that equals the built-in default to an empty string**.
`SqlTableSchemaProvider.ParseDBDefaultValue` first strips the `((0))` stored by SQL Server down to `0`, then compares it
with `SqlSchemaSyntax.GetDefaultValueExpression(FieldDbType.Boolean)`, which is also `"0"`, and returns
`string.Empty` when they match. So the DB side reads `""` while the definition side has `"0"`, and
`IsEquals(oldField.DefaultValue, newField.DefaultValue)` in `SqlTableAlterCommandBuilder` never holds, so every
comparison asks for the constraint to be rebuilt.

**Why `st_api_key` is not affected**: its `enabled` / `key_type` both have `DefaultValue="1"`, which is not equal to the
built-in `"0"`, so the read-back side keeps `"1"` as it is and both sides agree. **It only hits when "the explicitly
written value happens to equal the built-in default"**: `0` for numeric types and Boolean, the empty string for String.

**Fix**: **do not write `DefaultValue="0"` explicitly**. The built-in default is already 0, and the DDL generation side
(`SqlSchemaSyntax`, the same shape in the other providers) outputs `DEFAULT (0)` when `DefaultValue` is empty. It
covers both CREATE and ALTER ADD, and existing rows are filled with 0 just the same. Omitting it produces exactly the
same DDL, without creating this permanent diff.

> The more fundamental fix is to apply the same normalization to both sides of the comparison, but that would change
> the diff behavior of all 5 providers. Until then, when adding a column, remember: "if the default equals the
> built-in value, do not write it".

> **There are two more entries with the same family of symptoms in this file**: Oracle's nullability projection (see
> above) and SQLite's description difference (see below).
> The common shape is **"the definition side can write it, but that dialect's side cannot store it or read it back"**,
> so the two sides never match.
> Such differences are not "not done yet"; they are **not comparable**, and the fix is always to keep the comparer from
> using them to judge differences.

## MySQL: `TEXT` cannot have a DEFAULT → an INSERT that omits the column fails outright

**Symptom**: only MySQL throws `Field 'x' doesn't have a default value` (strict mode).

**Root cause**: a NOT NULL column with `DbType="Text"` **cannot** have `DEFAULT ''` on MySQL (a syntax restriction on
TEXT/BLOB). The framework's `MySqlSchemaSyntax.GetDefaultExpression` **already handles this correctly**: a Text column
with `AllowNull=false` **never outputs a DEFAULT** (the column is still NOT NULL). The other dialects give a NOT NULL
string column an implicit empty-string default; MySQL TEXT has none.

The framework's own CRUD / seed INSERTs include every column, so they are not affected; **what gets hit is
hand-written native SQL** (test helpers, the `SharedDatabaseState` seed).

**Fix = complete the INSERT, not make the column nullable.** On 2026-07-01 `st_company.number_formats_xml` (Text) hit
this, and **it was once wrongly changed to `AllowNull="true"`, which the user rejected** (it violates the NOT NULL
design principle). The fix was to keep the column NOT NULL and add `number_formats_xml=''` to every hand-written
`st_company` INSERT (the seed + 4 test helpers).

To reproduce the CI behavior locally: run `ALTER ... MODIFY <col> LONGTEXT NOT NULL` manually with `docker exec`.

## MySQL: ALTER ADD of a Guid column on an existing table is judged replication-unsafe (fixed)

**Symptom**: MySQL error 1592/1674. In tests it makes the whole MySQL setup of `SharedDatabaseState` get caught and
skipped (`{dbType} setup skipped`), so the table's new column is never applied and later INSERTs report
`Unknown column`. **The visible symptom is two layers away from the root cause.**

**Root cause**: the framework generates `char(36) NOT NULL DEFAULT (UUID())` for a MySQL Guid column. Running
`ALTER TABLE ... ADD COLUMN <guid> NOT NULL DEFAULT (UUID())` on an **existing table** is considered
replication-unsafe under statement-based binlog (the system function gives a different value per row). **A fresh
CREATE TABLE with `DEFAULT (UUID())` is safe** → CI (a brand-new container every time) is unaffected; only the
persistent local container hits it.

**Fixed (commit [`eeea3aad`](https://github.com/jeff377/bee-library/commit/eeea3aad))**: `MySqlTableAlterCommandBuilder` splits the ADD of "a NOT NULL column whose default is a
non-deterministic function" into two steps: ① first `ADD COLUMN ... NOT NULL DEFAULT '00000000-...'` with a constant
empty Guid default (safe; existing rows get `Guid.Empty`), ② then `ALTER COLUMN ... SET DEFAULT (UUID())`
(metadata-only, does not touch existing rows, and matches the fresh CREATE schema → the comparer does not drift).
Detection condition = the parsed default contains `UUID()`.

**Remaining**: nothing at the framework level. But the **pattern** "cross-dialect default value / nullability
differences in ALTER ADD, where local and CI take different paths" will recur; see the Oracle entry above.

## MySQL: `MODIFY COLUMN` replaces the whole column definition, so leaving out `AUTO_INCREMENT` strips the auto-increment (fixed)

**Symptom**: MySQL `Field 'sys_no' doesn't have a default value`, on completely unrelated INSERT tests
(`EmployeeBuildSelectIntegrationTests`, 6 at once). **And a second run is all green**, which looks very much like a
flaky test, but it is not.

**Root cause**: MySQL's `ALTER TABLE ... MODIFY COLUMN` is a **whole replacement**; anything not written in the
fragment disappears. `MySqlSchemaSyntax.GetColumnDefinition` produces `type + nullability + default + comment`,
**without `AUTO_INCREMENT`** (that lives in `GetAutoIncrementColumnDefinition`). Using it for a MODIFY on an identity
column turns the column into a plain `BIGINT NOT NULL` with no default, and every INSERT after that fails.

**Why the second run fixes itself**: in the next comparison, the `sys_no` read back is no longer AutoIncrement, and
`AlterCompatibilityRules` judges a change of type family involving AutoIncrement as **Rebuild**, so the whole table is
rebuilt with `CREATE TABLE` → the auto-increment and the comment are both back. **Here, "passes on a rerun" means the
damage was covered up, not that the test is flaky.**

**Trigger path**: when description sync landed for MySQL, it issued MODIFY COLUMN to add a `COMMENT` to columns whose
caption had drifted with no structural change. If that column happened to be `sys_no`, it hit. The ALTER path itself
cannot reach it: any AutoIncrement change is always routed to Rebuild.

**Fixed**: added `MySqlSchemaSyntax.GetModifyColumnDefinition`, which outputs
`BIGINT NOT NULL AUTO_INCREMENT COMMENT '...'` for an AutoIncrement column (without `PRIMARY KEY`, which the table
already has); both `MySqlDescriptionSyncCommandBuilder` and `MySqlTableAlterCommandBuilder` now go through it.

**General rule**: **only MySQL stores the description inside the column definition**, so only its description sync
has to issue a destructive statement like `MODIFY COLUMN`; `COMMENT ON` in Oracle / PostgreSQL and extended properties
in SQL Server are pure metadata and cannot touch the column definition. Before adding any MODIFY to MySQL that "only
wants to change an auxiliary attribute", check that the fragment carries clauses such as `AUTO_INCREMENT`,
`GENERATED` and `ON UPDATE` along with it.

## SQLite: a GUID is case-sensitive TEXT (fixed, with remaining caveats)

**Symptom**: open an existing order and add a detail line; the detail line **is** INSERTed into the DB, but it
"disappears" after a reload. It is actually an orphan row.

**Root cause**: SQLite has no GUID type; it stores GUIDs as **TEXT** and compares them case-sensitively. This project
has several sources of casing: the seed / existing data are **uppercase**, `Guid.ToString()` is **lowercase**, and
Microsoft.Data.Sqlite binds a Guid parameter as **uppercase** TEXT.
The client writes the master's Guid via `ToString()` (lowercase) into the `sys_master_rowid` string column → it does
not match the uppercase master → the reload's `WHERE sys_master_rowid = '<UPPERCASE>'` finds nothing. A new order is not
affected (master and detail both go through Guid parameters, consistently uppercase); only "open an existing order and
add a detail line" hits it.

**Fixed at the root (2026-06-15)**: SQLite GUID (`UUID`) columns get `COLLATE NOCASE` in CREATE/ALTER
(`SqliteSchemaSyntax.UsesNoCaseCollation` puts `FieldDbType.Guid` alongside String/Text). CREATE and ALTER ADD share
`GetColumnDefinition`, so one change covers both paths. GUID hex is all ASCII → NOCASE covers it completely.

**Remaining (all three still apply)**:

1. When setting a GUID foreign key link, still **copy the source's original value**; do not round-trip it through
   `Guid.Parse/ToString` (the masterRowId in `FormRowDefaults.Apply` is written as is as an `object?`). This is
   orthogonal and complementary to COLLATE.
2. COLLATE only makes **comparison** case-insensitive; it **does not normalize the stored value**. An existing SQLite
   table has to have its schema rebuilt to pick up the new collation.
3. **"A GUID column read back on the client is of type String" spills over**: the expression engine's coerce pitfall
   is caused by it; see [serialization-and-expressions.md](serialization-and-expressions.md).

## SQLite: no COMMENT mechanism → the description difference can never be cleared (fixed)

**Symptom**: no error, no SQL, no sign at all. It only shows when you ask "has this table finished upgrading": as long
as the TableSchema has any caption, `CompareToDiff` always reports a difference, `Plan` always returns **an `Alter` with
zero stages**, and `UpgradeExecutionMode.NoChange` never appears.

**Root cause**: SQLite has no `COMMENT ON` and no column comment field, so `SqliteTableSchemaProvider` reads every
`Caption` back as an **empty string** (`Caption = string.Empty`, hard-coded).
The comparer's conservative policy is "the define side has a value and the real side does not → count one
`DescriptionChange`", and `TableSchemaDiff.IsEmpty` counts `DescriptionChanges`, so the difference always exists and no
statement can remove it.

**Why "add a SQLite description sync builder" is not enough**: that builder cannot produce anything.
This is not "not wired up yet"; **the dialect has nowhere to write it at all**. Give it a builder that returns an
empty list, and the difference still stays in the diff and `IsEmpty` is still false.

**Fixed**: `TableSchemaComparer.PopulateDescriptionChanges` returns immediately for `DatabaseType.SQLite`: what cannot
be persisted should not be listed as a difference. This is the same idea as Oracle's `NormalizeNullability`:
**for an attribute the dialect cannot control, the two sides are not comparable, so do not use it to judge
differences**.

**To decide**: when adding any metadata that "the definition has but some dialect cannot store in the database",
first ask: **is there any statement that can remove this difference?** If not, it must not enter the diff, otherwise
it silently pins the table in the "not in sync" state forever: no error, just a permanent no.

## Decimal precision: the framework does not set the parameter scale, and DB behavior is inconsistent

**Root cause**: `DbCommandSpec.CreateCommand` only sets `Value`/`DbType`/`Size`/`IsNullable`; `DbParameterSpec` **has
no `Precision`/`Scale` properties** (the ADO.NET provider infers the scale from the value itself); `DbField.Scale` is
only used for CREATE TABLE DDL. Across the whole repository (tests excluded), there is no `Math.Round` /
`decimal.Round` / `Truncate` before writing to the DB.

**Consequence**: SQL Server / PostgreSQL / MySQL / Oracle **round (not truncate)** values beyond the column scale;
**SQLite does not enforce scale at all → it keeps full precision as is** (NUMERIC affinity does not convert). The same
decimal may be stored with **different precision** on SQLite vs SQL Server.

**Fix**: rounding must be done explicitly by the **Repository write layer** (CRUD is driven by FormSchema/DbField,
which holds `DbField.Scale` for every column); the `DbCommandSpec` layer has no column scale, so it cannot be hooked
there.

## datetime2: changing the schema is not enough; the bottleneck is the parameter inference layer

**Symptom**: the DDL for SQL Server `FieldDbType.DateTime` has already been changed to `datetime2(7)`, but sub-millisecond
precision is still not available, and pre-1753 values still throw `SqlDateTimeOverflow`.

**Root cause**: `DbParameterSpec` is the only parameter write path for every provider. `DbTypeMapper.Infer` maps
`DateTime → DbType.DateTime`, and SqlClient rounds the value to ms and throws for pre-1753 values **before sending**,
**even if the column is datetime2**.

**The pitfall hit during the fix (important)**: the first idea was to change `DbTypeMapper.Infer` globally to
`DbType.DateTime2`, and it **broke the Northwind seed on PostgreSQL / Oracle**: under DateTime2, Npgsql resolves the
type of `Kind=Utc` values differently, the seed transaction rolled back → 0 rows. Locally it **did not reproduce**
because the shared DB had old seed data; it only broke in CI's fresh containers.

**Fix**: `DbTypeMapper.Infer` stays `DbType.DateTime` (unchanged across providers); instead, the provider-aware
`DbCommandSpec.NormalizeDbType` maps `DateTime → DateTime2` **only for SQL Server** (the same mechanism as the existing
Oracle `Guid → Binary`). Existing `datetime` columns are automatically ALTERed to datetime2 at the next schema
upgrade (the comparer tells them apart by `sys.columns.scale`, 3 vs 7).

**General rule**: every "cross-provider type adjustment at the parameter layer" goes through a provider-gated rewrite
in `NormalizeDbType`; do not touch the global `Infer`. Driver behavior for DateTime parameters differs enormously
between providers.

## Oracle: parameters bind by position, so placeholders in the wrong order bind to other columns (fixed)

**Symptom**: on Oracle, `ORA-00932: 表示式 (:1) 為 TIMESTAMP 資料類型, 與預期的資料類型 BINARY 不相容`.
The same SQL works fine on the other four. In the load test the visible effect was "`GetList` fails 100%", but
`GetList` was not what was wrong: that SELECT has no bind variables at all. The login path failed first, and the VU
pool cached the faulted task.

**Root cause**: `OracleCommand.BindByName` in `Oracle.ManagedDataAccess` defaults to `false`: the n-th bind variable in
the SQL gets the n-th entry of the parameter collection, **regardless of name**.
The other four always bind by name, and the semantics of the `{0}` / `{Name}` placeholder API is matching by name.
The placeholder order in `SessionRepository.UpdateSession` was `{1} {2} {0}`, so a `DateTime` was sent into
`access_token` (`RAW(16)`).

**Why it is worse when the types are compatible**: a mix-up between two string columns **produces no error at all**;
it just writes the wrong columns. `ORA-00932` only blew up by luck.

**Fix**: `DbCommandSpec.CreateCommand` sets `BindByName = true` on Oracle text commands (set via reflection;
`Polhem.Db` references no ADO.NET driver). **Do not rewrite SQL statement by statement to accommodate positional
binding**: that requires everyone who writes SQL to remember an Oracle-only rule that no mechanism checks.
The gate is `tests/Polhem.Db.UnitTests/ParameterBindingOrderTests.cs` (two per provider, for all five).

## Oracle: `RAW(16)` reads back as `byte[]`, so `is Guid` is always false (fixed, with remaining caveats)

**Symptom**: `Cannot coerce value of type 'System.Byte[]' into Guid` (`GetData` / `Save`); or a quieter version: the
row is found and the value is there, but every `is Guid` branch takes the else, so "existing data looks as if it does
not exist" (because of this, the load test tool's `ResolveRowId` made a second `prepare` insert again and hit the
unique key).

**Root cause**: Oracle has no UUID type; `FieldDbType.Guid` maps to `RAW(16)`. **The write side was handled long
ago** (`DbCommandSpec.NormalizeParameterValue` converts to `byte[]`), but each reader did its own thing.

**Fix**: conversions always go through `ValueUtilities.CGuid(object)`, which **already accepts 16-byte arrays**.
`DataFormRepository` missed it because it carried its own parallel implementation (`TryCoerceToGuid`).
For FormSchema-driven result tables, `MarkFromSchema` additionally replaces, in place, a column declared as Guid but
holding `byte[]` with a real Guid column; otherwise consumers get a DataTable that is "declared Guid, actually
byte[]".

**Remaining**: `FormDataGuard` and the grids of each UI head (`GridControl.Cells`, `DynamicGrid`, `ListView`) still
use a bare `is Guid`. Data that went through `MarkFromSchema` is fine; other sources have not been checked.

## SQLite: date columns read back as `string`, so `is DateTime` is always false (fixed, with remaining caveats)

**Symptom**: on SQLite, `ApiKeyRepository.GetEnabledById` read a key that has an expiry time as
`ExpiredAt = null`: no exception, no warning, the **expiry time just vanished**, so `ApiKeyInfo.IsExpired` always
returned false and expired keys were let through.

**Root cause**: SQLite has no date type; the column is stored as TEXT, and `Microsoft.Data.Sqlite` returns it as a
`string` for ad hoc queries that have no schema to follow. `expiredAt is DateTime dt ? dt : null` therefore takes the
else. It is the same shape of error as the Oracle `RAW(16)` in the previous entry: **the CLR type the driver returns is
not the declared type, and a bare `is T` collapses "wrong type" and "no value" into the same answer**.

**Fix**: conversions go through `ValueUtilities.CDateTime(object?)` (returns `DateTime?`, accepts `DBNull`, the empty
string and parseable strings).

The FormSchema-driven path (since 2026-09-12) is handled by `DataFormRepository.MarkFromSchema`: a column declared as
Date / DateTime but holding `string` is replaced in place with a real `DateTime` column (in the same place as the
Oracle Guid column). Text that cannot be parsed throws `InvalidOperationException`; an empty string is read as
`DBNull`.

> **This section used to say "the FormSchema-driven path is unaffected, because `MarkFromSchema` has already normalized
> the columns by their declared types". That did not hold**: at the time, `MarkFromSchema` only added markers and only
> converted Guid columns. The consequence was that on SQLite, the time columns of a form read back through an
> **in-process (Local) call** got no UTC → user time zone conversion at all, because `DateTimeZoneConverter` only
> picks columns with `DataType == DateTime`. **Remote calls do not show it**: the wire rebuilds columns by their
> declared types, so the client already gets a `DateTime` column. What pins it down is `DateTimeZoneFormReadTests`
> (in-process and the two wires side by side).
> That guarantee could not name any mechanism that enforced it: one more example of `code-style.md`'s "A claim in
> absolute terms must name the mechanism that enforces it".

**Why it took until now to find**: `ApiKeyRepository` goes through `DbScope.Common`, and the test fixture binds
`common` to SQL Server, so those `[DbFact(DatabaseType.SQLite)]` tests were actually running on SQL Server.
The inventory and the fix are in `ProviderScopedRouter` (`tests/Polhem.Tests.Shared/`).

**Remaining**: other places that build their own SQL to read date columns have not been checked one by one. When you
see a bare `is DateTime`, ask: "what type is this value on SQLite?"

## Oracle: the load test tool and the unit tests share the same schema and break each other

**Symptom**: after running `dotnet run --project tools/Polhem.LoadTests -- prepare --provider Oracle`, the whole unit
test fixture fails with
`InvalidOperationException: Change narrows a column (AlterFieldChange)`
(`InvalidOperationException` is not a `DbException`, `RunStep` does not catch it, and the whole Oracle setup aborts).

**Root cause**: `databaseNamePrefix` has no effect on Oracle: all five tables live under the single `testuser` schema
(see the comment in `.runsettings`). The load test uses the `st_user` from `apps/Polhem.Northwind/Define` (`password`
length 200), the unit tests use the one from `tests/Define` (length 40), and they overwrite each other.

**Fix (since 2026-09-08)**: use a **dedicated schema**, and stop letting the two share one. The tool now blocks
connection strings that cannot be isolated by database name; the remedy is to set `POLHEM_LOADTEST_CONNSTR_ORACLE` to
a user reserved for load tests. The setup steps are in [`docs/repo-ops/load-testing.md`](../load-testing.md).

**Use the recovery SQL below only when it has "already been hit"** (that is, on a machine that ran load tests before
the dedicated schema existed). It deletes the `loadtest_user_%` and `loadtest` company rows one by one, and changes
`st_user.password` back to 40:

```sql
delete from st_user_company where company_rowid in (select sys_rowid from st_company where sys_id='loadtest');
delete from st_company where sys_id='loadtest';
delete from st_user where sys_id like 'loadtest_user_%';
commit;
alter table st_user modify (password varchar2(40 char));
```

The `loadtest_user_%` rows must be deleted first: their password hashes are 79 characters, and without deleting them
you get `ORA-01441`. To load test again after that, just rerun `prepare` (pointed at the dedicated schema).

> **Running this SQL leaves traces, and they do not look like something a person did.** It deletes only the accounts,
> not `ft_customer`, so what you see afterwards is "the hundred thousand rows of load test data are still there, but
> the accounts that seeded them are gone". That is easily misread as the test suite having deleted the accounts. In
> fact nothing in `tests/` has a `DELETE` / `TRUNCATE` / `DROP` that could do this. To tell: **look at the length of
> `st_user.password`**; 40 means this was run (the load test definition has 200).

## Deep pagination: the cost of `OFFSET` grows with the page number, and none of the four escapes it

**Symptom**: same table, same `pageSize`, the later pages get noticeably slower, while the first page does not move no
matter how much data is added.

**Root cause**: page-number pagination relies on `OFFSET`, and the engine has to walk past and discard every row before
the offset. This is inherent to offset pagination, **not a problem in the framework's dialect implementation**:
switching to another provider does not solve it.

**Measured on a macOS development machine on 2026-09-08** (`ft_customer` 100,000 rows, `pageSize` 50, shallow pages
from page 1, deep pages from page 1,900, i.e. `OFFSET 94,950`, 10 pages each, 20 VUs, Local + Encrypted, closed model,
warm-up 30s + measurement 120s; two rounds for the first three, three rounds for Oracle, all with zero errors):

| Provider | Shallow p50 | Deep p50 | Ratio |
|---|---:|---:|---:|
| SQL Server | 1.2 | 18.5 | 15.4× (other round 14.5×) |
| PostgreSQL | 0.68 | 11.16 | 16.4× (15.6×) |
| MySQL | 1.17 | 22.39 | 19.1× (20.1×) |
| Oracle | 0.7 | 19.3 ~ 24.6 | 27.6× / 33.3× / 35.1× |

Units are milliseconds. These are **what was measured on that machine at the time, not a performance specification of
the framework**: client and server on the same machine, a closed model (the send rate drops as the system slows down,
so the saturation point does not show), and 100,000 rows is still a small table for all four.
**The absolute values cannot be compared across providers** (each container is configured differently); the ratios
are what matters.

The Oracle row has two further caveats: the ratio increased round after round instead of fluctuating (for the first
three, the two rounds differ by less than 1), and **both scenarios** carry a p99 of about 0.8 to 1.9s and a max of 2.1
to 2.4s. **The shallow pages have it too**, and a first page that scans only 50 rows cannot be slow because of the
offset, so that tail is a periodic stall of that container and unrelated to pagination; **the cause has not been
found**. Its throughput is also only about a fifth of the first three.

**Decision**: **do nothing** (2026-09-08). Keyset pagination makes the cost independent of the offset, at the price
that users cannot jump straight to page 500. That is a product decision, not a technical one, so the maintainer made
the call. The XML doc of `PagingOptions.Page` already states that the cost grows with the page number.

**The judgement the decision was based on (maintainer, 2026-09-08)**: *"In real use, users should rarely need deep
pagination; they usually set query conditions and then look at the shallow pages."* This is **a judgement based on
our own usage patterns at the time, not a general rule**, and no mechanism keeps it true. To overturn the decision,
start by asking about this sentence: does it still hold?

Worth mentioning: **SAP wrote the same judgement into a hard UI guideline** (≤200 rows, list reports use growing
instead of page numbers, the main interaction is the filter bar), which counts as independent evidence; see
[../pagination-prior-art.md](../pagination-prior-art.md).

### What this judgement does not cover: machine callers

**The real consumers of deep offsets are not people but machines.** Users filter, but **integration / export callers
do not**: they walk from page 1 to the last page and pull all the data. There is no filter condition on that path, and
`OFFSET` grows all the way to the end.

This also explains why the motivation for keyset at CAP and Microsoft is **consistency**, not speed: the clients that
walk through every page are exactly this kind, and they run long enough that the chance of the data changing midway is
not negligible (duplicate rows / missing rows). A person paging through three pages in the UI never meets this
problem.

**Current state**: integration / export uses the same page-number pagination, with **no dedicated mechanism**. This is
not a to-do item; it is a statement of scope. When a real need for large exports appears, that path is what should be
addressed, not UI pagination.

### How other ERPs handle it

Odoo, SAP RAP, SAP CAP and Microsoft ASP.NET OData were checked: **all four default to offset**, and keyset exists as
an opt-in in only two of them, motivated by consistency rather than performance. The evidence item by item, the two
axes that are often mixed up (who computes the page boundaries vs how the server continues), the difference between
RAP and CAP, and the limits of what was verified are all in
[../pagination-prior-art.md](../pagination-prior-art.md). **This file does not copy them.**

## Miscellaneous cross-DB seed notes

- Identifiers always go through `dbType.QuoteIdentifier(...)`. **Oracle uppercases them** (`"FT_CATEGORY"`); the
  others keep them as they are.
- Seed JSON values are all strings (including numeric PKs such as order `"10248"`); **convert them by the target
  column's `FieldDbType`**, never guess the type from the value.
- Bind dates with `DateTimeKind.Utc`: PG's Date maps to `timestamptz`, and Npgsql rejects Unspecified/Local; Utc is
  safe on all 5 DBs.
- If a persistent DB has old seed data left over, the gate skips → the related tables in that DB must be emptied by
  hand (including the gate table itself, to reopen the gate).
