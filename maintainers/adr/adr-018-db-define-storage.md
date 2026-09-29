# ADR-018: Storing definitions in the database (a single `st_define` table of XML blobs)

## Status

Accepted (2026-06-01)

## Context

Definition data (`FormSchema`, `TableSchema`, `FormLayout`, `Language`, `DbCategorySettings`, `ProgramSettings` and
so on) was originally stored as "one XML file per definition" (`FileDefineStorage` over `PathOptions`), and
cross-process invalidation relied on `ChangeMonitorFilePaths` (file watching).

Multi-node and cloud deployments should not depend on a shared file system. A backend that "stores definitions in
the database" is needed, with the notification table of [ADR-017](adr-017-db-cache-invalidation.md) replacing file
watching as the cross-process invalidation channel.

`BackendComponents.DefineStorage` is already a configurable type name (`AddPolhemFramework` builds `IDefineStorage`
from it), so switching between `FileDefineStorage` and a DB backend is a configuration change and does not touch
callers.

## Decision

Adopt "**a single table, every storable type, one row per definition, an XML blob**": `DbDefineStorage :
IDefineStorage` (which also implements `ICustomizeDefineReader`), placed in `Polhem.Db`.

### The notification/storage table `st_define`

| Column | Type | Description |
|----|------|------|
| `define_type` | varchar | Discriminator = **the name of the cached type** (`typeof(T).Name`) |
| `customize_id` | varchar | The base layer uses the sentinel `"*"`; anything else = a tenant customization code |
| `define_key` | varchar | Identity within the type (`"*"` for singletons) |
| `content` | Text/CLOB | The XML-serialized content of the definition |
| `sys_update_time` | datetime | DB server time (a system column, `SysFields.UpdateTime`) |

`PK(define_type, customize_id, define_key)` plus an index on `define_type` (for "list every definition of a type").

### Five key design points

1. **Bootstrap split**: `SystemSettings` / `DatabaseSettings` **stay as files** (settings required at startup;
   `DatabaseSettings` is "how to connect to the DB" itself, and cannot possibly live in the DB it describes). The
   other storable types go into the DB.

2. **`define_type` = `typeof(T).Name`** → the storage discriminator, the cache group dispatched by convention in
   [ADR-017](adr-017-db-cache-invalidation.md), and the bump group are all the same thing (note that the cached type
   for Language is `LanguageResource`, so `define_type` = `"LanguageResource"`). `SaveX` calls
   `Touch("<typeof(T).Name>:<define_key>")` in the same transaction → invalidation is routed to the matching cache
   automatically.

3. **`define_key` matches the cache's Remove key**: composite keys use **`.`** (`TableSchema` → `"common.st_user"`,
   `Language` → `"zh-TW.common"`), consistent with the internal key encoding of `TableSchemaCache` /
   `LanguageResourceCache`; otherwise cross-node invalidation cannot find the key. (The original text said
   `TryEvict(group, entity)`; that API was removed by the change described in ADR-017's "Implementation evolution".)

4. **Sentinel `"*"`**: `customize_id` (base) and the singleton `define_key` use a non-empty `"*"`. Because **Oracle
   treats `''` as `NULL` and a PK column cannot be NULL** (the String→nullable fix in the dialect layer cannot save a
   PK column), an empty string cannot be used; `"*"` also matches the existing `"Type:*"` singleton convention.

5. **DI activation breaks the construction cycle with deferred resolution**: the `DbDefineStorage(IServiceProvider)`
   constructor resolves `IDbConnectionManager` / `ICacheNotifyService` **only on the first read or write**.
   Otherwise `DbDefineStorage → IDbConnectionManager → IDatabaseSettingsProvider → IDefineAccess →
   IDefineStorage(=DbDefineStorage)` forms a cycle at construction time. The bootstrap split already breaks the
   semantic cycle (`DatabaseSettingsCache` reads the file directly and does not go through `IDefineStorage`), and
   deferred resolution then breaks the construction cycle of the DI object graph.

### The customization overlay lives in the same table

Tenant customization overrides (`Language` / `FormLayout` / `ProgramSettings`) go into the same `st_define` table
through the `customize_id` column (`"*"` = base); the reads of `ICustomizeDefineReader` query by that
`customize_id` and return `null` when a row is missing (the same read-only semantics as the file version). This is
more uniform than the file model's two directories.

## Consequences

### Into the DB / stays as files

| DefineType | Destination |
|------------|------|
| `SystemSettings` / `DatabaseSettings` | **Stays as files** (bootstrap) |
| `DbCategorySettings` / `ProgramSettings` / `TableSchema` / `FormSchema` / `FormLayout` / `Language` | Into the DB |

### Invalidation integration (following ADR-017)

Each definition cache's `GetPolicy()` is already storage-aware: `FileDefineStorage` sets file watching,
`DbDefineStorage` does not (it relies on the notification table instead). After `DbDefineStorage.SaveX` bumps in the
same transaction, cross-node invalidation is completed automatically by the poller; **no routing needs to be
registered**. (The convention-based `IEvictableCache` dispatch mentioned in the original text has since been changed
to notify-key versions plus lazy expiry; see [ADR-017](adr-017-db-cache-invalidation.md) "Implementation
evolution".)

### Prerequisite for enabling it (a deployment task, not code)

Switching to DB storage (`BackendComponents.DefineStorage = Polhem.Db.Storage.DbDefineStorage`) first requires
**migrating the existing definition data into `st_define`**; otherwise reads such as `GetDbCategorySettings()` hit an
empty table and throw. This data migration is a deployment task.

## Alternatives considered (evaluated and rejected)

1. **One table per type / normalized columns** (splitting FormSchema into columns): rejected. Definitions are read
   often, written rarely and sit behind a cache; blob granularity = file granularity is the simplest. Normalization
   only adds schema and mapping complexity.

2. **MessagePack instead of XML**: rejected. Definitions are already XML-serializable; keeping `XmlCodec` stays
   round-trip consistent with the file version, is human-readable inside the DB and adds no new dependency; size is
   not a bottleneck (there is a cache).

3. **A separate `st_define_customize` table for customizations**: rejected. It goes against the original intent of
   "one unified table"; the single `customize_id` column is enough to tell them apart, and the query
   `WHERE customize_id IN ('*', @tenant)` fetches both layers at once.

4. **Wiring `CreateInstance` to `ICacheDataSourceProvider`**: rejected. DB loading is already handled by
   load-on-miss in the service layer (`CompanyInfoService` and others), and the cache stays "dumb storage"; wiring a
   provider would duplicate work and push a DB dependency into `Polhem.ObjectCaching`.

5. **Splitting the composite key into two columns `key1`/`key2`**: rejected. It would be inconsistent with the single
   string cache key of [ADR-017](adr-017-db-cache-invalidation.md); a single `define_key` matches the notification
   key and needs no schema change if the key becomes more composite in the future.

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing
with the current code:

- **2026-07-01 to 2026-08-06: More types in `st_define`.** `CurrencySettings` and `UnitSettings` (2026-07-01),
  `MenuSettings` (2026-08-04, split out of `ProgramSettings`) and `PluginSettings` (2026-08-06) were added as
  definition types and are stored in `st_define` like the rest; the "Into the DB" table above lists the types of the
  time. The current list is the set of `Get`/`Save` members of `IDefineStorage`, implemented in
  `src/Polhem.Db/Storage/DbDefineStorage.cs`.
- **2026-08-04 to 2026-08-06: The customization reader grew, and one customization is writable.**
  `ICustomizeDefineReader` (`src/Polhem.Definition/Storage/ICustomizeDefineReader.cs`) reads the customization of
  `Language`, `ProgramSettings`, `MenuSettings`, `FormLayout` and `PluginSettings`. Plugin bindings are no longer
  read-only: `DbDefineStorage` also implements `ICustomizeDefineWriter`, whose `SaveCustomizePluginSettings` stores a
  tenant's plugin bindings.
- **2026-07-29: Alternative 4 became the current approach.** Database-dependent caches load a missing entry
  themselves: `CreateInstance` calls `ICacheDataSourceProvider` (for example
  `src/Polhem.ObjectCaching/Database/CompanyInfoCache.cs`), and services such as `CompanyInfoService` only read the
  cache. `ICacheDataSourceProvider` is an interface in `Polhem.Definition` implemented in `Polhem.Business`
  (`src/Polhem.Business/Providers/CacheDataSourceProvider.cs`), so `Polhem.ObjectCaching` still has no project
  reference to the data layer. See also [ADR-017](adr-017-db-cache-invalidation.md) "Implementation evolution".

## Related documents

- [ADR-017](adr-017-db-cache-invalidation.md): the database cache invalidation mechanism (the invalidation channel of
  this storage)
- [ADR-016](adr-016-multitenant-customization-overlay.md): the multi-tenant customization overlay (where the
  semantics of `customize_id` come from)
- [ADR-009](adr-009-cache-implementation.md): the cache implementation (the layering of dumb cache storage vs
  service loading)
- Naming conventions: [`database-naming-conventions.md`](../../docs/en/database-naming-conventions.md)
