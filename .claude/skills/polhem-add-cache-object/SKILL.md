---
name: polhem-add-cache-object
description: The full cross-file procedure for adding a framework cache object to polhem, in two kinds — the Define cache (source is a definition file, through IDefineAccess) and the Database-dependent cache (source is the DB, self-loaded through ICacheDataSourceProvider + invalidated by cache-notify). Includes the ObjectCache vs KeyObjectCache decision tree, keeping ICacheContainer + CacheContainerService in sync (missing one always gives CS0535), deferred resolution for the DI dependency cycle, and the cache-notify invalidation chain. Use when the user wants to "add a cache object", "add a cache", "cache some definition / database data", "KeyObjectCache / ObjectCache", "cache-notify invalidation", "permission checks / settings lookups with zero DB hits", or similar requests.
---

# polhem: add a cache object

polhem has **two kinds** of cache. They differ in source and invalidation mechanism, and their file chains differ too. Locate yours with the decision tree first, then follow the matching path. Every path touches `ICacheContainer` + `CacheContainerService` + two `CacheNotify` test stubs; miss any one of these three and the build fails with `CS0535` (building an individual project does not catch it; **it only shows up when `dotnet build Polhem.slnx` reproduces the CI strict build**).

> Templates to compare against (keep them open while reading code):
> - Define cache (single): `PermissionModelsCache` (`ObjectCache<PermissionModels>`)
> - Define cache (keyed): `FormSchemaCache` (`KeyObjectCache<FormSchema>`, by progId)
> - Database cache (keyed): `CompanyRolePermissionsCache` / `CompanyInfoCache` (`KeyObjectCache<T>`, by id, `CreateInstance` self-loads through `ICacheDataSourceProvider`)

## Decision tree

### First cut: what is the data source?

| Source | Kind | Folder | Invalidation mechanism | Template |
|------|------|--------|---------|------|
| **Definition file** (XML, through `IDefineAccess`) | **Define cache** | `Polhem.ObjectCaching/Define/` | `CreateInstance` self-loads; cleared on `SaveDefine` | `PermissionModelsCache` / `FormSchemaCache` |
| **Database** (runtime data) | **Database cache** | `Polhem.ObjectCaching/Database/` | `CreateInstance` self-loads through `ICacheDataSourceProvider`; cleared by cache-notify polling | `CompanyRolePermissionsCache` / `CompanyInfoCache` |

### Second cut: single or keyed? (applies to both kinds)

| Base class | Meaning | Examples |
|------|------|------|
| `ObjectCache<T>` | **Exactly one object for the whole thing** (no key) | `SystemSettings` / `DatabaseSettings` / `ProgramSettings` / `PermissionModels` |
| `KeyObjectCache<T>` | **Many instances, by key** (progId / company id / token) | `FormSchema` / `TableSchema` (by progId), `CompanyInfo` / `SessionInfo` / `CompanyRolePermissions` (by id) |

- `KeyObjectCache<T>` requires `T` to implement `IKeyObject` (`string GetKey()`).
- **In both kinds `CreateInstance(key)` self-loads**: the Define cache takes from `IDefineAccess`, the Database cache
  goes through `ICacheDataSourceProvider`. The difference is only the data source and the invalidation mechanism,
  not where loading happens.

> **Convention change since 2026-07-29**: Database caches used to be `CreateInstance => null` across the board, with the
> service doing "`Get` misses → repository → `Set` to backfill". That amounted to hand-writing a read-through in every
> service, and it bypassed the negative caching already built into the base class. All of them now self-load:
> `CompanyInfoCache` / `CompanyRolePermissionsCache` / `DepartmentTreeCache` all do.
> **Do not reuse the old `=> null` template for a new cache** (`SessionInfoCache` still returns `null`; that is a
> pending item waiting for persistence to be wired up, not a template).

---

## Path A: Define cache

The source is a definition file, accessed through `IDefineAccess`. Adding one (such as line A's `PermissionModels`) spans the **Definition + ObjectCaching** projects.

### File chain

| # | File | Convention |
|---|------|------|
| 1 | `src/Polhem.Definition/<Area>/<Name>.cs` | POCO definition class; the keyed variant implements `IKeyObject` |
| 2 | `src/Polhem.Definition/DefineType.cs` | Add the enum value `<Name>` |
| 3 | `src/Polhem.Definition/DefineTypeExtensions.cs` | Map `{ DefineType.<Name>, "<full type name>" }` |
| 4 | `src/Polhem.Definition/PathOptions.cs` | Add `Get<Name>FilePath()` |
| 5 | `src/Polhem.Definition/Storage/IDefineAccess.cs` | Add the DIM `<Name> Get<Name>() => (<Name>)GetDefine(DefineType.<Name>);` |
| 6 | `src/Polhem.ObjectCaching/Define/<Name>Cache.cs` | `: ObjectCache<T>` (single) or `: KeyObjectCache<T>` (keyed); `CreateInstance` loads from `DefineAccess` |
| 7 | `src/Polhem.ObjectCaching/CacheDefineAccess.cs` (server side), `src/Polhem.Api.Client/ClientDefineAccess.cs` (client side) | If both sides need to read the definition. (Rechecked 2026-08-06: the old `LocalDefineAccess` / `RemoteDefineAccess` no longer exist) |
| 8 | `src/Polhem.ObjectCaching/ICacheContainer.cs` | Add `<Name>Cache <Name> { get; }` |
| 9 | `src/Polhem.ObjectCaching/CacheContainerService.cs` | **Two places** (see the shared section below) |
| 10 | Two test stubs | **Must be added** (see the shared section below) |

- The DIM (default interface method) means existing `IDefineAccess` implementers need no changes.
- The type name mapped in `DefineTypeExtensions` must match the POCO's full name (used for deserialization).

---

## Path B: Database-dependent cache

The source is the database; it is loaded at runtime and invalidated by cache-notify. The goal is usually "**checks / lookups with zero DB hits**" (for example line B, where permission checks run entirely from the cache). Adding one (such as `CompanyRolePermissions`) spans **Definition + ObjectCaching + Repository + Hosting**.

### File chain

| # | File | Convention |
|---|------|------|
| 1 | `src/Polhem.Definition/<Area>/<Name>.cs` | POCO implementing `IKeyObject` (`GetKey() => <CacheKey>`); pure data + query methods, no DB |
| 2 | `src/Polhem.Definition/ICacheDataSourceProvider.cs` | Add a data-fetch method `<T>? Get<Name>(string key)`; it **must return a `Polhem.Definition` type** (see the dependency constraint below) |
| 3 | `src/Polhem.Business/Providers/CacheDataSourceProvider.cs` | Implement the method: get the repository from `IRepositoryFactory` and assemble the POCO |
| 4 | `src/Polhem.ObjectCaching/Database/<Name>Cache.cs` | `: KeyObjectCache<T>`; `CreateInstance` calls the provider (see the template) |
| 5 | `src/Polhem.Definition/<Area>/I<Name>Service.cs` | `Get(string key)` / `Remove(string key)`; the layer boundary that keeps upper layers from depending on `Polhem.ObjectCaching` |
| 6 | `src/Polhem.ObjectCaching/Services/<Name>Service.cs` | **Single-line delegation** to the cache; loading logic is not here |
| 7 | `src/Polhem.Repository.Abstractions/.../I<X>Repository.cs` + `src/Polhem.Repository/.../<X>Repository.cs` | The data source (DB reads); **also add the matching `Create<T>()` resolution to `IRepositoryFactory`** |
| 8 | `src/Polhem.ObjectCaching/ICacheContainer.cs` | Add `<Name>Cache <Name> { get; }` |
| 9 | `src/Polhem.ObjectCaching/CacheContainerService.cs` | **Two places** (see the shared section below); the ctor passes `dataSource` to the new cache |
| 10 | `src/Polhem.Hosting/PolhemFrameworkServiceCollectionExtensions.cs` | Register only the service; do **not** register repositories one by one (see below) |
| 11 | Two test stubs | **Must be added** (see the shared section below) |
| (12) | cache-notify bump point | The BO/Repository that writes the configuration calls `ICacheNotifyService.Touch(cacheKey, tx, dbType)` in the **same transaction** (see below) |

### Dependency constraint: why the data-fetch method returns a domain type

`ICacheDataSourceProvider` lives in `Polhem.Definition`, while `Polhem.Repository.Abstractions`
**depends back on** `Polhem.Definition` (`ICompanyRepository.GetById` returns `CompanyInfo`).
If the data-fetch method returned a repository type, `Polhem.Definition` would have to reference
`Polhem.Repository.Abstractions` → **a circular project reference that does not compile**.

So: the POCO goes in `Polhem.Definition`, the provider returns that POCO, and `Polhem.ObjectCaching` only sees the interface.

### Cache template (path B)

```csharp
namespace Polhem.ObjectCaching.Database
{
    public class <Name>Cache : KeyObjectCache<<T>>
    {
        private readonly Func<ICacheDataSourceProvider>? _dataSource;

        /// <param name="cachePrefix">Per-owner cache namespace.</param>
        public <Name>Cache(string cachePrefix = "") : this(null, cachePrefix) { }

        /// <remarks>
        /// WARNING: dataSource must stay a factory — see CompanyInfoCache for the cycle it avoids.
        /// </remarks>
        internal <Name>Cache(Func<ICacheDataSourceProvider>? dataSource, string cachePrefix)
            : base(cachePrefix)
        {
            _dataSource = dataSource;
        }

        protected override <T>? CreateInstance(string key)
            => _dataSource?.Invoke().Get<Name>(key);
    }
}
```

**Two shape constraints that are easy to trip over**:

- **The constructor that takes `dataSource` must be `internal`.** If it is public, `RS0026` / `RS0027` block it:
  two public overloads may not both have optional parameters, and the one with optional parameters must be the overload
  with the most parameters. `CacheContainerService` is in the same assembly, so `internal` is enough, and the public
  surface does not change.
- **`dataSource` must be a `Func<T>`, not an instance** (see the dependency cycle in the DI section below).

### Service template (path B)

The loading logic is already in the cache, so the service reduces to a single-line delegation on the layer boundary:

```csharp
public class <Name>Service : I<Name>Service
{
    private readonly ICacheContainer _cache;

    public <Name>Service(ICacheContainer cache)
        => _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    public <T>? Get(string key) => _cache.<Name>.Get(key);
    public void Remove(string key) => _cache.<Name>.Remove(key);
}
```

> The service looks like a pure facade, but its interface is defined in `Polhem.Definition`, so upper layers such as
> `Polhem.Business` / `Polhem.Repository` need not depend on `Polhem.ObjectCaching`. It is a layer boundary, not the
> 1-line wrapper that code-style wants removed.

### DI registration (path B, `PolhemFrameworkServiceCollectionExtensions.cs`)

```csharp
// service: takes only ICacheContainer
services.AddSingleton<I<Name>Service>(sp =>
    new <Name>Service(sp.GetRequiredService<ICacheContainer>()));
```

Do **not** add `services.AddSingleton<I<X>Repository>(...)` for a new repository. Consumers always obtain it on demand
through `IRepositoryFactory` (the same convention as `IRepositoryFactory.CreateFormRepository<T>` producing a form
repository by progId). Registering each one would turn every new system table into three edits: "factory method + DI
registration + consumer ctor parameter".

**The dependency cycle (you must understand this, or resolving `AddPolhemFramework` deadlocks)**:

```
ICacheContainer → ICacheDataSourceProvider → IRepositoryFactory → IDefineAccess → ICacheContainer
```

`CacheDefineAccess` takes `ICacheContainer`, which closes the cycle. The fix is for the container to **receive a deferred
factory as a method group**, resolved only on the first cache miss:

```csharp
services.AddSingleton<ICacheContainer>(sp =>
    new CacheContainerService(
        sp.GetRequiredService<IDefineStorage>(),
        sp.GetRequiredService<PathOptions>(),
        string.Empty,
        sp.GetRequiredService<ICacheDataSourceProvider>));   // NOTE: no parentheses, not resolved immediately
```

### cache-notify invalidation chain (path B)

The invalidation **infrastructure is already in place**, and a new cache hooks into it automatically. **You do not need
to register the cache anywhere**:

1. `KeyObjectCache<T>`'s `GetCacheKey(key)` = `cachePrefix + CacheGroup + ":" + key`; `CacheGroup` defaults to `typeof(T).Name`.
2. The poller polls the cache-notify table in common and writes the version numbers it observes into `CacheInfo.NotifyVersions`
   (`CacheNotifyPollSession` → `SetVersion(cacheKey, version)`). **The poller holds no cache references.**
3. Each cache entry records the current version number of its `ChangeNotifyKey` when it is created
   (`MemoryCacheProvider`) and compares on every later read; if the version changed, it is treated as invalidated and
   reloaded.

In other words, invalidation is **pulled by the entry itself** (pull), not pushed to the container (push). That is why a
new cache does not need to be registered in any array.

**The only thing you add is the bump point**: the BO/Repository that writes the database data calls `ICacheNotifyService.Touch("<CacheGroup>:<key>", transaction, dbType)` **within the same transaction**, so the next poller round clears it. If there is no management interface that writes the configuration, the bump point waits until that management BO is built (line B's `CompanyRolePermissions` is in this state).

---

## Shared section: two places (both paths need it)

### `ICacheContainer.cs` + `CacheContainerService.cs`

```csharp
// (1) Add the property declaration to ICacheContainer
<Name>Cache <Name> { get; }

// (2) Initialise it in the CacheContainerService ctor
//     Define cache: new <Name>Cache(storage, paths, CachePrefix)
//     Database cache: pass dataSource in — new <Name>Cache(dataSource, CachePrefix)
<Name> = new <Name>Cache(CachePrefix);

// (3) Add the matching public property to CacheContainerService (`/// <inheritdoc/>`)
public <Name>Cache <Name> { get; }
```

Missing the interface property or the implementing property → `CS0535` (interface not fully implemented); missing the ctor initialisation → NRE.

> **Rechecked 2026-08-06: the "third place: eviction array" described earlier no longer exists.** `CacheContainerService`
> once maintained an `IEvictableCache[]` for cache-notify routing (`TryEvict` / `_evictableByGroup`). The current
> mechanism is that **the poller only publishes the observed version numbers to `CacheInfo.NotifyVersions`, and cache
> entries with a matching `ChangeNotifyKey` invalidate themselves**; a new cache does not need to be registered in any array.
>
> The same recheck also confirmed: **the CacheNotify tests in `tests/Polhem.Hosting.UnitTests` no longer implement
> `ICacheContainer`** (the poller no longer holds cache references), so the "two stubs that must be added" do not exist.
> The only implementation of `ICacheContainer` in the whole repository is `CacheContainerService`.

---

## Tests

| Kind | What to test | How to test |
|------|--------|--------|
| Define cache | Access returns the correct object; invalidated after `SaveDefine` | Get `IDefineAccess.Get<Name>()` through `PolhemTestFixture` |
| Database cache POCO | Pure query logic (such as OR-merging multiple roles) | Pure unit test: build the POCO from synthetic data and assert directly (**no DB needed**) |
| Database service | Load on cache miss + short-circuit on cache hit | Fake repository + fake source service; verify that two `Get` calls load only once |
| Repository | DB round-trip | `[DbFact]` 5 DBs, `IClassFixture<SharedDbFixture>` |

Put check / lookup logic in **the POCO's methods** where possible (such as `CompanyRolePermissions.GetAllowed`), so the core logic can be unit tested with synthetic data and is not tied to a DB.

## Common pitfalls

1. **Missing the property declaration on `CacheContainerService` → CS0535**: once `ICacheContainer` gains a property, it needs an implementation.
2. **Building individual projects only, without the slnx**: the stubs' CS0535 only appears in `dotnet build tests/Polhem.Hosting.UnitTests`; **always run `dotnet build Polhem.slnx -c Release` to reproduce the CI strict build**.
3. **Changing only one of the two places in `CacheContainerService`**: ctor initialisation missing → NRE; property declaration missing → CS0535.
4. **Reusing the old `CreateInstance => null` template** (the convention before 2026-07-29): Database caches now
   **should self-load** through `ICacheDataSourceProvider`. Returning `null` means hand-writing the read-through into the
   service and bypassing the negative caching already built into the base class. To clarify along the way: "checks with
   zero DB hits" refers to **cache hits** costing zero DB, while a miss has to touch the DB whether the service or
   `CreateInstance` fetches it; self-loading does not break that design.
5. **Writing `dataSource` as an instance instead of a `Func<T>`**: `AddPolhemFramework` deadlocks when resolving `ICacheContainer`
   (see the dependency cycle in path B's DI section). Pass a method group at the DI registration, without parentheses.
6. **Adding an individual DI registration for a new repository**: always obtain it through `IRepositoryFactory`; do not register them one by one.
7. **Choosing single vs keyed wrongly**: one object for the whole thing uses `ObjectCache<T>`; many instances use `KeyObjectCache<T>` with `T : IKeyObject`.
8. **`IDE0028` collection initialisation**: `new List<string>()` as a field / local initialiser must be changed to the collection expression `[]` (net10 + strict build).
9. **A POCO mutated after it is put in the cache**: cache contents are shared and must not be mutated (see `.claude/rules/definition.md`); for per-session changes, `Clone()` first.
10. **cache-notify bump point not in the same transaction**: `Touch` must be in the same transaction as the configuration write; otherwise the write succeeds but the notify is missing, or the notify lands but the write rolls back.

## Full checklist

**Locate**:
- [ ] Source: definition file (path A) or database (path B)
- [ ] Shape: single (`ObjectCache<T>`) or keyed (`KeyObjectCache<T>` + `IKeyObject`)

**Path A (Define cache)**:
- [ ] POCO definition class + `DefineType` enum value + `DefineTypeExtensions` mapping + `PathOptions.Get<Name>FilePath`
- [ ] `IDefineAccess` DIM `Get<Name>()`
- [ ] `Define/<Name>Cache.cs` (`CreateInstance` self-loads)

**Path B (Database cache)**:
- [ ] POCO implementing `IKeyObject` (in `Polhem.Definition`); check / lookup logic in POCO methods
- [ ] Add the data-fetch method to `ICacheDataSourceProvider` (**returns a `Polhem.Definition` type**)
- [ ] Implement it in `CacheDataSourceProvider` (get the repository through `IRepositoryFactory`, assemble the POCO)
- [ ] `Database/<Name>Cache.cs` (`CreateInstance` calls the provider; the constructor that takes `dataSource` is `internal`)
- [ ] `I<Name>Service` + `<Name>Service` (**single-line delegation**, no loading logic)
- [ ] Repository abstraction + implementation + the matching `Create<T>()` resolution on `IRepositoryFactory`
- [ ] DI registers only the service (does **not** register repositories one by one)
- [ ] The `CacheContainerService` ctor passes `dataSource` to the new cache
- [ ] cache-notify bump point (`Touch` in the same transaction as the configuration write; deferred if there is no management interface)

**Shared (both paths)**:
- [ ] Add the property to `ICacheContainer`
- [ ] The two places in `CacheContainerService` (ctor initialisation + property declaration)
- [ ] Matching tests (pure POCO unit / service fake / repository `[DbFact]`)
- [ ] **`dotnet build Polhem.slnx -c Release` 0w/0e**, then run the tests

## Reference files (keep them open while reading code)

| Purpose | File |
|------|------|
| Define cache (single) template | `src/Polhem.ObjectCaching/Define/PermissionModelsCache.cs` |
| Define cache (keyed) template | `src/Polhem.ObjectCaching/Define/FormSchemaCache.cs` |
| Database cache template | `src/Polhem.ObjectCaching/Database/CompanyInfoCache.cs` (includes the WARNING comment on the `Func<T>` dependency cycle) |
| Database cache (needs to resolve the source DB) | `src/Polhem.ObjectCaching/Database/DepartmentTreeCache.cs` / `CompanyRolePermissionsCache.cs` |
| Data-fetch seam | `src/Polhem.Definition/ICacheDataSourceProvider.cs` + `src/Polhem.Business/Providers/CacheDataSourceProvider.cs` |
| Cache base classes | `src/Polhem.ObjectCaching/ObjectCache.cs` / `KeyObjectCache.cs` |
| Service template | `src/Polhem.ObjectCaching/Services/DepartmentTreeService.cs` (single-line delegation) |
| POCO + check logic template | `src/Polhem.Definition/Identity/CompanyRolePermissions.cs` (`GetAllowed` / `GetKey`) |
| The two sync points | `src/Polhem.ObjectCaching/CacheContainerService.cs` (ctor initialisation / property declaration) |
| ICacheContainer | `src/Polhem.ObjectCaching/ICacheContainer.cs` |
| DI registration | `src/Polhem.Hosting/PolhemFrameworkServiceCollectionExtensions.cs` (service; `ICacheContainer` receives a deferred factory) |
| POCO pure unit test template | `tests/Polhem.Definition.UnitTests/Identity/CompanyRolePermissionsTests.cs` |
