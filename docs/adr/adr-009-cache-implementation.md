# ADR-009: Polhem.ObjectCaching adopts Microsoft.Extensions.Caching.Memory + IChangeToken

[繁體中文](adr-009-cache-implementation.zh-TW.md)

## Status

Accepted (2026-04-28)

## Context

`Polhem.ObjectCaching` originally used `System.Runtime.Caching.MemoryCache` (NuGet package `System.Runtime.Caching`)
as its underlying storage:

1. Microsoft's official documentation states explicitly that `System.Runtime.Caching` is not recommended for new
   projects, which should use `Microsoft.Extensions.Caching.Memory` instead.
2. On Linux, `MemoryCache.Default` occasionally throws `NotImplementedException` because of factors such as
   performance counter initialization. This caused a `CacheInfo.Provider` static initialization race in this
   project's CI several times (with a downstream chain reaction: `SystemBusinessObject` failing wholesale with NREs,
   the typical flaky pattern that goes green on a rerun).
3. The `CacheItemPolicy` object is heavy: the write path carries performance counter overhead, and expiration scans
   run frequently.
4. The `ChangeMonitor` system is old and incompatible with modern .NET DI and the `IChangeToken` system.

Polhem is a new, pure .NET 10 framework, unreleased and with no compatibility baggage, so it is well placed to migrate
fully to modern packages in one go and drop `System.Runtime.Caching` completely.

## Decision

Adopt the design principle "**drop `System.Runtime.Caching` entirely and switch the internals completely to
`Microsoft.Extensions.Caching.Memory` + `IChangeToken`; keep the public API (`CacheItemPolicy`, `ICacheProvider`) as
Polhem's own abstractions, with the internal implementation mapped onto the new underlying layer**".

### Three key points

1. **The public layer keeps Polhem's own abstractions**

   - `CacheItemPolicy`: kept as Polhem's own definition, exposing three fields, `AbsoluteExpiration` /
     `SlidingExpiration` / `ChangeMonitorFilePaths` (`ChangeNotifyKey` was added later with ADR-017's DB cache
     invalidation, so there are now four)
   - `ICacheProvider`: kept as the storage abstraction, so that it can still be swapped for implementations such as
     Redis or `IDistributedCache` in the future
   - `Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions` and `IMemoryCache` are not exposed to callers
     directly

2. **The internal implementation is rewritten on `Microsoft.Extensions.Caching.Memory`**

   - `MemoryCacheProvider` holds a `Microsoft.Extensions.Caching.Memory.MemoryCache` internally (it no longer uses
     `MemoryCache.Default` and does not depend on `System.Runtime.Caching`)
   - `Set` maps `CacheItemPolicy` onto `MemoryCacheEntryOptions` internally:
     - `AbsoluteExpiration` / `SlidingExpiration` map directly
     - `ChangeMonitorFilePaths` now uses `PhysicalFileProvider.Watch(name)` to get an `IChangeToken`, which is added
       through `MemoryCacheEntryOptions.AddExpirationToken(...)`
   - `MemoryCacheProvider` implements `IDisposable` and releases the `MemoryCache` and every `PhysicalFileProvider`
     created for `ChangeMonitorFilePaths`

3. **Slimmer interface and modernized key normalization**

   - `ICacheProvider` drops the methods with no production caller: `Trim(int percent)` and `GetAllKeys()`
   - `Remove` changes from `object Remove(string key)` to `void Remove(string key)` (zero production callers use the
     return value; the new underlying layer is void as well)
   - `Get` is annotated as `object? Get(string key)`, stating explicitly that a cache miss returns null
   - `MemoryCacheProvider.GetCacheKey` now uses `key.ToLowerInvariant()`:
     - From the culture-dependent `ToUpper` to a culture-invariant form, avoiding locale-specific behavior such as the
       Turkish I and the German ß
     - Aligned with the modern .NET / HTTP / REST convention (lowercase)
   - The `CacheItemPolicy.ChangeMonitorDbKeys` property is removed (its companion `DbChangeMonitor` was already
     removed in [`8099d03`](https://github.com/jeff377/bee-library/commit/8099d03); it was only ever used by tests, and
     no real monitor received it)

## Outcome

### Dependencies after adoption

| Package | Action |
|---------|--------|
| `System.Runtime.Caching` | **Removed** |
| `Microsoft.Extensions.Caching.Memory` | **Added** (10.x) |
| `Microsoft.Extensions.FileProviders.Physical` | **Added** (10.x, provides `PhysicalFileProvider`) |

`Microsoft.Extensions.Primitives` (which contains `IChangeToken`) is a transitive dependency of the other two and does
not need to be added explicitly.

### External API changes

| Target | Change |
|--------|--------|
| `ICacheProvider.Trim` | Removed |
| `ICacheProvider.GetAllKeys` | Removed |
| `ICacheProvider.Remove` | Return type changed to `void` |
| `ICacheProvider.Get` | Changed to `object? Get(string key)` |
| `CacheItemPolicy.ChangeMonitorDbKeys` | Removed |
| `MemoryCacheProvider` | Stays a public class; now implements `IDisposable` |
| `CacheFunc.CreateCachePolicy` | Removed (internal method) |
| `CacheItemPolicy.AbsoluteExpiration` / `SlidingExpiration` / `ChangeMonitorFilePaths` | Unchanged |
| `CacheInfo.Provider` and other classes | Unchanged |

No separate version bump (stays on `4.0.x`); listing the mapping table in the release notes is enough.

### Default configuration

- `new MemoryCache(new MemoryCacheOptions())`: all options at their defaults
  - `SizeLimit` is not set (no limit): the number of objects Polhem caches (SystemSettings, FormSchemas and so on) is
    in the dozens, so size-based eviction is not needed
  - `ExpirationScanFrequency` keeps the default of 1 minute, which is enough outside high-throughput scenarios
- `PhysicalFileProvider` is not shared up front: each cache entry creates its own
  `new PhysicalFileProvider(directory)` for each file it watches
  - OS file handles are cheap and there are dozens of entries; a sharing mechanism would need a
    `static Dictionary<directory, PhysicalFileProvider>` + lifetime management + reference counting, adding
    complexity and a risk of resource leaks
  - Do it only if handle exhaustion or an explosion in numbers actually happens (it can be added later in a separate
    commit)
- `MemoryCacheProvider` gets no `Reset()` / `Clear()` API (YAGNI; no caller currently needs it)

## Alternatives considered (evaluated and rejected)

1. **Keep `System.Runtime.Caching` and work around the CI flakiness** (for example, change `MemoryCache.Default` to
   `new MemoryCache(name)`)
   - Reason for rejection: workable in the short term, but in the long term it still carries the baggage of an
     outdated package and stays disconnected from the modern .NET ecosystem; since this is a new framework, do it
     properly in one go

2. **Expose `Microsoft.Extensions.Caching.Memory.IMemoryCache` directly and remove the `ICacheProvider` abstraction**
   - Reason for rejection: loses room to switch to Redis or `IDistributedCache` in the future; keeping the abstraction
     costs little and gains a lot

3. **Maintain both a `System.Runtime.Caching` provider and a `Microsoft.Extensions.Caching.Memory` provider**
   - Reason for rejection: introduces the maintenance burden of a dual stack; goes against Polhem's pure .NET 10
     design direction

4. **Bring in DI directly (`IServiceCollection.AddMemoryCache()`)**
   - Reason for rejection: inconsistent with Polhem's existing service-locator pattern (`CacheInfo.Provider`); it is a
     separate refactoring topic

## Later extension: negative caching (2026-05-15)

`KeyObjectCache<T>.Get` originally did **not write** a "`CreateInstance` returned null" result to the cache, so the
next request for the same key went through to the data source (file IO / DB query). An attacker sending invalid
keys, a program bug using a wrong key, or an upper layer forgetting a precondition check would all amplify this cache
penetration problem.

`KeyObjectCache` introduces negative caching:

### Design points

- **The `MissMarker` sentinel**: `KeyObjectCacheSentinel.MissMarker` is a single process-wide `object` instance (a
  non-generic static, to avoid [S2743]: the waste of every closed type creating its own sentinel). When
  `CreateInstance` returns null after a cache miss, this sentinel is written; when `Get` hits the sentinel it returns
  null directly and does not call `CreateInstance` again
- **The `GetNegativePolicy(key)` virtual method**: defaults to a 5-minute **absolute** expiration (shorter than the
  positive cache's 20-minute sliding expiration; absolute expiration ensures that an attacker poking the same key
  repeatedly does not extend the TTL). A subclass overrides it to return null to disable negative caching
- **`Set` / `Remove` behavior unchanged**: writing a positive value to the same cacheKey, or `Remove`, naturally
  overwrites / clears the sentinel, with no special handling needed

### `SessionInfoCache` is the exception and disables it

`SessionInfoCache.CreateInstance` always returns null (a session enters the cache only through the `Set` path of
`Login` and is not rebuilt from a backing store). With negative caching enabled, anonymous traffic could flood the
cache with marker entries using arbitrary access tokens, with no real protective value: a session lookup for an
unknown token already returns null quickly. `SessionInfoCache` overrides `GetNegativePolicy` to return null and
disable it.

The other `KeyObjectCache<T>` subclasses (`FormSchemaCache` / `TableSchemaCache` / `FormLayoutCache`) read files in
`CreateInstance`, and repeatedly reading invalid file names is a real amplification risk, so they **keep the default**
negative caching.

### External API changes

| Target | Change |
|--------|--------|
| `KeyObjectCache<T>.GetNegativePolicy(string key)` | **New** virtual method, returns a 5-minute absolute TTL by default; returning null disables negative caching |
| `KeyObjectCacheSentinel` (internal) | **New** static class holding the single `MissMarker` instance |
| `KeyObjectCache<T>.Get(string key)` | Behavior change: on a cache miss + `CreateInstance` returning null, `GetNegativePolicy` decides whether the sentinel is written; a sentinel hit returns null directly |
| `Set` / `Remove` | Behavior unchanged |

### Known effects

- A second lookup of a key known not to exist no longer triggers `CreateInstance`, and by default returns null
  consistently for 5 minutes
- Existing tests that rely on the side effect of "`CreateInstance` is called every time" will fail; when this landed,
  the related expectations in `KeyObjectCacheTests` were fixed along the way

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **2026-09-27: file watching.** `ChangeMonitorFilePaths` no longer goes through `PhysicalFileProvider.Watch`, and
  `Microsoft.Extensions.FileProviders.Physical` is not referenced. `MemoryCacheProvider` adds its own lazy
  `FileModificationToken`, which compares the file's last write time with a baseline taken before the load, and
  re-reads it at most once per second per entry (`FileWriteTime.RecheckInterval`). "No customization file" answers
  are likewise remembered for a second and dropped on save. See `src/Polhem.ObjectCaching/Providers/MemoryCacheProvider.cs`.
- **2026-09-27: fills that race an invalidation.** A cache fill reads the invalidation state before loading and
  discards what it stored if that state changed in the meantime (`src/Polhem.ObjectCaching/CacheInvalidation.cs`),
  for the definition file caches and the database-dependent caches alike.
- **2026-09-27: sessions are rebuilt, and misses of caller-supplied keys are capped.** `SessionInfoCache.CreateInstance`
  no longer always returns null: it rebuilds a session from `st_session` through
  `ICacheDataSourceProvider.GetSessionInfo`. It therefore uses negative caching again, and so does `ApiKeyCache`: both
  keep a miss for one minute in a capped set (`BoundedMissMarkers`) instead of in the shared provider, because their
  keys come from the caller. The other `KeyObjectCache<T>` subclasses (the definition caches `FormSchemaCache`,
  `TableSchemaCache`, `FormLayoutCache` and `LanguageResourceCache`, and the database-dependent `CompanyInfoCache`,
  `CompanyRolePermissionsCache`, `CompanyAuditRulesCache`, `DepartmentTreeCache` and `ApiKeyGateCache`) keep the
  default. The current list of caches is in [Caching](../en/caching.md).
- **2026-09-27: cache size.** The provider still sets no `SizeLimit`, but the premise under "Default configuration"
  (a few dozen cached objects) no longer holds: the session and API key caches hold an entry per active token or key,
  so their size follows traffic.
- **2026-09-27: dependency injection.** Alternative 4 was rejected for consistency with the service locator
  (`CacheInfo.Provider`). [ADR-011](adr-011-di-replaces-service-locator.md) later moved the framework to DI; the cache
  provider itself is still the process-wide `CacheInfo.Provider`, chosen through `BackendComponents.CacheProvider`.

## Related documents

- Mechanism overview: [Caching](../en/caching.md) (read path, invalidation signals, list of caches)
- Package README: [`src/Polhem.ObjectCaching/README.md`](../../src/Polhem.ObjectCaching/README.md)
- Related commits: [`8099d03`](https://github.com/jeff377/bee-library/commit/8099d03) (removed the `DbChangeMonitor`
  placeholder), [`715c159e`](https://github.com/jeff377/bee-library/commit/715c159e) (negative caching)
