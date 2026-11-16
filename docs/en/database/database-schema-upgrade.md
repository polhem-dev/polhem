# Database Schema Upgrade Guide

[繁體中文](../../zh-TW/database/database-schema-upgrade.md) · [← Docs Index](../README.md)

> This guide explains how a Polhem application maintains database table schemas: how definition changes are synchronized to the live database, the upgrade strategy used under the hood, and operational considerations.
> For naming rules see [Database Naming Conventions](database-naming-conventions.md); for the underlying definition-driven philosophy see [ADR-005 FormSchema-Driven](../../../maintainers/adr/adr-005-formschema-driven.md).

## 1. Core Concepts

Polhem adopts a **define-driven schema** model: the table structure is sourced from FormSchema / TableSchema XML definitions as the single source of truth. When the host calls the upgrade API (at startup or during operations; the framework does not run it on its own), the framework compares the definitions against the live database and generates and runs the required upgrade statements.

As a developer you only need to:

1. Modify the definition file (add a column, widen a length, add an index)
2. Call the upgrade API
3. Let the framework decide whether to use ALTER or rebuild

### Success Contract

> **After a successful upgrade returns, the database contains at least every column listed in the definition, and each column's type / length / nullability matches the definition** — except where a dialect cannot express the definition, such as Oracle's text columns, which are always nullable ([Database Dialect Differences §3.1](database-dialect-differences.md#31-oracle--is-null)).

This is the only thing that matters from the application's perspective. Whether the framework chooses `ALTER TABLE` or a full table rebuild internally is a library implementation detail; callers do not need to care.

### Upgrade principles

Every upgrade path follows four principles:

1. **Keep the existing data.** An upgrade changes the table, not its rows. A rebuild copies every column, including the ones the definition does not declare, and keeps AutoIncrement numbers; a rebuild that would lose an index only the database has is refused instead (§4).
2. **The current `TableSchema` is the target.** When the schema read back from the database differs from the definition, the upgrade changes the table to match the definition.
3. **One upgrade is enough.** After an upgrade, comparing the same definition with the database again finds no difference, so the next run plans nothing. A difference that survived its own upgrade would be applied again on every run.
4. **When the existing data blocks the upgrade, it stops and reports.** The table and its rows stay as they were, the error names the table and the cause, and you decide how to fix the data or the definition (§7). The upgrade never changes or discards data to get through. The one exception is a column narrowing you turn on yourself with `AllowColumnNarrowing` (§5).

> **Upgrading an existing deployment to company tables with `sys_company_id`: use `db upgrade`.** The APIs below add
> the column with an empty value and stop there. A row whose `sys_company_id` is empty belongs to no company, so every
> existing row would disappear from every company. `db upgrade` first checks that the company table schemas carry the
> column and that the company ids are in their stored form, and after upgrading gives the rows with no company the
> company of their database when only one company uses it; in a database several companies share, it reports them for
> you to assign (`db add-company-column` adds the column to schemas that lack it). See
> [Database Settings §5.5 and §7](database-settings-guide.md#55-companies-sharing-a-company-database).

## 2. Entry-Point APIs

### 2.1 General Use: `IDatabaseRepository.UpgradeTableSchema`

The simplest call, suitable for most scenarios:

```csharp
// Resolve via DI: inject IRepositoryFactory in the consuming class ctor,
// then create the repository on demand.
public class MyService(IRepositoryFactory repoFactory)
{
    public bool UpgradeEmployeeTable()
    {
        var repo = repoFactory.Create<IDatabaseRepository>();
        // Parameter order: databaseId (physical connection id), categoryId (logical category), tableName
        return repo.UpgradeTableSchema("myDb", "company", "st_employee");
    }
}
```

The return value indicates whether an upgrade was actually performed: `false` means the database already matches the definition and nothing needed to change.

> The interface is defined in [IDatabaseRepository](../../../src/Polhem.Repository.Abstractions/System/IDatabaseRepository.cs); the default implementation is [DatabaseRepository](../../../src/Polhem.Repository/System/DatabaseRepository.cs).

### 2.2 Advanced Use: `TableSchemaBuilder`

When you need finer control (dry-run, `UpgradeOptions`, structured diff), drop down to [TableSchemaBuilder](../../../src/Polhem.Db/Schema/TableSchemaBuilder.cs):

```csharp
// Resolve defineAccess and connectionManager via DI (e.g. inject in BO / Service ctor)
var builder = new TableSchemaBuilder("myDb", defineAccess, connectionManager);

// Get the structured diff (no execution); the first argument of subsequent methods is the categoryId
TableSchemaDiff diff = builder.CompareToDiff("company", "st_employee");

// Get the SQL that would be executed (no execution)
string sql = builder.GetCommandText("company", "st_employee");

// Run the upgrade (UpgradeOptions is optional)
bool upgraded = builder.Execute("company", "st_employee", new UpgradeOptions
{
    AllowColumnNarrowing = true,
});
```

When to drop down to this layer:

- You want to inspect the SQL before deployment (dry-run)
- You need to enable special options such as "allow column narrowing"
- A maintenance tool needs to enumerate the columns / indexes that will change

## 3. Upgrade Pipeline: Diff → Plan → Execute

Internally the upgrade is split into three stages, each callable in isolation:

```
┌─────────────────────────────┐
│ 1. CompareToDiff            │  Compare definition vs live DB
│    → TableSchemaDiff        │  Pure structure, no SQL
└─────────────────────────────┘
              ↓
┌─────────────────────────────┐
│ 2. Orchestrator.Plan(diff)  │  Decide ALTER vs rebuild
│    → UpgradePlan            │  Staged SQL plus warnings
└─────────────────────────────┘
              ↓
┌─────────────────────────────┐
│ 3. Orchestrator.Execute     │  Run the plan against the DB
└─────────────────────────────┘
```

### TableSchemaDiff (Structured Changes)

[TableSchemaDiff](../../../src/Polhem.Db/Schema/TableSchemaDiff.cs) is a provider-agnostic intermediate result listing each `ITableChange`:

| Change Type | Meaning |
|-------------|---------|
| `AddFieldChange` | Add a new column |
| `AlterFieldChange` | Modify an existing column (type / length / nullable / default) |
| `RenameFieldChange` | Rename a column (requires `DbField.OriginalFieldName`) |
| `AddIndexChange` | Create an index |
| `DropIndexChange` | Drop an index |

It also carries `DescriptionChanges` (table / column description drift). Description synchronization runs on SQL Server, PostgreSQL, MySQL and Oracle; SQLite has no facility to store descriptions, so the comparer does not report description drift there at all (see Stage 5 in §7).

### UpgradePlan (Execution Plan)

[UpgradePlan](../../../src/Polhem.Db/Schema/UpgradePlan.cs) holds the `Mode` (`NoChange` / `Create` / `Alter` / `Rebuild`), the `Stages` (staged SQL), and `Warnings`. You can print the SQL directly:

```csharp
var diff = builder.CompareToDiff("company", "st_employee");
var plan = new TableUpgradeOrchestrator("myDb", connectionManager).Plan(diff);

Console.WriteLine($"Mode: {plan.Mode}");
foreach (var sql in plan.AllStatements)
    Console.WriteLine(sql);
```

## 4. ALTER vs Rebuild: Which Path Is Taken

### Changes that take the ALTER path (sub-second)

The following changes are handled with `ALTER TABLE` in seconds, without copying data:

- Adding a column
- Type changes within the same family (e.g. `String(50) → String(100)`, `Integer → Long`)
- Toggling nullable, changing default
- Adding / dropping an index
- Narrowing a column length (requires `AllowColumnNarrowing = true`)

### Cases that trigger a rebuild

Every dialect starts from the same dialect-neutral rules, shown below; SQLite and Oracle add restrictions of their own.

| Category | Examples |
|----------|----------|
| **Cross-family type change** | `String → Integer`, numeric → `Date`, `Boolean → anything else`, `Binary → non-Binary`, `Guid ↔ String` |
| **AutoIncrement state toggle** | Plain column ↔ AutoIncrement column (for example, SQL Server `ALTER COLUMN` cannot toggle IDENTITY) |

> **SQLite** is the extreme case: its `ALTER TABLE` supports only `ADD` / `RENAME` / `DROP COLUMN`, so **any** change to a column's type, nullability, or default triggers a rebuild.
>
> **Oracle** also rebuilds when a text column crosses the LOB boundary (`VARCHAR2` ↔ `CLOB`), which its `ALTER ... MODIFY` rejects.

**Rebuild mechanism**: create a temporary table → `INSERT INTO tmp SELECT FROM original` → drop the old table → rename. For large tables (tens of millions of rows) this can take from minutes to hours and holds a table-level lock for the duration.

Columns the definition does not declare are rebuilt too, with their rows. Such a column keeps a default that is a plain literal; any other default, such as an expression an earlier version of Polhem used as its built-in default, becomes the built-in default of the column's type, and the plan lists the column in its warnings.

The rebuild keeps what the table holds:

- **AutoIncrement numbers** are copied, so `sys_no` keeps its values. The sequence then carries on after the old table's, so a number freed by a delete is not handed out again; on Oracle a few numbers may be skipped (the sequence's cached high-water mark). MySQL is the exception: its counter carries on after the highest copied number, so a number freed at the end of the table can come back.
- **Indexes only the database has** are not created again, because the framework reads back their columns but not their filter, included columns, prefix lengths or kind, so a copy could enforce something other than the original. A rebuild of a table that has one is refused before anything runs, naming the index: declare it in the definition, or drop it by hand, and upgrade again.
- **A plain column that becomes AutoIncrement** is not copied: the rows are numbered afresh, since its old values (often all `0`) cannot serve as identity values.

When the rebuild cannot finish, the table stays as it was: it stops before the old table is dropped. §7 describes what is recorded and how to resolve it.

### Automatic decision — no developer choice

The orchestrator inspects every change in the `TableSchemaDiff`:

- All changes ALTER-capable → ALTER path
- Any change requires rebuild → the whole table goes through rebuild
- Any change unsupported by the provider → throws and aborts
- A rebuild combined with a column rename (§6) → the rebuild copies the old column into its new name

> The design **intentionally exposes no Strategy option**: callers should not need to decide "ALTER or rebuild this time" — the choice is fully determined by the diff content.

## 5. UpgradeOptions

The options, as declared in [UpgradeOptions](../../../src/Polhem.Db/Schema/UpgradeOptions.cs) (comments shortened):

```csharp
public sealed class UpgradeOptions
{
    /// <summary>
    /// Allow a change that reduces a column's length / precision (may truncate data),
    /// whether it is applied by ALTER COLUMN or by rebuilding the table.
    /// Default false: narrowing is rejected to avoid silent data loss.
    /// </summary>
    public bool AllowColumnNarrowing { get; init; } = false;

    /// <summary>
    /// How the upgrade decides whether a table differs from its definition (§11).
    /// Default Fast: a table whose upgrade record says it is current is not compared.
    /// </summary>
    public UpgradeCompareMode CompareMode { get; init; } = UpgradeCompareMode.Fast;

    /// <summary>A shared instance with every option at its default.</summary>
    public static UpgradeOptions Default { get; } = new UpgradeOptions();
}
```

The properties are `init`-only, so options are set when the instance is created (`new UpgradeOptions { ... }`) and the shared `Default` cannot be changed.

### What `AllowColumnNarrowing` controls

When the definition specifies a length / precision smaller than the current column in the database:

- Default: **rejected with an exception** (`InvalidOperationException`) to avoid silent truncation
- Enabled: truncation is explicitly accepted and recorded under the plan's `Warnings`

The check applies to both paths. A plan that rebuilds the table (every column change on SQLite, or any plan containing a change that needs a rebuild) copies every row into the new column, so a narrowed column truncates or rejects data exactly as an `ALTER` would, and the same opt-in is required.

```csharp
var options = new UpgradeOptions { AllowColumnNarrowing = true };
builder.Execute("company", "st_employee", options);
```

> **When to enable**: you have already verified that existing data fits the new length (e.g. `SELECT MAX(LEN(col))`), or the column is brand-new with no data yet. **When not to enable**: for narrowing on a live business table, clean up the data first, then upgrade the schema.

## 6. Renaming a Column (`DbField.OriginalFieldName`)

By default Polhem will not infer "this column in the database that has no matching definition is a rename" — it is left in place as an extension column. To rename, mark the previous name **explicitly** in the definition:

```xml
<DbField FieldName="employee_no" OriginalFieldName="emp_no" Caption="Employee No." />
```

The comparer detects the `emp_no → employee_no` rename intent during upgrade and emits a `RenameFieldChange`, executed with the dialect's column rename (`sp_rename` on SQL Server, `ALTER TABLE ... RENAME COLUMN` on the others), which preserves the data.

### Rules

| Scenario | Behaviour |
|----------|-----------|
| DB has the old name `emp_no`, no new name | Rename the column |
| DB already has the new name `employee_no` | No rename; the column is compared as usual (idempotent, safe to re-run) |
| DB has neither old nor new name | Adds `employee_no` as a new column, without a warning |
| The same upgrade also needs a rebuild | The rebuild copies the old column into the new name (§4) |
| Multiple cumulative renames across versions | **Not supported**: clear `OriginalFieldName` after each release |

### When to use it

- ✅ Iterating on a brand-new module during development
- ❌ Cumulative renames across multiple already-deployed versions — version skipping is not guaranteed

> Remove `OriginalFieldName` **in the next release** after the rename has shipped, to avoid stale metadata.

## 7. Stages and Transaction Behaviour

On the ALTER path the orchestrator splits the SQL into ordered stages, each running in its own transaction:

```
Stage 1: DropIndexes        Drop indexes whose definition will change
Stage 2: AlterColumns       Rename / retype / resize / re-nullable existing columns
Stage 3: AddColumns         Add new columns
Stage 4: CreateIndexes      Create new indexes; recreate ones dropped in Stage 1
Stage 5: SyncDescriptions   Sync extended properties (MS_Description)
```

> Stage 5 (`SyncDescriptions`) is produced by the dialect's `IDescriptionSyncCommandBuilder`
> (`sp_addextendedproperty` on SQL Server, `COMMENT ON` on PostgreSQL / Oracle, an inline
> `COMMENT` clause on MySQL). A dialect that cannot store descriptions supplies no builder and
> the stage is skipped — SQLite is the only such dialect today.
>
> The stage also covers the columns added in Stage 3: the comparer only reports drift for columns
> present on both sides, so a column that does not exist in the database yet produces no drift
> entry of its own. Without this, a column added by an in-place upgrade would never receive its
> caption and every later comparison would report the table as out of date.

### Failure behaviour

- A stage failure → that stage's transaction rolls back, subsequent stages do not run, the exception propagates
- Stages that already committed are **not rolled back**
- The comparer is **idempotent**: after fixing the cause and re-running, completed stages produce no diff and are silently skipped

> The design intentionally avoids a single overall transaction: most databases do not provide reliable rollback for DDL across multiple statements; per-stage transactions plus an idempotent re-run is the more practical strategy.

### When the existing data blocks an upgrade

Some upgrades cannot be applied to the data a table already holds. For example, a text column that becomes a number while some rows hold text that is not a number, or a new unique index over duplicate values. The upgrade does not try to repair such data. Instead:

- **The table stays as it was.** On the ALTER path the failing stage rolls back, as described above. A rebuild stops before it drops the old table (§4), so the table keeps its columns and rows. On MySQL and Oracle, which commit DDL as they go, the temporary table `tmp_<table>` may be left behind; the next attempt drops it.
- **A rebuild that would lose part of the table is refused before anything runs.** This happens when a column has a type the framework does not map, or the database has an index the definition does not declare; the error names them.
- **The error is recorded per table, and the other tables are still upgraded.**
  - `db upgrade` (polhem-db, and DefineEditor through it) writes a line for each table, with the error for a table that failed, and exits with code `1` (see [Database Settings](database-settings-guide.md)).
  - In code, `IDatabaseInitializer.UpgradeTables` returns a `TableUpgradeReport`. Each `TableUpgradeEntry` whose `Outcome` is `Failed` carries the `Error`, and `Succeeded` is `false`.
  - `IDatabaseRepository.UpgradeTableSchema` and `TableSchemaBuilder.Execute` upgrade one table and throw the error to the caller.

To resolve a failure:

1. Read the error, and run the upgrade with `--dry-run` (or `TableSchemaBuilder.GetCommandText`) to see the statements it attempts (§9).
2. Decide which side is wrong: fix or move the rows that block the change, or change the definition.
3. Run the upgrade again. Upgrades are idempotent, so the work that already succeeded is skipped.

## 8. Unsupported Scenarios

The following are **outside the scope of automatic upgrade** and must be handled manually:

| Unsupported | Reason / Workaround |
|-------------|---------------------|
| **Drop column** | Continues the extension-field preservation policy; third-party integrations may rely on columns not listed in the definition. Run the SQL manually if you really need to drop |
| **Foreign key** | Framework principle: referential integrity belongs in business objects, not the database layer |
| **Trigger / View** | Same principle — business rules do not live in the database |
| **Multiple renames across versions** | Only single-step rename is guaranteed idempotent; deploy each rename individually |
| **Cross-database schemas** | Only the default schema is supported |
| **Online schema change** | E.g. SQL Server `ALTER INDEX ... WITH (ONLINE = ON)` — run manually |

## 9. Dry-Run and Deployment Practices

### Inspect the SQL before deployment

For large tables (tens of millions of rows), **strongly recommend a dry-run** to confirm the execution mode:

```csharp
var diff = builder.CompareToDiff("company", "ft_orders");
var plan = new TableUpgradeOrchestrator("myDb", connectionManager).Plan(diff);

if (plan.Mode == UpgradeExecutionMode.Rebuild)
{
    // This run will rebuild — schedule a maintenance window
    Console.WriteLine("Rebuild will be triggered:");
    Console.WriteLine(builder.GetCommandText("company", "ft_orders"));
}
```

### When to schedule a maintenance window

| Plan.Mode | Impact | Action |
|-----------|--------|--------|
| `NoChange` | None | No action |
| `Create` | New table, no data | Run anytime |
| `Alter` | Sub-second; brief per-column lock at most | Safe in normal hours |
| `Rebuild` | Full table copy, table-level lock | **Schedule a maintenance window** and estimate the copy time first |

### Standard procedure for column narrowing

Do not flip `AllowColumnNarrowing = true` on a live table. Recommended flow:

1. Dry-run to identify which columns will narrow
2. `SELECT COUNT(*) FROM table WHERE LEN(col) > <newLength>` to check for existing data
3. If any rows exceed → clean up the data first (trim, move to a new column, coordinate with the business)
4. Once all data fits → enable the option and upgrade

### Alternatives to rebuild on large tables

If the dry-run shows a rebuild but downtime is unacceptable, consider:

- Split the change: turn a cross-family type change into a multi-step deploy — add a new column → dual-write from the application → backfill → cut over → drop the old column
- Manual online schema change: run provider-specific SQL yourself, bypassing the Polhem upgrade
- Run side by side: leave the old table untouched, build new functionality on a new table, retire the old one over time

## 10. Renaming framework tables

> Manual rename DDL, for a database that holds a framework table under an earlier name. The example is the rename of `ft_department` / `ft_employee` to `st_department` / `st_employee`, made before Polhem 1.0: a database created by an earlier release that used the old names needs it to keep its data. Run against the **company database**.

The framework's auto-upgrade pipeline never renames tables on its own (see §8 Unsupported Scenarios). When the framework changes a system-table name across releases, deployments that pre-created the old name must rename manually.

### SQL Server / MySQL / Oracle / PostgreSQL

```sql
-- SQL Server
EXEC sp_rename 'ft_department', 'st_department';
EXEC sp_rename 'ft_employee',   'st_employee';

-- MySQL
RENAME TABLE ft_department TO st_department;
RENAME TABLE ft_employee   TO st_employee;

-- PostgreSQL
ALTER TABLE ft_department RENAME TO st_department;
ALTER TABLE ft_employee   RENAME TO st_employee;

-- Oracle
ALTER TABLE ft_department RENAME TO st_department;
ALTER TABLE ft_employee   RENAME TO st_employee;
```

Indexes named after the old table (e.g. `pk_ft_employee`, `rx_ft_employee`) are left alone by the rename and keep working on the renamed table. The framework's index-name templates use `{0}` for the table name, so the definition now expects `rx_st_employee` and so on. The next schema upgrade finds the primary key regardless of its name, but matches every other index by its formatted name: it does not find `rx_st_employee`, and creates it next to the old `rx_ft_employee`. Drop the old-named indexes by hand if you do not want the duplicates.

## 11. Upgrade records and the schema format

Every database the upgrade runs on keeps a record of each table's last upgrade in `st_table_upgrade`. The upgrade
creates the table the first time it upgrades a table of that database, or compares one and finds it matching; a dry run
writes nothing. The records do two things: they keep a tool of an older framework from undoing what a newer one
did, and they let the upgrade skip the comparison of a table whose record says it is current.

### The schema format

`SchemaFormat.Current` is the version of the upgrade's behaviour: the statements each dialect writes, the shape each
provider reads back, and how a rebuild works. It is raised when that behaviour changes, not with every release, and
each record carries the format of the tool that wrote it.

A tool of a lower format must not change a database that a higher one upgraded: it would read the newer shape as a
difference and change it back, and the two would undo each other on every run. Before it runs any statement, the
upgrade reads the highest format recorded in the database and stops when it is higher than its own:

- `db upgrade`, `db upgrade --dry-run` and `IDatabaseInitializer.UpgradeTables` report the database once in
  `TableUpgradeReport.Problems`, skip its tables and go on with the other databases.
- `IDatabaseRepository.UpgradeTableSchema`, `TableSchemaBuilder.Execute` and `TableUpgradeOrchestrator.Execute` throw an
  `InvalidOperationException`.

The message names the database, the format and the framework version that upgraded it, and says that nothing was
changed. Upgrade that database with a framework of that version or later. A record whose status is `Verified` does not
count: it says that a tool compared the table and found nothing to change, so it does not keep an older tool out after
a deployment that changed no table.

The check protects a database from the first release that records upgrades on. A tool released before that keeps no
records and reads none.

### Compare modes

`UpgradeOptions.CompareMode` decides how the upgrade finds out whether a table differs from its definition:

| Mode | What it does |
|------|--------------|
| `Fast` (default) | When the table's record says that the last upgrade succeeded or found it matching, with the current schema format and the definition as it is now, the table counts as unchanged and the database's catalog is not read. Otherwise the table is compared as in `Full`. |
| `Full` | Reads the table's schema from the database's catalog and compares it with the definition. |

The records of a database are read once, before its first table, and only their small columns (not `schema_xml`), so
the cost does not grow with the size of the recorded schemas. Reading the catalog is what makes a comparison slow on
SQL Server and Oracle, and `Fast` avoids it for every table that has not changed.

`Fast` does not see a change made to a table outside the framework, such as a column or an index dropped by hand, until
the definition changes. Use `Full` when that matters: `db upgrade --full-compare`, or
`new UpgradeOptions { CompareMode = UpgradeCompareMode.Full }`. A `Full` comparison that finds nothing to change writes
the record, so the next `Fast` run can use it.

`TableSchemaBuilder.GetCommandText` and `CompareToDiff` always compare in full and write no record.

### The record table

| Column | Content |
|--------|---------|
| `table_name` | The table, in lowercase (primary key). |
| `format_version` | The schema format of the tool that wrote the record. |
| `framework_version` | The framework version of that tool. |
| `status` | `Upgrading`, `Succeeded`, `Failed` or `Verified` (`TableUpgradeStatus`). |
| `error` | The error of a failed upgrade. |
| `schema_xml` | The definition the table was last upgraded to successfully. |
| `schema_hash` | The SHA-256 of `schema_xml`, which `Fast` compares with the current definition. |
| `sys_update_time` | When the record was written. |

The record is marked `Upgrading` before the first statement runs, and gets the outcome afterwards. A record left at
`Upgrading` means the process stopped during the upgrade; a `Failed` record keeps the error and the schema of the last
upgrade that succeeded. Either way the next run compares the table in full. `db upgrade` notes both on the table's line.

To see the records, query the table, or read one in code with `TableUpgradeRecordStore.GetRecord`:

```sql
SELECT table_name, status, format_version, framework_version, sys_update_time, error
FROM st_table_upgrade
ORDER BY sys_update_time DESC;
```

Do not edit the records by hand. Deleting a table's record is safe: the next run compares that table in full and
records it again.

The reasons for the records, the format and the `Verified` status are in
[ADR-065](../../../maintainers/adr/adr-065-table-upgrade-records-and-schema-format.md).

## 12. References

### Source files
- [TableSchemaBuilder](../../../src/Polhem.Db/Schema/TableSchemaBuilder.cs) — public entry point
- [TableUpgradeOrchestrator](../../../src/Polhem.Db/Schema/TableUpgradeOrchestrator.cs) — Plan / Execute
- [TableSchemaDiff](../../../src/Polhem.Db/Schema/TableSchemaDiff.cs) / [UpgradePlan](../../../src/Polhem.Db/Schema/UpgradePlan.cs)
- [UpgradeOptions](../../../src/Polhem.Db/Schema/UpgradeOptions.cs)
- [SchemaFormat](../../../src/Polhem.Db/Schema/SchemaFormat.cs) / [TableUpgradeRecordStore](../../../src/Polhem.Db/Schema/TableUpgradeRecordStore.cs) — the schema format and the upgrade records
- [DbField.OriginalFieldName](../../../src/Polhem.Definition/Database/DbField.cs)

### Related documents
- [Database Naming Conventions](database-naming-conventions.md)
- [Architecture Overview](../architecture/architecture-overview.md)
- [Development Cookbook](../guides/development-cookbook.md)
- [Development Constraints](../architecture/development-constraints.md)
- [ADR-005: FormSchema-Driven](../../../maintainers/adr/adr-005-formschema-driven.md)
