# ADR-008: Polhem.Db namespace layout: separating the syntax layer from the model layer

[繁體中文](adr-008-polhem-db-namespace-layout.zh-TW.md)

## Status

Accepted (2026-04-27)

## Context

After the SQLite provider was added to `Polhem.Db`, its namespace structure showed the following problems of unclear
ownership:

1. The name `Polhem.Db.Sql` is vague. In practice it holds only DML building blocks (query composition + IUD
   builders), but the name easily suggests that it contains DDL too.
2. The root of `Polhem.Db.Providers` lays out interfaces of four different functions side by side:
   - DDL string generation contracts (`ICreateTableCommandBuilder` / `ITableAlterCommandBuilder` /
     `ITableRebuildCommandBuilder`)
   - Schema model reading (`ITableSchemaProvider`)
   - The DML string generation contract (`IFormCommandBuilder`)
   - The provider factory (`IDialectFactory`)
3. `Polhem.Db.Schema` carries the name "Schema", but its actual content (`TableSchemaBuilder` /
   `TableSchemaComparer` / `TableSchemaDiff` / `TableUpgradeOrchestrator` / `UpgradePlan` and so on) generates no SQL;
   it only compares models and coordinates the upgrade flow. DDL string generation is instead concentrated in
   `Providers/`.
4. Two DML-related files sit isolated at the root:
   - `TableSchemaCommandBuilder`: generates INSERT/UPDATE/DELETE from a `TableSchema`, pure DML string generation
   - `JoinType`: an enum used only by SELECT, used only by the SQL builders

Without a clear rule, future DDL contracts (such as index management or TCL) will keep adding to the confusion about
where things belong.

## Decision

Adopt the design principle "**separate the syntax layer from the model layer; group contracts by function and
implementations by provider**", with the following namespace layout:

| Layer | Namespace | Characteristic function |
|-------|-----------|-------------------------|
| Syntax layer | `Polhem.Db.Ddl` / `Polhem.Db.Dml` | Generates SQL strings (including their abstract contracts) |
| Model layer | `Polhem.Db.Schema` | Operates on the `TableSchema` data model (comparison, upgrade flow) |
| Factory | `Polhem.Db.Providers` | Binds the syntax layer contracts to a provider |
| Implementation | `Polhem.Db.Providers.{X}` | Each provider is responsible for the DDL / DML / reading implementations of its family |

### Three key points

1. **Separate the syntax layer (`Polhem.Db.Ddl` / `Polhem.Db.Dml`), the model layer (`Polhem.Db.Schema`) and the
   factory (`Polhem.Db.Providers`)**

   - `Polhem.Db.Ddl`: the DDL string generation contracts (`ICreateTableCommandBuilder`,
     `ITableAlterCommandBuilder`, `ITableRebuildCommandBuilder`)
   - `Polhem.Db.Dml`: DML string generation + building blocks (SELECT/INSERT/UPDATE/DELETE builders,
     `IFormCommandBuilder`, `TableSchemaCommandBuilder`, `JoinType`, the context / parameter collector and so on)
   - `Polhem.Db.Schema`: operations on the `TableSchema` model (builder façade, comparer, diff, upgrade orchestrator,
     plan, stage and so on) + the reading contract (`ITableSchemaProvider`)
   - `Polhem.Db.Providers`: keeps only `IDialectFactory`

   The upgrade flow in `Schema` calls the DDL builders, and the DDL contracts in turn take the change models of
   `Schema` (`TableSchemaDiff`, the types in `Polhem.Db.Schema.Changes`) as input, so the two namespaces reference each
   other; both stay separate from `Polhem.Db.Dml`.

2. **Group contracts by function, implementations by provider**

   Following the convention of the .NET BCL's `System.Data` / `System.Data.SqlClient`:
   - Interfaces / abstract contracts → grouped by "what they do" (`Polhem.Db.Ddl` / `Polhem.Db.Dml` /
     `Polhem.Db.Schema`)
   - Concrete per-provider implementations → all go into `Polhem.Db.Providers.{SqlServer|PostgreSql|Sqlite}`

   The benefits of this principle:
   - All implementations for one provider (CREATE, ALTER, REBUILD, Form CRUD, SchemaProvider and so on) share one
     namespace, which makes them easy to write and review
   - A single external registration entry point: `using Polhem.Db.Providers.Sqlite; new SqliteDialectFactory();`
   - No second split into DDL / DML inside a provider subfolder (each provider subfolder has about 8-9 files, so a
     split costs more than it is worth)

3. **The boundary test: "does it generate SQL strings?"**

   When adding a class / interface in the future, place it by this test:
   - **Yes** → the syntax layer (`Polhem.Db.Ddl` or `Polhem.Db.Dml`, depending on the kind of SQL)
   - **No, and it operates on a data model such as `TableSchema`** → `Polhem.Db.Schema`
   - **It is a factory / registration mechanism** → `Polhem.Db.Providers`
   - **It is a concrete per-provider implementation** → `Polhem.Db.Providers.{X}`

## Outcome

### Namespace allocation after adoption

```
Polhem.Db                       # Cross-cutting infrastructure: DbAccess, DbAccessFactory, DbCommandSpec, DbBatchSpec,
                             # DbConnectionScope, ILMapper, DbCommandKind, DbBatchResult and other execution core types
Polhem.Db.Manager               # IDbConnectionManager, DbProviderRegistry, DbDialectRegistry
Polhem.Db.CacheNotify           # Cross-process cache invalidation notification
Polhem.Db.Storage               # DbDefineStorage (the storage implementation that puts definitions in the DB)
Polhem.Db.Ddl                   # DDL string generation contracts (the I*CommandBuilder interfaces)
Polhem.Db.Dml                   # DML string generation + building blocks (including IFormCommandBuilder, TableSchemaCommandBuilder, JoinType)
Polhem.Db.Schema                # TableSchema model / comparison / upgrade flow + ITableSchemaProvider
Polhem.Db.Schema.Changes        # Change models such as Add/Alter/Drop/Rename Field / Index
Polhem.Db.Providers             # IDialectFactory only
Polhem.Db.Providers.SqlServer   # SQL Server implementation (DDL + DML + SchemaProvider + Helper + TypeMapping)
Polhem.Db.Providers.PostgreSql  # Same as above (PostgreSQL)
Polhem.Db.Providers.Sqlite      # Same as above (SQLite)
```

### External API changes

The main `using` mappings that affect external users:

| Old | New |
|-----|-----|
| `using Polhem.Db.Sql;` | `using Polhem.Db.Dml;` |
| `using Polhem.Db.Providers;` when referencing `IFormCommandBuilder` | `using Polhem.Db.Dml;` |
| `using Polhem.Db.Providers;` when referencing the DDL contracts (`ICreateTableCommandBuilder` and others) | `using Polhem.Db.Ddl;` |
| `using Polhem.Db.Providers;` when referencing `ITableSchemaProvider` | `using Polhem.Db.Schema;` |

The provider registration entry point (`using Polhem.Db.Providers.Sqlite; DbDialectRegistry.Register(...)`) is
completely unchanged.

No separate version bump (stays on `4.0.x`); listing the mapping table in the release notes is enough.

## Alternatives considered (evaluated and rejected)

1. **The "minimal change" option**: only rename `Sql → Dml`, leaving the mix inside `Providers/` alone.
   - Reason for rejection: DDL and DML contracts stay mixed, and the schema reading contract is treated as a
     companion of DDL; future contracts would still run into the same unclear ownership.
2. **Rename `Polhem.Db.Schema` → `Polhem.Db.Ddl` wholesale**: on the surface it would be symmetric with
   `Polhem.Db.Dml`.
   - Reason for rejection: `Schema/*` generates no SQL (it only compares models and runs the flow). Renaming it to
     `Ddl` would mix the two concerns of the "model layer" and the "syntax layer", which is fundamentally wrong.
3. **Split each provider subfolder further into DDL/DML sub-namespaces** (such as
   `Polhem.Db.Providers.Sqlite.Ddl`).
   - Reason for rejection: poor symmetry, and each provider subfolder has few files (about 8-9), so a split costs more
     than it is worth. It violates the simplicity of "implementations are grouped by provider".

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **2026-09-27: more contracts and providers.** `Polhem.Db.Ddl` also holds `IDescriptionSyncCommandBuilder`, and
  there are provider namespaces for MySQL and Oracle (`Polhem.Db.Providers.MySql`, `Polhem.Db.Providers.Oracle`)
  next to the ones listed above. The SQL Server folder no longer has separate Helper and TypeMapping files; its
  dialect rules live in `SqlSchemaSyntax`.
- **2026-09-27: the root of `Polhem.Db.Providers`.** `IDialectFactory` is still its only public type, but it also
  holds internal helpers shared by the provider implementations (such as `IndexStatementJoiner` and
  `SqlLiteralParser`).

## Related documents

- Package README: [`src/Polhem.Db/README.md`](../../src/Polhem.Db/README.md)
