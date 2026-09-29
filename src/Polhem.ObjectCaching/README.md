# Polhem.ObjectCaching

> Runtime caching layer for definition data, database-dependent data and session information, plus the services built on it.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: Infrastructure (caching)
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/architecture/dependency-map.md). Not enumerated here — the csproj files are the authority, and a prose copy in every package README drifts with nothing to catch it. These did: `Polhem.Hosting` was missing as a dependent from four of them for months after it was extracted.
- Consumed by application code.

## Target Framework

- `net10.0` -- access to modern runtime APIs and performance improvements

## Key Features

### Definition Caching (`Define/`)

- One cache per definition type: `SystemSettingsCache`, `DatabaseSettingsCache`, `DbCategorySettingsCache`,
  `ProgramSettingsCache`, `MenuSettingsCache`, `PluginSettingsCache`, `TableSchemaCache`, `FormSchemaCache`,
  `FormLayoutCache`, `LanguageResourceCache` and the other `*SettingsCache` types
- `CacheDefineAccess` -- the `IDefineAccess` implementation that reads through these caches (with the optional
  customization overlay) and invalidates the slot on save

### Database-Dependent Caching (`Database/`)

- `SessionInfoCache` -- authenticated session data, keyed by access token
- `CompanyInfoCache`, `CompanyRolePermissionsCache`, `DepartmentTreeCache`, `CompanyAuditRulesCache`,
  `ApiKeyCache`, `ApiKeyGateCache` -- data loaded through `ICacheDataSourceProvider` and invalidated through the
  shared cache-notify table

### Services (`Services/`)

- `SessionInfoService`, `CompanyInfoService`, `CompanyAuthorizationService`, `RolePermissionService`,
  `DepartmentTreeService`, `AuditRuleService`, `ApiKeyValidator`, `ApiKeyGateStateProvider` -- the
  Definition-layer service interfaces implemented over the caches

### Cache Infrastructure

- `ObjectCache<T>` -- single-object cache base class with template method hooks (`GetPolicy`, `GetKey`, `CreateInstance`)
- `KeyObjectCache<T>` -- keyed cache base class for objects identified by a string key
- `ICacheProvider` / `MemoryCacheProvider` -- pluggable cache storage provider
- `CacheItemPolicy` / `CacheTimeKind` -- expiration configuration. The default policy is a sliding expiration set
  in `GetPolicy`, which a cache overrides to change it
- `CacheInfo` -- process-wide static access to the active `ICacheProvider` and to the cache-notify versions
  (`ICacheNotifyVersionStore`) the poller publishes

### Tenant Customization Overlay

- `ICacheContainerProvider` / `CacheContainerProvider` -- lazily builds a per-`CustomizeId` read-only override cache container (`CachePrefix=customizeId`, backed by `CustomizeOnlyStorage`), reusing the existing cache classes unchanged
- `CustomizeDefineReader` -- `ICustomizeDefineReader` implementation that reads Language / FormLayout / ProgramSettings / MenuSettings / PluginSettings from the per-tenant override containers; a missing override returns `null` (see [ADR-016](../../maintainers/adr/adr-016-multitenant-customization-overlay.md))
- `CustomizeDefineWriter` -- the matching `ICustomizeDefineWriter`

## Key Public APIs

| Class / Interface | Purpose |
|-------------------|---------|
| `ICacheContainer` | DI-injected contract exposing every cache instance (`SystemSettingsCache`, `FormSchemaCache`, `SessionInfoCache`, etc.) |
| `CacheContainerService` | `ICacheContainer` implementation, registered as a Singleton by `AddPolhemFramework` |
| `ObjectCache<T>` | Single-object cache base class |
| `KeyObjectCache<T>` | Keyed cache base class |
| `ICacheProvider` | Cache storage provider interface |
| `CacheDefineAccess` | `IDefineAccess` implementation that reads definitions through the cache (with optional customization overlay) |
| `ICacheContainerProvider` / `CacheContainerProvider` | Per-`CustomizeId` override cache container provider |
| `CustomizeDefineReader` | Tenant customization-override reader (`ICustomizeDefineReader`) |
| `CacheItemPolicy` | Expiration and eviction configuration |
| `CacheInfo` | Static access to the cache provider and the cache-notify versions |

## Design Conventions

- **DI injection** -- consumers ctor-inject `ICacheContainer`; the `CacheContainerService` implementation is registered as a Singleton by `AddPolhemFramework`, so callers reach the individual cache classes through the injected contract rather than a static facade.
- **Template Method Pattern** -- `ObjectCache<T>` subclasses override `GetPolicy`, `GetKey`, and `CreateInstance` to define caching behavior without modifying the base retrieval logic.
- **Key normalization** -- `MemoryCacheProvider` lowercases keys with `ToLowerInvariant()`, so lookups ignore case.
- **Cached instances are shared** -- every session receives the same instance, so it is not modified after loading; a caller that needs a per-session variant clones it. See [Development Constraints](../../docs/en/architecture/development-constraints.md).
- **Backing store** -- `MemoryCacheProvider` wraps `Microsoft.Extensions.Caching.Memory.IMemoryCache`; the public `CacheItemPolicy` is mapped internally to `MemoryCacheEntryOptions`.
- **Nullable reference types** enabled (`<Nullable>enable</Nullable>`).

## Directory Structure

- `Define/` -- the definition caches
- `Database/` -- the session and database-dependent caches
- `Services/` -- the services implemented over the caches
- `Providers/` -- `ICacheProvider`, `MemoryCacheProvider`
- project root -- `ICacheContainer`, `CacheContainerService`, `ObjectCache<T>`, `KeyObjectCache<T>`, `CacheItemPolicy`,
  `CacheTimeKind`, `CacheInfo`, `CacheDefineAccess`, `CacheContainerProvider`, `CustomizeDefineReader`, `CustomizeDefineWriter`
