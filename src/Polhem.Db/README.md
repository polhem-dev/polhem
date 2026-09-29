# Polhem.Db

> Database abstraction layer providing dynamic SQL generation, parameterized queries, multi-database support, and row-to-object mapping.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: Data Access Layer (infrastructure)
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/architecture/dependency-map.md). Not enumerated here — the csproj files are the authority, and a prose copy in every package README drifts with nothing to catch it. These did: `Polhem.Hosting` was missing as a dependent from four of them for months after it was extracted.

## Target Framework

- `net10.0` -- access to modern runtime APIs and performance improvements

## Key Features

### Database Access

- `DbAccess` -- primary entry point for executing queries, batch commands, and DataTable updates
- `DbConnectionScope` -- scoped connection lifetime management
- `DbCommandSpec` -- parameterized command specification supporting positional (`{0}`) and named (`{Name}`) placeholders with automatic conversion
- `DbBatchSpec` -- batch execution with optional transaction wrapping and configurable isolation levels

### Connection & Provider Management

- `IDbConnectionManager` -- centralized connection information registry
- `DbProviderRegistry` -- database provider factory resolution
- `DbConnectionInfo` -- connection metadata (connection string, database type, provider)

### Query Composition

> Polhem.Db is **`FormSchema`-driven**: `FormSchema` describes business entities, and the query context recursively walks `FormSchema` chains to expand JOINs — yielding a "form-level relation" data-access experience distinct from ORM. See [FormSchema-Driven Database Access](../../docs/en/definitions/formschema-data-access.md).

- `SelectCommandBuilder` -- builds SELECT commands from `FormSchema` definitions
- `SelectBuilder` / `FromBuilder` / `WhereBuilder` / `SortBuilder` / `LimitBuilder` -- composable builders for the SELECT, FROM, WHERE, ORDER BY and row-limit clauses
- `SelectContext` -- query context tracking field mappings and table joins
- `WhereBuilder` -- filter-to-SQL translation with parameterized output

### Multi-Database Support

The framework routes SQL generation and schema reading by `DatabaseType` through a dialect factory layer:

- `IDialectFactory` -- per-provider factory exposing `IFormCommandBuilder`, `ICreateTableCommandBuilder`, `ITableAlterCommandBuilder`, `ITableRebuildCommandBuilder`, `ITableSchemaProvider`, and `GetDefaultValueExpression(FieldDbType)`
- `DbDialectRegistry` -- maps `DatabaseType` to its `IDialectFactory` (mirrors how `DbProviderRegistry` maps to ADO.NET `DbProviderFactory`); registration is explicit and performed by the host
- Built-in dialect implementations:
  - **SQL Server** (`Providers/SqlServer/`) -- full support: form SELECT / INSERT / UPDATE / DELETE, CREATE/ALTER/REBUILD DDL, schema introspection via the `sys.*` catalog views
  - **PostgreSQL** (`Providers/PostgreSql/`) -- full support: form SELECT / INSERT / UPDATE / DELETE, CREATE/ALTER/REBUILD DDL, schema introspection via `information_schema` + `pg_catalog`
  - **SQLite** (`Providers/Sqlite/`) -- full support: form SELECT / INSERT / UPDATE / DELETE, CREATE DDL, ALTER (limited to ADD / RENAME COLUMN / Index — every other column-level mutation falls back to REBUILD), schema introspection via `sqlite_master` + `PRAGMA`. Targeted at file-backed single-process and embedded scenarios; see the limitations list below
  - **MySQL** (`Providers/MySql/`) -- full support: form SELECT / INSERT / UPDATE / DELETE, CREATE/ALTER/REBUILD DDL, schema introspection via `information_schema`
  - **Oracle** (`Providers/Oracle/`) -- full support: form SELECT / INSERT / UPDATE / DELETE, CREATE/ALTER/REBUILD DDL, schema introspection via `USER_*` data-dictionary views. Identifiers are emitted as quoted-UPPERCASE (`"ST_USER"`) — aligning with Oracle's natural unquoted-fold-to-UPPER convention while keeping reserved-word columns and special-character names safe. The provider lowercases identifiers at the read-back boundary so the rest of the framework (FormSchema, Repository, Business) sees a consistent lowercase abstraction across every supported database. See [docs/en/database/database-naming-conventions.md §5.3](../../docs/en/database/database-naming-conventions.md) for the full identifier strategy

#### SQLite Known Limitations

The following are SQLite engine or `Microsoft.Data.Sqlite` driver capability differences for which the framework has made deliberate trade-offs along its corresponding code paths:

- **`ALTER TABLE` is severely restricted**: SQLite supports only `ADD COLUMN` / `RENAME COLUMN` (3.25+) / `DROP COLUMN` (3.35+) / `RENAME TO`; column type, nullability, default, and PK changes are unsupported. Every `AlterFieldChange` therefore falls through the rebuild path (drop / create temp / copy / drop old / rename).
- **AutoIncrement must be inlined as the primary key**: `INTEGER PRIMARY KEY AUTOINCREMENT` must appear on the column definition itself; SQLite refuses to attach `AUTOINCREMENT` via an external `CONSTRAINT pk_xxx PRIMARY KEY (...)`. `SqliteCreateTableCommandBuilder` inlines it automatically and throws `InvalidOperationException` when an AutoIncrement column conflicts with a PK index pointing at a different column.
- **No `COMMENT ON`**: SQLite does not persist `DisplayName` / `Caption`; `SqliteCreateTableCommandBuilder` is silent no-op for descriptions and `SqliteTableSchemaProvider` always reads them back as empty. Keep captions in the FormSchema XML at the application layer.
- **TYPE AFFINITY rather than strict types**: declared type strings such as `VARCHAR(50)` / `NUMERIC(18,2)` are written verbatim and SQLite applies affinity rules. `SqliteTableSchemaProvider` reverse-parses them from `PRAGMA table_info`.
- **No schema concept**: every table lives in `main`, identifiers are always unqualified (still `"..."`-quoted).
- **The `DbDataAdapter` is supplied by the framework**: `Microsoft.Data.Sqlite.SqliteFactory` does not provide a `DbDataAdapter`. The framework's `SqliteProviderFactory` wrapper adds a home-grown `SqliteDataAdapter`, so **once that wrapper is registered (see the registration example below), `DbAccess.UpdateDataTable` works exactly as it does on the other providers** — SQLite runs through the same adapter-based read/write path and needs no bespoke fallback. Registering the raw `SqliteFactory.Instance` directly is what loses this.
- **PK index naming**: SQLite auto-generates the PK backing index as `sqlite_autoindex_*`; `SqliteTableSchemaProvider` normalises it to the framework's `pk_{table}` convention so `TableSchemaComparer` matches by name.
- **Driver**: uses [`Microsoft.Data.Sqlite`](https://learn.microsoft.com/dotnet/standard/data/sqlite/); recommended connection strings are an in-memory shared cache `Data Source=file:polhem_test_sqlite?mode=memory&cache=shared` for tests, or `Data Source={path}.db` for file-backed deployments.

`Polhem.Db` itself has zero ADO.NET driver dependencies; the driver lives in the host application.

### Provider Registration

The host application enables a database by registering two things at startup: the ADO.NET `DbProviderFactory` (for connections) and the `IDialectFactory` (for SQL generation). Any combination is allowed; only register what your app actually uses.

```csharp
using Polhem.Db.Manager;
using Polhem.Db.Providers.PostgreSql;
using Polhem.Db.Providers.Sqlite;
using Polhem.Db.Providers.SqlServer;
using Polhem.Definition.Database;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;

// SQL Server
DbProviderRegistry.Register(DatabaseType.SQLServer, SqlClientFactory.Instance);
DbDialectRegistry.Register(DatabaseType.SQLServer, new SqlDialectFactory());

// PostgreSQL
DbProviderRegistry.Register(DatabaseType.PostgreSQL, NpgsqlFactory.Instance);
DbDialectRegistry.Register(DatabaseType.PostgreSQL, new PgDialectFactory());

// SQLite: wrap the driver's factory so a DbDataAdapter is available (see the limitations above)
DbProviderRegistry.Register(DatabaseType.SQLite, new SqliteProviderFactory(SqliteFactory.Instance));
DbDialectRegistry.Register(DatabaseType.SQLite, new SqliteDialectFactory());

// Configure connection items in DatabaseSettings (typically loaded from XML);
// each item picks its DatabaseType and one of the registered providers.
```

A `DatabaseItem` carries `Id`, `DatabaseType`, and `ConnectionString`. The framework looks up the provider/dialect by `DatabaseType` whenever a `DbAccess` / `TableSchemaBuilder` / `TableUpgradeOrchestrator` is created with that item's `Id`. The `{@DbName}`, `{@UserId}` and `{@Password}` placeholders are resolved by
`ConnectionStringTemplate`. PostgreSQL connection string template:

```
Host=localhost;Port=5432;Database={@DbName};Username={@UserId};Password={@Password}
```

### Schema Introspection & Upgrade

- `ITableSchemaProvider` -- per-provider reader of the live database schema (the sources each provider reads are listed above)
- `TableSchemaBuilder` -- compares the defined schema against the live database and produces or executes the upgrade commands
- `TableSchemaComparer` -- structured diff (`TableSchemaDiff`) listing add/alter/drop changes
- `TableUpgradeOrchestrator` -- ALTER-based upgrade with rebuild fallback when ALTER cannot apply all changes; routes through the dialect factory
- `ITableAlterCommandBuilder` / `ITableRebuildCommandBuilder` -- per-provider DDL generation for in-place ALTER and full table rebuild
- `TableSchemaCommandBuilder` -- generates IUD commands from `TableSchema`

### Object Mapping

- `DbAccess.Query<T>` / `QueryAsync<T>` -- run a command and map each row to a `T` (a type with a
  parameterless constructor), matching columns to settable public properties by name, ignoring case. The mapper is an internal
  IL-emitted delegate cached per result shape


### Temporal Types Across Providers

`FieldDbType` has three temporal members and they map very differently:

| `FieldDbType` | SQL Server | PostgreSQL | MySQL | Oracle | SQLite |
|---|---|---|---|---|---|
| `Date` | `date` | `date` | `DATE` | `DATE` | `DATE` |
| `DateTime` | `datetime2` | `timestamp` | `DATETIME(6)` | `TIMESTAMP(6)` | `DATETIME` |
| `Time` | `nchar(5)` | `char(5)` | `CHAR(5)` | `VARCHAR2(5)` | `VARCHAR(5)` |

`Time` is carried as a fixed-width `"HH:mm"` string rather than a native time type. Native time
types differ too much across providers (range, precision, whether they are an interval or a
clock reading) to round-trip a wall-clock value reliably, and a time of day is never time-zone
converted — see [ADR-033](../../maintainers/adr/adr-033-time-of-day-semantics.md).

> Writing a custom dialect? `GetDefaultValueExpression(FieldDbType)` and your type mapping must
> both handle `Time`. It was appended to the enum, so an existing `switch` compiles fine and
> silently falls through to its default branch.

## Key Public APIs

| Class / Interface | Purpose |
|-------------------|---------|
| `DbAccess` | Execute queries, batch commands, and DataTable updates |
| `DbCommandSpec` | Parameterized command specification with placeholder auto-conversion |
| `DbBatchSpec` | Batch command execution with transaction support |
| `SelectCommandBuilder` | FormSchema-driven SELECT command building |
| `IDialectFactory` | Per-provider factory for SQL / schema builders |
| `IFormCommandBuilder` | Provider-specific CRUD generation interface |
| `ITableSchemaProvider` | Provider-specific live-database schema reader |
| `DbDialectRegistry` | `DatabaseType` → `IDialectFactory` registry |
| `IDbConnectionManager` | Connection information registry |
| `DbProviderRegistry` | ADO.NET `DbProviderFactory` resolution |
| `TableSchemaCommandBuilder` | Schema-based IUD command generation |

## Design Conventions

- **Builder Pattern** -- query composition through `SelectBuilder`, `FromBuilder`, `WhereBuilder` and `SortBuilder`, each responsible for a single SQL clause. They are concrete classes: the matching one-implementation interfaces were removed, because no caller ever held one by its interface type.
- **Specification Pattern** -- `DbCommandSpec`, `DbBatchSpec`, and `DataTableUpdateSpec` encapsulate execution intent as data, decoupling command definition from execution.
- **IL Emit Mapping** -- `Query<T>` maps rows through `DynamicMethod` delegates generated at runtime and cached per query shape, instead of reflecting on every row.
- **Placeholder Auto-Conversion** -- `DbCommandSpec` accepts both positional (`{0}`, `{1}`) and named (`{Name}`) placeholders, converting them to provider-specific parameter syntax (`@p0`, `:p0`).
- **Provider Pattern** -- database-specific behavior (quoting, parameter prefixes, DDL, schema introspection) is isolated behind provider interfaces; routing is centralized in `DbDialectRegistry`. Hosts register the dialects they actually use; `Polhem.Db` does not auto-register any of them.
- **Nullable reference types** enabled (`<Nullable>enable</Nullable>`).

## Directory Structure

- `Ddl/` -- DDL string-generation contracts (`ICreateTableCommandBuilder`, `ITableAlterCommandBuilder`, …)
- `Dml/` -- DML string-generation contracts and builders (`IFormCommandBuilder`, `SelectCommandBuilder`,
  the clause builders, `SelectContext`, `TableSchemaCommandBuilder`); insert and update go through the
  `DbDataAdapter` (see [ADR-024](../../maintainers/adr/adr-024-dataform-save-dataadapter.md))
- `Schema/` -- the `TableSchema` comparison and upgrade flow (`TableSchemaBuilder`, `TableSchemaComparer`,
  `TableUpgradeOrchestrator`, `ITableSchemaProvider`); it emits no SQL itself. `Schema/Changes/` holds the change types
- `CacheNotify/` -- both directions of `st_cache_notify`: `ICacheNotifyService` (version bump) and
  `ICacheNotifyReader` (poll read)
- `Storage/` -- `DbDefineStorage` (definitions persisted in the database)
- `Providers/` -- `IDialectFactory`, with one sub-folder per provider (`SqlServer/`, `PostgreSql/`, `MySql/`,
  `Oracle/`, `Sqlite/`)
- `Manager/` -- `IDbConnectionManager`, `DbProviderRegistry`, `DbDialectRegistry`, `DbConnectionInfo`,
  `ConnectionStringTemplate`
- project root -- `DbAccess`, `DbCommandSpec`, `DbBatchSpec`, `DbConnectionScope` and their result and parameter types

The namespace layout follows three principles (see [ADR-008](../../maintainers/adr/adr-008-polhem-db-namespace-layout.md)):

1. **Syntax layer (`Polhem.Db.Ddl` / `Polhem.Db.Dml`) vs model layer (`Polhem.Db.Schema`)** — namespaces emitting SQL strings live under `Ddl` or `Dml`; namespaces operating on the `TableSchema` model live under `Schema`.
2. **Contracts by responsibility, implementations by provider** — abstract contracts go to the responsibility-named namespace; concrete per-provider implementations all live in `Polhem.Db.Providers.{X}` regardless of whether they implement DDL, DML, or schema-reading contracts.
3. **`IDialectFactory` is the only public type in `Polhem.Db.Providers`** — it is the factory binding contract, not a grab-bag for per-provider interfaces.
