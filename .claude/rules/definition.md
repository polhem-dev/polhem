# Definition layer (Polhem.Definition) rules (core)

> Design rules for definition types (collection properties inherit a base class, field reference naming, the role
> of `Defaults/`) → `src/Polhem.Definition/CLAUDE.md` (loaded automatically when you touch that project).
> Criteria for adding BO interfaces → `src/Polhem.Business/CLAUDE.md` (those interfaces are in Business, not in
> Definition).

## Objects in the cache must not change after init

**This rule stays always loaded because its violators are every cache consumer.** BOs, Repositories, the cache
layer and every UI head can get a cached instance, not just the definition layer itself.

**Any instance obtained from the process-wide `ICacheContainer` is the same reference for every session and must not
be mutated at runtime after loading**, otherwise it leaks across sessions / races. The rule holds because **the cache
is shared**, regardless of where the data is loaded from, so it applies to both kinds:

| Kind | Load path | Invalidation path |
|------|---------|---------|
| **Definition file cache** (FormSchema, FormLayout, each `*Settings`…) | `IDefineAccess.GetX(...)` | `IDefineAccess.SaveX(...)` |
| **Database-dependent cache** (CompanyInfo, CompanyRolePermissions, DepartmentTree, CompanyAuditRules, ApiKeyInfo, ApiKeyGateState) | `ICacheDataSourceProvider` | Shared cache-notify table (**no** `SaveX`) |

`SessionInfo` is the exception (it is per-session by nature; the cache key is the access token). The one admitted
in-place mutation is `CacheDefineAccess.GetDatabaseSettings`, which decrypts passwords on the cached instance; its
WARNING remarks list the three conditions that make it safe, and they do not generalise. Do not copy the shape.
**The complete type list lives only in `docs/en/development-constraints.md`; this file does not copy it.**

- Need a per-session change → `cached.Clone()` first, then mutate. Not every definition type has `Clone()` today
  (the settings types and `LanguageResource` have none); if you need a per-session variant of one that lacks it,
  add `Clone()` to that type rather than mutating the cached instance.
- Persistent changes to definition data go through `IDefineAccess.SaveX(...)` (writes storage + invalidates the
  cache slot); database-dependent data goes through its repository + one cache-notify record. **Miss the notify and
  the whole process gets stale values.**
- **Database-dependent cache types deliberately do not provide `Clone()`**: they are read-only snapshots. If you need
  a variant, copy the values yourself; do not add `Clone()` as a back door.
- **Serializing a cached instance must stay side-effect free.** Empty collections are omitted by get-only
  `XSpecified` properties that decide from the value (`src/Polhem.Definition/CLAUDE.md`), and
  `CachedDefinitionSerializationTests` pins that serializing a cached definition does not change it. Do not bring
  back per-object serialize state: the removed `IObjectSerialize.SetSerializeState` wrote onto the **source**, and
  under concurrency it corrupted shared instances (hit in Bee.NET; fix commit
  [`aa843f71`](https://github.com/jeff377/bee-library/commit/aa843f71)).
- In code review, "directly mutating an instance taken from the cache" must be blocked.

For the full rules see `docs/en/development-constraints.md` § Cached Data Immutability After Init.
