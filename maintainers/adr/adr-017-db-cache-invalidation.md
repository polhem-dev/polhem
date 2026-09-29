# ADR-017: Database cache dependency and invalidation (notify table + polling + convention-based dispatch)

## Status

Accepted (2026-06-01)

## Context

`Polhem.ObjectCaching` is an in-process cache ([ADR-009](adr-009-cache-implementation.md)). When it landed, there
were only two invalidation mechanisms:

1. `IDefineAccess.SaveX() → _cache.XXX.Remove()` — active invalidation, but **only effective in the process where the
   write happened**.
2. `ChangeMonitorFilePaths` (file-watch) of the XML definition files — covers only caches **sourced from files**, and
   depends on a shared file system.

Two gaps:

- **Caches loaded from the database** (`CompanyInfoCache`, future organization caches and so on) have no channel for
  "the source data changed → reload across processes / nodes".
- **Once definitions are stored in the database** (`DbDefineStorage` of [ADR-018](adr-018-db-define-storage.md)),
  there is no file to watch, and file-watch, the free cross-process invalidation signal, disappears with it.

Multi-node / cloud deployments should not depend on a shared file system. The requirement: when source data (possibly
across several tables) changes, and **the application judges the change to be meaningful**, the matching cache
(possibly in another process / on another node) can be notified to reset.

## Decision

Adopt "**a database notify table + polling on each node + idempotent version numbers + convention-based
dispatch**":

1. **The notify table `st_cache_notify`** (a single table with three columns, one row per logical cache key, UPSERT):
   - `cache_key` (PK) = a `"group:entity"` convention string (such as `"OrgInfo:0001"`, `"FormSchema:Employee"`, and
     `"SystemSettings:*"` for a single-object cache).
   - `cache_version` (bigint) = a monotonically increasing per-key version number.
   - `sys_update_time` = DB server time, the cursor for incremental fetching (the column name has the `sys_` prefix
     because it is the `SysFields.UpdateTime` system field; `cache_key` / `cache_version` are not system fields and
     have no prefix, see [database-naming-conventions](../../docs/en/database/database-naming-conventions.md)).

2. **The bump primitive `ICacheNotifyService.Touch(cacheKey, transaction, databaseType)`** (in `Polhem.Db`): within
   **the same transaction passed in by the caller**, a single UPSERT atomically increments `cache_version` and
   refreshes `sys_update_time`. Each dialect uses its native UPSERT (PG/SQLite `ON CONFLICT`, MySQL
   `ON DUPLICATE KEY`, SQL Server / Oracle `MERGE`).

3. **The poller `CacheNotifyPoller : BackgroundService`** (in `Polhem.Hosting`, a `PeriodicTimer` loop, 5 seconds
   by default): each node keeps an in-memory mirror `{cache_key → version}`; the first round only takes the baseline
   cursor and does not evict; after that it fetches rows with `sys_update_time >= highWater - margin`, evicts only
   when the `version` has grown, and advances highWater.

4. **Convention-based dispatch** (replacing a manual routing registry): each cache implements `IEvictableCache`
   (`CacheGroup` defaults to the name of the cached type), and `CacheContainerService` automatically builds a
   "group → cache" table and provides `ICacheContainer.TryEvict(cacheKey)`. The poller just calls
   `container.TryEvict(cacheKey)`, which dispatches by group to the matching cache's `Remove`.
   `Polhem.ObjectCaching` does not need to reference the DB layer.

   > ⚠️ **This item was superseded as the implementation evolved**: `IEvictableCache` and `ICacheContainer.TryEvict`
   > have both been removed. See "Implementation evolution" at the end.

### Core invariants (they override every implementation convenience)

1. **The bump must be committed in the same transaction as the data change.** Otherwise the notification is seen by
   the poller first while the data commit is not yet visible → the reload reads the old value and marks it fresh →
   **permanently stale**. `Touch` taking a `DbTransaction` explicitly guarantees this.

2. **Real changes are determined by `version`, not by time.** `sys_update_time` is only responsible for "fetching the
   increment cheaply"; the monotonically increasing `cache_version` + comparison against the mirror is responsible for
   "deciding idempotently". The delta uses `>=` + a safety `margin` to look back with overlap → nothing is missed; no
   eviction unless `version` has grown → nothing is duplicated. Together they are correct at edge cases such as
   "several changes within the same time tick" and "in a long transaction, update_time is earlier than when the
   commit becomes visible".

3. **Time always comes from the DB server clock, never from the app-side clock.** The write, highWater and threshold
   all share the same source (the DB clock), and no time zone conversion happens anywhere; multiple nodes each read
   the clock of the same DB, avoiding node clock skew. When the DB server is set to **UTC+0**, all values are UTC. The
   mechanism does not depend on the app host's time zone.

4. **Invalidation does not reload; it only makes the next read get the new value.** Keys nobody is reading are not
   reloaded for nothing; it reuses the existing lazy `CreateInstance` / the service layer's load-on-miss.

   > The original text was "evict (`Remove`) + lazy reload". The invariant itself (no active reload) still holds, but
   > the means of achieving it changed to publishing notify-key versions and letting existing entries expire lazily.
   > See "Implementation evolution" at the end.

5. **Adding a cache requires zero registration.** The convention group name = type name makes any cache added to
   `ICacheContainer` automatically invalidatable, with no routing table to maintain — scalable to the large number of
   DB-dependent caches in an ERP.

## Consequences

### Components and where they live

| Component | Location |
|------|------|
| `st_cache_notify.TableSchema.xml` | `tests/Define/TableSchema/common/` (the define directory for system tables) |
| `ICacheNotifyService` / `CacheNotifyService` | `Polhem.Db` (the lowest point shared with `DbDefineStorage`) |
| ~~`IEvictableCache` / `ICacheContainer.TryEvict`~~ (removed, see "Implementation evolution") | `Polhem.ObjectCaching` (the same layer as cache registration, no DB dependency) |
| `CacheNotifyPoller` / `CacheNotifyPollSession` | `Polhem.Hosting` (`Microsoft.Extensions.Hosting.Abstractions`) |
| `CacheNotifyOptions` | `Polhem.Definition.Settings` (`BackendConfiguration.CacheNotifyOptions`) |

### Configuration (`BackendConfiguration.CacheNotifyOptions`)

| Key | Default | Description |
|----|------|------|
| `Enabled` | `true` | Enables the poller; it can be turned off for a pure single-process, single-node setup (local writes invalidate immediately) |
| `IntervalSeconds` | `5` | Polling interval. In steady state each round is just one indexed query that mostly returns 0 rows, so the cost is negligible; this value is essentially the "cross-node invalidation delay" knob |
| `MarginSeconds` | `5` | Overlapping look-back margin for the increment, covering the residual edge of long transactions |
| `DatabaseId` | `common` | The database where the polled notify table lives |

> ⚠️ "Single machine" does not mean "single process": several app pools / processes on the same machine each have
> their own in-memory cache and still need the poller for cross-process invalidation. Disable it only when you are
> sure there is **a single process**.

### Resilience

Each round of `CacheNotifyPoller` is wrapped in `try/catch (DbException / InvalidOperationException)`; a transient DB
outage is only logged and does not break the loop (.NET's default `BackgroundServiceExceptionBehavior.StopHost` would
stop the whole Host on an unhandled exception).

## Alternatives considered (evaluated and rejected)

1. **Automatic bump by a DB trigger** — rejected: the dependency is "semantic", not "the whole table"; the source can
   span several tables, and often only a change to specific columns needs a reset; only application code can judge
   "whether this change is meaningful", and a trigger cannot express that granularity.

2. **`SqlDependency` (SQL Server) / `LISTEN`/`NOTIFY` (PostgreSQL)** — rejected: tied to a single dialect, which
   breaks Polhem's multi-DB support; and `SqlDependency` needs Service Broker, which is operationally complex.

3. **Redis pub/sub or a message bus** — rejected: does not fit the existing in-process single-node architecture and
   brings in an extra infrastructure dependency; overkill. A DB notify table naturally supports multiple nodes (each
   node polls the same table independently).

4. **Per-instance dynamic routing registration** — rejected: registering one by one does not scale to the large number
   of DB-dependent caches in an ERP, and it would bring routing state and a deregistration lifecycle along. Use
   convention-based dispatch (group = type name) + lazy evict instead; the caches themselves are the registry.

5. **Determining changes by timestamp (rather than version number)** — rejected: three pitfalls, clock skew, several
   updates in the same millisecond, and polling boundaries. A monotonic `version` compared per key is the most robust
   (time only serves as a cheap incremental cursor).

## Implementation evolution

An ADR records the design at the time of the decision. The following are later deviations in the implementation, for
readers comparing with the current code:

**`IEvictableCache` and `ICacheContainer.TryEvict` have been retired**
([`c45ff350`](https://github.com/jeff377/bee-library/commit/c45ff350)). The original design used a "group → cache"
registry to dispatch the poller's notifications to the matching cache to run `Remove`; the current approach instead has
the poller publish notify-key versions, and `CacheNotifyToken` of `MemoryCacheProvider` expires the entry on the next
read.

The core invariants at the decision level **all still hold** — bump in the same transaction, decide by version,
the DB clock as the single source, no active reload, zero registration for a new cache. Only the means of achieving
the last two changed: from "actively Remove" to "mark the version and let existing entries expire naturally", which
removes a routing table that needed maintenance and no longer requires every cache type to implement an extra
interface. The zero-registration convention now lives in `CacheGroup` (`src/Polhem.ObjectCaching/ObjectCache.cs`,
`src/Polhem.ObjectCaching/KeyObjectCache.cs`), which defaults to the type name and builds each entry's default
`ChangeNotifyKey` from it.

**The DB server time zone no longer affects correctness.** The original text of invariant 3 said "when the DB server
is set to UTC+0, all values are UTC"; the current `CacheNotifyService` instead takes the value from each dialect's
`GetDefaultValueExpression(FieldDbType.DateTime)`, and SQL Server, MySQL, PostgreSQL, Oracle and SQLite all return
UTC regardless of the server's time zone setting.

**Database-dependent caches read through on their own.** Invariant 4 mentions "the service layer's load-on-miss".
The database-dependent caches now load a missing entry themselves: `CreateInstance` calls `ICacheDataSourceProvider`
(for example `src/Polhem.ObjectCaching/Database/CompanyInfoCache.cs`), and the services in front of them only read
the cache. The invariant is unchanged: nothing is reloaded until it is read again.

## Related

- [ADR-009](adr-009-cache-implementation.md): the `Polhem.ObjectCaching` cache implementation
- Mechanism overview: [Caching](../../docs/en/guides/caching.md) (where this mechanism sits in the overall cache layer)
- [ADR-018](adr-018-db-define-storage.md): definitions stored in the database (one of the main consumers of this
  mechanism)
- Usage guide: [`development-cookbook.md`](../../docs/en/guides/development-cookbook.md) § Cross-Process Cache Invalidation
- Naming conventions: [`database-naming-conventions.md`](../../docs/en/database/database-naming-conventions.md)
