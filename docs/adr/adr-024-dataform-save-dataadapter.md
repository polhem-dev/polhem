# ADR-024: DataForm persistence moves to a DataTable-level DataAdapter

[繁體中文](adr-024-dataform-save-dataadapter.zh-TW.md)

## Status

Accepted (2026-06-15)

## Context

[ADR-001](adr-001-dataset-as-dto.md) carries master-detail data in a DataSet; `DataFormRepository.Save` is responsible
for persisting the DataSet's changes to the database. The original approach checked `DataRow.RowState` **row by
row**, used `IFormCommandBuilder` to build each row's SQL dynamically (an UPDATE includes only "the columns that
changed"), and then executed them as a batch.

This design has a structural flaw: for a row that is `Modified` but **has no actual column changes** (situations such
as a grid re-realizing, or a recomputation writing back the same value, leave RowState=Modified while every cell has
Original==Current), the per-row builder produces an **empty SET clause** → the framework throws
`UPDATE would be empty`. This was exactly the root cause of saving an order again in `apps/Polhem.Northwind` failing
after deleting a detail row: the master table had no real change but was judged Modified. At the time a
`ComputeAmounts` guard mitigated it for the master table, but the foundation stayed fragile: any "Modified but
unchanged" row would hit it.

## Options considered

1. **Special-case each row: "skip it if nothing changed"**: diff Original vs Current before the per-row builder and
   skip when everything is equal. Treats the symptom: every RowState path has to remember to add the check, and the
   exposure to an empty SET remains (a future new write path can easily hit it again).
2. **Switch to ADO.NET `DataAdapter.Update` (adopted)**: each DataTable gets three **all-column parameterized**
   commands, Insert/Update/Delete, and `DataAdapter.Update` applies them by RowState. A Modified row is written back
   with an "all-column UPDATE"; even when the values are the same it is only a harmless same-value update, and **an
   empty SET is structurally impossible**. The whole class of bug is eliminated by design, with no per-row special
   cases.
3. **Switch to an ORM** (EF Core and the like): the editing model brings its own change tracking, but it conflicts
   with the framework's core model of "DataSet-as-DTO, definition-driven", and the amount of refactoring is out of
   proportion.

## Decision

Adopt **option 2**:

- `DataFormRepository.Save` goes in `[master, details...]` order; for each table, `FormTable.GenerateDbTable()` gets
  the `TableSchema` → `TableSchemaCommandBuilder.BuildUpdateSpec(dataTable)` produces a `DataTableUpdateSpec` (the
  three Insert/Update/Delete commands + the DataTable, with parameters bound to DataColumns through
  `SourceColumn`/`SourceVersion`).
- **Cross-table atomicity (D1)**: add `DbAccess.UpdateDataTables(IReadOnlyList<DataTableUpdateSpec>)`, which opens a
  **single transaction** and runs `DataAdapter.Update` for each spec in order, committing only if all succeed. One
  master-detail Save involves several tables, and a single transaction ensures a failure halfway leaves no partial
  data; the FK order is naturally correct (the master is inserted first, the details after).
- **No changes is a no-op (D2)**: when the whole DataSet has no pending changes it returns 0 instead of throwing
  (aligned with the `DataAdapter` convention).
- **Filling the SQLite adapter gap**: Microsoft.Data.Sqlite **does not provide a `DbDataAdapter`**
  (`CreateDataAdapter()` returns null), while SQLite is the database used by the demos and most local tests. The
  originally evaluated fallback, "no adapter → manually apply the prebuilt commands row by row", was in the end
  replaced by a **home-made `SqliteDataAdapter`** (wrapped through `SqliteProviderFactory`): the 5 providers (SQL
  Server / PostgreSQL / SQLite / MySQL / Oracle) **all take the adapter path**, with no provider-specific branch.
- **Retiring the per-row builders (D3)**: after Save was rewritten, the per-row `InsertCommandBuilder` /
  `UpdateCommandBuilder` had no production users left and were removed from `src/Polhem.Db/Dml` (relocated to
  `tests/Polhem.Tests.Shared/` as test seeding / round-trip tools). `DeleteCommandBuilder` / `SelectCommandBuilder`
  are still used by `Delete()` / `GetData()` and are kept.

## Consequences

- The whole class of errors "Modified but no column changes → empty SET → error" is eliminated by design; saving an
  existing master-detail document again is no longer fragile.
- The read/write paths of the 5 DB providers are the same (all `DataAdapter`), with one less provider-specific
  fallback in the code.
- The `ref_*` RelationField / virtual columns in the DataSet are not among the command parameters, so `DataAdapter`
  ignores them automatically, with no side effects.
- The only production caller is `FormBusinessObject.Save`, so the blast radius is small; `Delete()` / `GetData()` /
  `GetNewData` / `GetList` are unaffected.
- Removing the per-row IUD builders from `Polhem.Db` is breaking (see CHANGELOG 4.10.0), but it is limited to the
  framework's construction surface and has no external consumers.
- An all-column UPDATE writes a few more columns than "update only the changed columns"; for a single-record form
  Save the cost is negligible, and what it buys is robustness by design.
