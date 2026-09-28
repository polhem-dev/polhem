# ADR-034: ProgramSettings as the framework-wide type registry

[繁體中文](adr-034-progid-type-registry.zh-TW.md)

## Status

**Accepted (2026-08-04)**. The decision has been carried out. `ProgramSettings` has been reduced to a pure type
registry, the menu has been split out into `MenuSettings`, both BOs and Repositories are bound by progId, and all of it
is in use in `apps/Polhem.Northwind`.

This ADR records three long-lived decisions: **ProgramSettings as the framework-wide type registry** (including its
COM+ origin), **separating the menu from the registry**, and **the Repository 1:1 rule applies only to the form
track**.

## Origin: the COM+ registration model

The reference model for `ProgramSettings` is **the COM+ registration model**: a ProgID and its corresponding component
type are registered under a registry key, and a ProgID represents an independent function or program. The framework
continues this model: **the whole framework defines object types by ProgId**.

This positioning leads to three direct inferences:

1. **Every BO is an entry in the registry**, including `SystemBusinessObject`, `AuditLogBusinessObject` (named
   `LogBusinessObject` when this ADR was written) and any BO added in the future, not only form BOs.
2. **Repositories follow suit**: the same progId binds both its BO and its Repository.
3. **Customization overrides types per ProgId**, so the packaged product and the customizations are completely
   isolated and do not affect each other.

The COM+ registry only handles "ProgID → type", not "where this program sits in the menu". The second decision follows
from this.

## Decision 1: ProgramSettings only handles progId → type

`ProgramSettings.xml` is reduced to a single flat list of `ProgramItem`s, each carrying two assembly-qualified type
names, `BusinessObject` and `Repository`.

### Why flatten

The premise of a registry is that "the progId is a unique key". With a nested structure (`ProgramCategory` →
`ProgramItem`), that guarantee only exists within a category: when the same progId appears in two categories, **which
entry takes effect depends on the document order of the XML**. After flattening, the key mechanism of
`ProgramItemCollection` **is itself** the guarantee of global uniqueness, duplicates are rejected at load time, and a
lookup becomes a single-level key lookup. The concept of categories exists only in the menu definition, so the two
sides cannot get out of sync.

### Why even the reserved progIds are included

`System` and `AuditLog` were always progIds, yet they used to bypass the registry through hard-coded dispatch: the same
kind of thing, progIds, with two kinds of treatment, inconsistent with the origin. Once they were included, the
three-way branch in `JsonRpcExecutor` disappeared entirely, and SystemBO gained per-progId, per-tenant customization for
the first time (previously the only way was to replace the whole factory, which was process-wide and not per tenant).

The cost is a bootstrap cliff: the registry is the only source, and a missing entry cannot be resolved. The solution is
to **check the reserved names one by one at startup and add whichever is missing**, and the added entries **go into
memory first and take effect immediately**, with writing them to the file only a subsequent attempt at persistence;
a read-only deployment can therefore still start.

### The failure strategy for reserved progIds is stricter than for ordinary ones

| progId | Type cannot be loaded / base type does not match |
|--------|---------------------|
| Ordinary (such as `Order`) | Silently falls back to `FormBusinessObject` (BO axis) |
| Reserved (`System` / `AuditLog`) | **Throws directly**, with an additional per-progId constraint on the expected base type |

The reason is **what the failure looks like**. `FormBusinessObject` has no `Login`; if `System` used the silent
fallback, the symptom would be a JSON-RPC "method Login not found", which steers whoever is diagnosing it towards the
API layer or the client rather than the real cause (the registry). And since the constructor of `FormBusinessObject`
accepts a progId, construction would succeed, so the failure would surface late and look like something else.

"A built-in default as a safety net" was not adopted: the service would not be interrupted, but **a typo in a
customization would silently fail to take effect**, and customization is the main motivation for including them in the
registry.

> **Revision (2026-08-16): the failure strategies of the two axes have been converged; both always throw directly.**
> The table above, "ordinary progId → silent fallback", and the next section, "`Repository` is the opposite of
> `BusinessObject`", are **the decision at the time; the original text is kept as a record**. For the current behavior,
> see "Implementation evolution" at the end of this ADR.

### The failure strategy of `Repository` is the opposite of `BusinessObject`

When a `Repository` type cannot be loaded or does not derive from `DataFormRepository`, **it always throws directly,
with no fallback**, even for an ordinary progId.

A typo in the BO name of `Order` only degrades it to generic CRUD: annoying, not a disaster. A typo in the Repository
name, however, makes this program's reads and writes **run the generic SQL that the author deliberately replaced**.
A fallback does not avoid the failure; it only postpones it until the data is already wrong. Data access has no
harmless degraded mode.

### Server side only

The registry carries assembly-qualified type names, which are of no use on the client. Once the menu is split out, the
client no longer needs it, so remote `GetDefine` blocks it just as it blocks `SystemSettings` / `DatabaseSettings`:
type names do not go onto the wire. This is a direct side benefit of splitting out the menu, not an extra protection
added on purpose.

## Decision 2: Separate the menu from the registry

The menu becomes a separate `MenuSettings.xml`, where each `MenuEntry` corresponds to a progId.

### Why split

The two roles have **different readers, lifecycles and sensitivity**: only the server needs the registry (and it
contains assembly-qualified type names), while only the client needs the menu (and it needs pure presentation
attributes such as ordering, i18n and visibility). The COM+ registry, too, only handles ProgID → type.

Splitting also solves a concrete problem: when the client builds the menu, it walks all entries unconditionally
without filtering, so once `System` / `AuditLog` were included in the registry, they would have turned straight into
two menu items. Avoiding that would have required a visibility flag or a reserved category; after the split it is not
needed.

### Structural decisions

| Item | Decision | Reason |
|------|------|------|
| **Depth** | Recursive, multi-level, with no fixed number of levels | ERP menus with three or more levels are common; changing a public definition file afterwards is a breaking change |
| **Node types** | Two types, `MenuFolder` / `MenuEntry`, with the common base `MenuNodeBase` | Their attributes differ anyway. With separate types, "a function entry must not have child nodes" is **guaranteed by the type** and needs no runtime validation |
| **Leaf node name** | `MenuEntry`, **not `MenuItem`** | `MenuItem` is taken by almost every UI framework (WPF / WinForms / Avalonia / DevExpress). Definition types are consumed by **every** UI head, and the collision happens exactly in the code that "builds the menu from the definition", so the conflict is certain, not accidental |
| **Key** | A separate `Id`, with `ProgId` as a different attribute, and `Id` **unique across the whole tree** | Allows the same program to appear in several places in the menu (orders and return orders can share one BO), and lets nodes be referenced stably (deep links, recently used) |
| **Customization overlay** | **Replaces the whole file** | The menu is an overall layout; per-item overlays would produce hard-to-predict mixtures |

### Knock-on effects

**`ProgId` → menu node is 1:N.** When you need "which menu entry corresponds to the currently open form" (breadcrumbs,
menu highlighting), you must track it by `Id` rather than `ProgId`, and client navigation state should carry the `Id`.

**`Visible` is not a permission mechanism.** It is a design-time switch that is the same for every user; per-user
visibility is the responsibility of [permissions and authorization](adr-019-permission-authorization-model.md).
**The client currently does no permission filtering of the menu at all.**

## Decision 3: The Repository 1:1 rule applies only to the form track

"One progId, one BO, one Repository" **holds completely on the form track**, and does not hold on the framework track.
`IRepositoryFactory` therefore has two methods rather than one:

```csharp
T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository;
T Create<T>(Guid accessToken = default) where T : class;
```

This is not a compromise but an honest reflection of how the consumers are structured. **The BO axis can be fully
ProgId-based; the Repository axis cannot**, for two reasons:

**Gap one: the single progId `System` corresponds to several Repositories.** Under a single progId, SystemBO uses the
Repositories of several system tables, such as session / user / company / api-key, and
`CreateFormRepository(token, "System")` has no way of deciding which one to return. (This **does not affect** the BO
axis: one progId to one **BO** type holds completely.)

**Gap two: some consumers are not in a request context, and are not BOs either.**

| Consumer | Situation | Why it cannot supply a progId |
|--------|------|------------------|
| `ExpiredSessionCleanupService` | A `BackgroundService` that cleans up expired sessions on a timer | No request, no session, no token |
| `EmployeeContextResolver` | Resolves the employee context when a session is created / a company is entered | Runs **before** any progId request, and needs two Repositories within a single method |

`SessionCompanyBinder`, `DeploymentAuthorizationService` and `CacheDataSourceProvider` are of the same kind.
**These consumers are not BOs at all**; making the BO axis ProgId-based has no effect on them and cannot cover them.

### Why system Repositories keep per-table interfaces

| | Form track | Framework track |
|---|---|---|
| Shape of data access | One master table (+ details) per progId, driven by FormSchema | Several unrelated system tables |
| DB scope | Single, decided by `FormSchema.CategoryId` | Crosses scopes: session / user / api-key in common, department / employee / role-grant in company |
| Owner of the Repository | Private to that BO | **Shared infrastructure across consumers** |

Forcing them into a single `ISystemRepository` would produce a god interface spanning two DB scopes with dozens of
methods, and would force these non-BO consumers to depend on the interface of some BO.

### Both methods are generic

Adding a Repository therefore **does not require changing the interface**. That is exactly the problem this decision
set out to solve: the replaced `ISystemRepositoryFactory` grew a method for every system table added, and two
hand-written test fakes each implemented nine methods just to use one Repository.

### The dedicated interface pattern of the form track

`IXxxRepository : IDataFormRepository` (**extends, does not replace**). The CRUD of `FormBusinessObject` and
`SaveContext` / `DeleteContext` are all written against the base interface, so replacing it would still mean
implementing every member, just without saying so. The BO obtains it through its own interface, with no cast:

```csharp
private IOrderRepository Repository() => CreateFormRepository<IOrderRepository>();
```

For an example, see `apps/Polhem.Northwind/Polhem.Northwind.Server/Repositories/IOrderRepository.cs` and
`OrderRepository.cs` in the same directory.

## Implementation evolution

### 2026-08-16: The failure strategies of the two axes converge (always throw directly)

The original decision made the failure strategies of the two axes **deliberately opposite**: `BusinessObject` fell back
silently, and `Repository` threw directly. It is now **always throw directly**: when the `BusinessObject` type of an
ordinary progId cannot be loaded or its base type does not match, it throws `InvalidOperationException`, just like the
reserved names and the `Repository` axis.

**The only thing that changed is the one path of "a name was declared, but that name does not resolve to a usable
type".** When nothing is declared (the registry has no entry for the progId, or `BusinessObject` is left empty), it
still resolves to the framework default: an ordinary progId gets `FormBusinessObject`, and a reserved name gets that
axis's framework object. **That is not a failure**; self-registration of missing entries and "fill in `BusinessObject`
only for the progIds that need customization" both still hold.

The reasons were already written in the original decision; they just were not applied to ordinary progIds at the time:

- **All the fallback buys is "it looks like it is still running".** The constructor of `FormBusinessObject` accepts any
  progId and always constructs successfully, so the failure surfaces late and looks like it points to the API layer
  rather than the registry. That is the very same reasoning the original decision used to explain "why reserved names
  must throw". The only difference is that the symptom for a reserved name is "`Login` not found", while the symptom for
  an ordinary progId is "this program now behaves like generic CRUD", and the latter may only be discovered after a
  whole batch of transactions has been written.
- **Having the two axes opposite is itself a burden.** The same `ProgramItem`, two attributes, two failure semantics:
  maintainers and framework users alike have to remember which is which.
- **"One bad entry should not bring down the whole system" does not hold under multi-tenancy.** If a typo in a
  customization silently fails to take effect, the victim is that tenant and nobody will know, and customization is
  the main motivation for including them in the registry.

As a consequence, a resolution failure **does not enter the type cache** (when the factory of `GetOrAdd` throws,
nothing is written), so every call throws, and it does not silently pass from the second call on.
The `ILogger` constructor overload of `ProgramSettingsBoTypeResolver` is kept (existing callers still compile and
bind), but it no longer has a purpose: with the fallback gone, that degrade log has nothing to report.

### 2026-09-27: the resolver's cache and the progId casing

- **The `ILogger` constructor overload is gone.** The paragraph above says it was kept for existing callers; it was
  removed with the other ignored constructor parameters before 1.0, and `ProgramSettingsBoTypeResolver` now has only
  the `(IDefineAccess)` and `(IDefineAccess, ICustomizeDefineReader?)` constructors.
- **The type cache is no longer a plain `GetOrAdd`.** A resolution is cached only for a progId the registry names, so
  names that arrive from the wire cannot grow the cache, and each entry remembers the `ProgramSettings` instances it
  was resolved from and is used only while those are still the current instances; a reload makes every entry resolve
  again. Failures are still never cached. The plugin axis (`src/Polhem.Business/Form/PluginSettingsResolver.cs`)
  follows the same rule about the settings instances.
- **The business object receives the progId in its declared casing**: the `ProgramItem.ProgId` of the registry entry,
  or the reserved name's own spelling, whatever casing the caller used (`src/Polhem.Business/BusinessObjectFactory.cs`).
  Lookups keyed by the progId inside the business object therefore see one spelling per program.
- **The audit log business object is `AuditLogBusinessObject`.** The `LogBusinessObject` named in Decision 1 was renamed
  with the rest of the audit log axis (`src/Polhem.Business/AuditLog/`); it is still registered under the reserved
  progId `AuditLog`.

## Related

- [ADR-007](adr-007-convention-based-type-resolution.md): convention-based type resolution
- [ADR-016](adr-016-multitenant-customization-overlay.md): the multi-tenant customization overlay; this ADR's per-progId
  replacement reuses its mechanism
- [ADR-010](adr-010-logical-database-category.md): logical database categories, which decide the routing target of
  form-track Repositories
- [Definition Files Overview](../en/definition-files-overview.md): how to use the two definition files
