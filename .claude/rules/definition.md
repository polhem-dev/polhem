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

`SessionInfo` is the exception (it is per-session by nature; the cache key is the access token).
**The complete type list lives only in `docs/en/development-constraints.md`; this file does not copy it.**

- Need a per-session change → `cached.Clone()` first, then mutate.
- Persistent changes to definition data go through `IDefineAccess.SaveX(...)` (writes storage + invalidates the
  cache slot); database-dependent data goes through its repository + one cache-notify record. **Miss the notify and
  the whole process gets stale values.**
- When adding a new Define-family class, remember to add `Clone()`. **Database-dependent cache types deliberately do
  not provide `Clone()`**: they are read-only snapshots. If you need a variant, copy the values yourself; do not add
  `Clone()` as a back door.
- **`XmlCodec.Serialize(cachedInstance)` cannot be used as a free deep clone.** Through
  `IObjectSerialize.SetSerializeState` it mutates state on the **source**, and under concurrency state-conditional
  logic such as `IsSerializeEmpty` goes wrong (already hit; fix commit
  [`aa843f71`](https://github.com/jeff377/bee-library/commit/aa843f71)).
- In code review, "directly mutating an instance taken from the cache" or using `XmlCodec.Serialize(cached)` as a
  clone must be blocked.

For the full rules see `docs/en/development-constraints.md` § Cached Data Immutability After Init.
