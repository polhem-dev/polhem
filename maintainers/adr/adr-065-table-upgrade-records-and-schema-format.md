# ADR-065: Each table upgrade is recorded with the schema format, and an older format may not change a newer database

## Status

**Accepted (2026-11-16)**

## Context

The table upgrade compares the schema it reads back from a database with the definition, and changes the table where
they differ. A framework release can change what a definition turns into: #508 changed the default of a `Date` column on
SQLite and Oracle to midnight of the UTC day, and #515 changed how SQL Server's numeric defaults are read back. A tool of
the earlier release that meets a database the later one upgraded reads the new shape as a difference and changes it
back; the next run of the later release changes it again. The two undo each other on every run, and on SQLite each
round rebuilds the table.

Such older tools are common in practice: an old `polhem-db`, an old DefineEditor, an application host that upgrades its
tables at start-up and has not been redeployed yet. By the fourth table upgrade principle (when the upgrade cannot
proceed safely, it stops and reports), the older tool has to stop, not push through.

Two further needs came with it: seeing the state of each table's upgrade (did it succeed, which tool did it, with which
definition), and a cheaper start-up. Every comparison reads the database's catalog, although most start-ups change
nothing. Measured on 2026-11-15 with local containers, 26 framework tables over three databases, the full comparison
took 599 ms on Oracle, 288 ms on SQL Server, 56 ms on PostgreSQL, 48 ms on MySQL and 1.2 ms on SQLite; reading all the
records of a database once took between 0.2 and 1.5 ms.

## Decision

### 1. A schema format, separate from the framework version

`SchemaFormat.Current` (`src/Polhem.Db/Schema/SchemaFormat.cs`) is an integer that versions the upgrade's behaviour: the
DDL each dialect emits, the shape each provider reads back, how a rebuild works, and what the comparer detects. It is
raised only when one of those changes. The framework version would not do: most releases change none of them, and
keying on the release would refuse every earlier tool after every release for no reason. Format 1 is the behaviour after
#508, #513, #514 and #515.

### 2. One record per table in `st_table_upgrade`, in every database

Each database the upgrade runs on (common, each company database, log) has a table `st_table_upgrade` with one row per
table: the table name, the format and framework version of the tool, the status, the error, the definition last applied
successfully (`schema_xml`) and its SHA-256 (`schema_hash`). The framework builds its `TableSchema` in code and creates
the table lazily with the ordinary create path of each dialect. It is not registered in `DbCategorySettings`; it plays
the part of a migration history table.

An upgrade marks the record `Upgrading` before its first statement and writes `Succeeded` or `Failed` afterwards. A
failed record keeps the schema of the last upgrade that succeeded.

### 3. A database a higher format upgraded is not changed

Before any statement runs, the upgrade reads the highest format recorded in the database. When it is higher than
`SchemaFormat.Current`, nothing is changed: `DatabaseInitializer` (behind `db upgrade` and host start-up) reports the
database once and goes on with the others, and the single-table APIs throw. There is no switch to override it; the fix
is a newer tool.

### 4. A comparison that changed nothing is recorded as `Verified`, which does not count for the check

When a full comparison finds a table already matching, the upgrade records it so the fast compare can use it. Recording
it as `Succeeded` under the current format would make a deployment that changed no table refuse a rollback to the
previous tool. So such a record becomes `Verified`, unless this format wrote the existing record, and the check of
decision 3 ignores `Verified` records.

### 5. The fast compare trusts a current record and is the default

`UpgradeCompareMode.Fast` takes a table as unchanged, without reading the catalog, when its record is `Succeeded` or
`Verified`, carries the current format, and has the hash of the current definition. Anything else falls back to the full
comparison, and so does a record table that cannot be read. The records of a database are read once, with only the
small columns; `schema_xml` is never part of that read, so the memory does not grow with the recorded schemas even for
thousands of tables.

A table changed by hand outside the framework is not seen by `Fast` until its definition changes. `Full`
(`db upgrade --full-compare`) is there for that. `TableSchemaBuilder.GetCommandText` and `CompareToDiff` always compare
in full and write nothing.

### 6. A test keeps the format honest for DDL

`SchemaFormatDdlSnapshotTests` (tests/Polhem.Db.UnitTests) plans the definition of `UpgradeIdempotencyGateTests` on
every dialect through the create, add-column and rebuild paths, and compares the statements with snapshots stored per
format. A change in the output fails until the format is raised; the snapshots of a format are never rewritten. A change
in what a provider reads back or what the comparer detects does not show in the DDL, so raising the format for those is
a rule in `.claude/rules/database.md`, checked in review.

## Consequences

- Tools released before the records existed keep none and read none, so they are not stopped. The protection starts
  with the release that introduced the records.
- The check and the record write are separate statements: an older and a newer tool started at the same moment on the
  same database can both pass the check. The race needs two deployments of different versions running together.
- `Fast` makes a manual change to a table invisible until the definition changes or a full comparison runs. That is the
  price of a start-up that reads no catalog, and the option to compare in full stays.
- Raising the format makes every table compare in full once after the deployment, and refuses tools of the older format
  on every database the new one upgraded.
- `st_table_upgrade` itself has a schema. Changing it later is an upgrade of a framework table like any other, and
  raises the format.
