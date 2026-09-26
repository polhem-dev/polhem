# ADR-016: Multi-tenant customization overlay (two read-only layers stacked)

[繁體中文](adr-016-multitenant-customization-overlay.zh-TW.md)

## Status

Accepted (2026-05-31)

## Context

Polhem's tenant concept originally reached only the **database layer** (`SessionInfo.CompanyId` + `EnterCompany` /
`LeaveCompany` of [ADR-012](adr-012-session-company-context.md), with row-level isolation by `sys_company_rowid`),
while **definition files were shared by the whole system**: every `GetXxxFilePath()` was derived from the single
`PathOptions.DefinePath` root directory.

In a multi-tenant deployment, different tenants need different customizations:

- **Language**: the same key shows different text / enums for different tenants.
- **FormLayout**: different tenants have different screen layouts (rearranging / hiding existing fields).
- **Custom BO (ProgramSettings)**: different tenants bind different `FormBusinessObject` subclasses.

A layer of **tenant-specific, read-only customization overrides** needs to be stacked on top of the base package
definitions, driven by a "customization code" (`CustomizeId`).

## Core invariant (it overrides every implementation convenience)

**Any cached data on the server — whether base (package) or cust (customization) — is always read-only at runtime
after initialization and is never mutated.**

- The base cache is a single instance shared by all sessions: mutating it contaminates **every** tenant and session.
- A customization cache is an instance "shared by all sessions of the same `CustomizeId`": mutating it contaminates
  the other sessions of **that tenant**.
- The first iron rule of multi-tenancy: **the data of any customization code must not affect other tenants**.

This invariant is the root cause of every choice that follows: it is precisely because the cache must not be mutated
that "merging into a single object" was rejected.

## Decision

Introduce a **per-`CustomizeId` read-only customization overlay** that forms, together with the base package layer,
**two independent layers, each read-only**; stacking happens only on the consumer side at lookup time.

### Key points

1. **Two read-only layers stacked, objects never merged**

   - Base layer = all the existing caches: a single process-wide copy, read-only, zero mutation.
   - Override layer = a separate per-`CustomizeId` cache whose backing is replaced by `CustomizeOnlyStorage`: it
     **strictly reads only** `{CustomizePath}/{customizeId}/...`, and returns `null` when there is no matching file
     (no fallback, no mixing in of base). It is physically isolated by `CachePrefix=customizeId`.
   - Stacking happens on the consumer side at lookup granularity: **a merged object is never produced, and base is
     never mutated**.

2. **Three kinds of customization, each with its own stacking granularity**

   | Kind | Stacking granularity | Lookup semantics |
   |------|---------|---------|
   | **Language** | Key level | The cust resource contains the key → use the cust value; otherwise the base value (enums likewise) |
   | **ProgramSettings** | progId level → property level (see the revision below) | The cust settings hit the progId → choose property by property; otherwise base |
   | **FormLayout** | Whole file, one or the other | The cust file exists → return the cust object; otherwise the base object |

   `FormSchema` / `TableSchema` / `SystemSettings` / `DatabaseSettings` / `DbCategorySettings` **always go through
   `DefinePath` and never enter the customization branch**.

   > **Revision (2026-08-05): the granularity of `ProgramSettings` is refined from "progId level, whole-entry
   > replacement" to "progId level → property level".** When this ADR was decided, `ProgramItem` carried only one
   > binding, `BusinessObject`, so whole-entry replacement and property-level inheritance behaved the same. Once
   > `ProgramItem.Repository` was added, the two diverged: whole-entry replacement would make a customization that
   > "only swaps the BO" also clear the package's dedicated Repository, and since **an empty string is a legitimate
   > "use the framework default" rather than an error**, that loss would never be reported. The current semantics are
   > that a customization entry overrides only the properties it names, and properties left empty keep the base
   > value; to deliberately fall back to the framework's generic type, name that type explicitly. This revision
   > **does not affect** the core decision "do not merge into a single object" (see "Why merge was rejected" below):
   > the combined result is a new instance produced at lookup time, and neither layer's cached objects are mutated.

   > **Revision (2026-08-06): the customization scope expands from three kinds to five.** When this ADR was decided
   > there were only three kinds: Language / ProgramSettings / FormLayout. Later `ProgramItem` gained the
   > `Repository` binding ([ADR-034](adr-034-progid-type-registry.md)), making "custom BO" and "custom Repository"
   > two independent axes; and `PluginSettings` was added as a fifth kind
   > ([ADR-035](adr-035-business-logic-plugin.md)). **The current five kinds and their granularity**:
   >
   > | Kind | Stacking granularity | Notes |
   > |------|---------|------|
   > | **Language** | Key level | An enum is replaced as a whole set |
   > | **FormLayout** | Whole file, one or the other | |
   > | **Custom BO** | progId level → property level | `ProgramItem.BusinessObject` |
   > | **Custom Repository** | progId level → property level | `ProgramItem.Repository`, independent of the BO |
   > | **Business plugin** | progId level, **additive** | The only additive granularity; the package chain comes first and the customization chain after, and **a customization cannot disable a package plugin** |
   >
   > `PluginSettings` is also **the first writable customization definition** (a `LocalOnly` maintenance API), while
   > the rest of the customization layer stays read-only — which makes the statements in the section "Customization
   > definitions are read, not written" below hold only for the other four kinds.
   >
   > **Revision (2026-08-13): the current state is six kinds; the table above leaves out `MenuSettings`.** The
   > revision above classifies by "what you want to change", and menu customization had been in the overlay from the
   > start but was never listed: `ICustomizeDefineReader` has `GetCustomizeMenuSettings`, and
   > `SystemBusinessObject.GetDefineCore` explicitly passes `GetCurrentCustomizeId()` for `DefineType.MenuSettings`.
   >
   > | Kind | Stacking granularity | Notes |
   > |------|---------|------|
   > | **MenuSettings** | Whole file, one or the other | For the same reason as `FormLayout`: a menu is one overall arrangement, and merging node by node would produce groupings and orderings nobody chose |
   >
   > ⚠️ **When citing this section, state which way of counting you mean.** Counted by **definition file** there are
   > **five** (`Language` / `FormLayout` / `ProgramSettings` / `PluginSettings` / `MenuSettings`), and that number is
   > stable; counted by **"what you want to change"** it depends on how finely you cut (the two bindings of
   > `ProgramItem` are independent of each other, and the text and the option sets of `Language` are two more
   > granularities), so the "five kinds" of the table above and the "six kinds" of this revision both count intents,
   > not files. The authoritative sources are the XML doc of `CustomizeOverlay` and `CustomizeOnlyPathOptions` (the
   > latter states that the overlay serves only those five types).
   >
   > The stacking algorithm was later centralized in `CustomizeOverlay` (`Polhem.Definition.Customization`), a pure
   > decision component shared by server and client (no storage / session / DI dependencies), so the two ends no
   > longer derive it separately. For the full usage description see
   > [Tenant customization](../en/customization.md).

3. **`CustomizeId` is an independent code, not the same as `CompanyId`**

   - Several companies can share the same customization (shared across a group, separating standard and customized
     editions); Company → `CustomizeId` is many-to-one.
   - The carrier follows the [ADR-012](adr-012-session-company-context.md) pattern: **`CompanyInfo` stores the
     mapping, `SessionInfo` stores the current value**. `CustomizeId` is loaded by `CompanyRepository` from the
     `st_company.customize_id` column; `EnterCompany` writes `SessionInfo.CustomizeId`, and `LeaveCompany` /
     `Logout` clear it (in step with `CompanyId`).

4. **`CustomizeId` is passed explicitly as a parameter (not ambient)**

   - The consumers (`LanguageService` / `ProgramSettingsBoTypeResolver` / `CacheDefineAccess`) are stateless
     singletons; the caller that holds the `AccessToken` resolves it from `SessionInfo.CustomizeId` and **passes it
     in explicitly**.
   - The new stacking methods are added to `ILanguageService` / `IBoTypeResolver` / `IDefineAccess` as **default
     interface methods** that delegate to base by default, with zero ripple to existing implementations.

5. **Short-circuiting is backward compatibility**

   - The first step at every stacking point is `string.IsNullOrEmpty(customizeId)`; if it is empty (single tenant,
     before login, no `EnterCompany`, or `CustomizePath` not set), it goes straight to base and **never enters the
     customization layer at all** (no reader / provider call, no file probing).
   - `ICustomizeDefineReader` performs the same guard again internally as a second line of defense.
   - Result: when customization is not enabled, the whole chain is **bit-for-bit identical** to the current state.

## Rationale

### Why merge (combining into a single object) was rejected

Merging base + cust into a new object invites two paths that violate the core invariant: "read base → rewrite it in
place" contaminates the cache shared by all tenants, or an extra merged cache has to be configured (yet another piece
of mutable state to maintain). With "two read-only layers, choose one at lookup time", the base cache is never mutated
from start to finish.

### Why `CustomizeId` is passed explicitly rather than ambient (AsyncLocal)

| Mechanism | Worst failure mode | Safety |
|------|------------|--------|
| **Explicit parameter** | A caller forgets to pass it → degrades to pure base (**the customization is not applied**) | ✅ fail-safe |
| Ambient AsyncLocal | A missed reset at the end of a request / thread reuse → **the previous request's `CustomizeId` is carried over** | ❌ fail-dangerous = cross-tenant spill |

A security-critical feature should choose fail-safe: when explicit passing fails, only "the customization does not
take effect" (a safe degradation); when ambient fails, it is exactly the cross-tenant contamination the plan was most
concerned about. Explicit passing also matches the framework's existing stateless design of "the caller passes lang
explicitly" and introduces no implicit global state.

### Why `FormSchema` customization is excluded

`FormSchema` is the definition hub that drives the UI / DB schema / validation rules at the same time; diverging it
per tenant would split the DB structure. `FormLayout` customization (which can only rearrange / hide existing fields,
while the field set stays locked by the shared `FormSchema`) is already enough for "different screens for different
tenants", and the constraint actually strengthens "`FormSchema` is the hub".

### Why the override cache uses `CachePrefix=customizeId`

Reusing the existing `CachePrefix` mechanism of `CacheContainerService` achieves per-tenant physical isolation with no
new cache infrastructure; and the cache classes of the base package layer and `FileDefineStorage` **do not change by a
single line**.

## Alternatives considered (evaluated and rejected)

1. **Merge base + cust into a single object**
   - Reason for rejection: it invites rewriting the base cache and contaminating every tenant (see above).

2. **Pass `CustomizeId` through ambient AsyncLocal**
   - Reason for rejection: fail-dangerous, cross-tenant spill; it introduces implicit global state, contrary to the
     framework's stateless design.

3. **Customize `FormSchema`**
   - Reason for rejection: diverging the definition hub per tenant would split the DB schema / validation rules.
     Excluded permanently, not postponed.

4. **`CustomizeId` equals `CompanyId`**
   - Reason for rejection: it cannot express "several companies share one customization" (shared across a group,
     separating standard and customized editions).

5. **The override container shares the base storage and falls back**
   - Reason for rejection: the override cache would mix in base content, breaking the clear semantics of "the
     override layer holds only pure customization content", and making it hard to return null cleanly when there is
     no file.

## Consequences

### Overlay architecture

```text
Consumer (holds the AccessToken)
   │ resolves customizeId from SessionInfo.CustomizeId and passes it in explicitly
   ↓
LanguageService / ProgramSettingsBoTypeResolver / CacheDefineAccess
   │ customizeId empty → short-circuit to pure base
   │ not empty ↓
ICustomizeDefineReader.GetCustomizeXxx(customizeId, ...)
   ↓
ICacheContainerProvider.For(customizeId)   → per-customizeId read-only container (CachePrefix=customizeId)
   ↓
CustomizeOnlyStorage (strictly reads only {CustomizePath}/{customizeId}/..., no file → null)
```

### Directory structure

```text
{DefinePath}/FormSchema/{progId}.FormSchema.xml          ← shared by all tenants, not customized
{DefinePath}/Language/{lang}/{ns}.Language.xml           ← standard edition
{DefinePath}/FormLayout/{layoutId}.FormLayout.xml
{DefinePath}/ProgramSettings.xml
{CustomizePath}/{customizeId}/Language/{lang}/{ns}.Language.xml   ← customization differences (3 kinds only)
{CustomizePath}/{customizeId}/FormLayout/{layoutId}.FormLayout.xml
{CustomizePath}/{customizeId}/ProgramSettings.xml
```

### Public API changes

| Scope | Change |
|------|------|
| `PathOptions` | Adds `CustomizePath`; `GetProgramSettings/FormLayout/Language FilePath` become `virtual` |
| `CustomizeOnlyPathOptions` / `CustomizeOnlyStorage` | **New** (`Polhem.Definition`): serve only the three kinds, return null when there is no file, include path traversal protection |
| `ICustomizeDefineReader` | **New** (`Polhem.Definition.Storage`): three `GetCustomizeXxx(customizeId, ...)` methods |
| `ICacheContainerProvider` / `CacheContainerProvider` / `CustomizeDefineReader` | **New** (`Polhem.ObjectCaching`) |
| `ILanguageService` / `IBoTypeResolver` / `IDefineAccess` | Add `customizeId`-aware default interface methods |
| `CompanyInfo` / `SessionInfo` | Add the `CustomizeId` field |
| `ClientDefineAccess` / `ClientInfo` | Add `ClearCache()` / `ResetDefineCache()` (clear the client cache when switching tenants) |
| `st_company` | Adds the `customize_id` column |

## Trade-offs

### The client cache has to be flushed when switching tenants

Customization stacking is done on the server (according to the session's `CustomizeId`), so what the client gets is
already the stacked result. But the local cache of `ClientDefineAccess` is keyed by progId / layoutId / namespace, so
when the same connection switches company through `EnterCompany` (changing the `CustomizeId`), it would return the
previous tenant's stacked result. The countermeasure: `ClientDefineAccess.ClearCache()` +
`ClientInfo.ResetDefineCache()`, called after switching company.

### Oracle `''=NULL` and the nullability of `customize_id`

In the standard edition `customize_id` is normally empty. Oracle treats `''` as `NULL`, so "a String that is NOT NULL
and normally empty" cannot hold under a fresh Oracle CREATE. How it is handled: the Oracle dialect always creates
String columns as nullable, and the read side normalizes with `ValueUtilities.CStr(null)→""`, so upper-layer C# always
sees an empty string and never null; the other dialects keep `NOT NULL` + `DEFAULT ''`.

### The "enumerate every program" scenario gets no union

ProgId-level lookup only answers "given a progId, take one of the two"; it does not directly give the union base ∪
cust. If building the menu needs a union, the consumer builds a temporary view at that moment (not cached, mutating
neither layer). BO type resolution does not need a union, so the framework does not implement it.

### Customization definitions are read, not written

The framework is only responsible for **reading** customization files; they are produced by external tools /
deployment processes. `SaveXxx` does not go through a customization write path. Writing is left to a later plan (it
needs to consider the write path, cache invalidation, and the interaction with the read-only invariant).

> **Revision (2026-08-06): this section now applies only to four kinds: Language / FormLayout / custom BO / custom
> Repository.** `PluginSettings` is now open for writing (`ICustomizeDefineWriter` + a `LocalOnly` maintenance API,
> which validates each type before writing and evicts that tenant's cache slot right after writing).
> `CustomizeOnlyStorage` itself stays fully read-only — writes instead land directly through
> `CustomizeOnlyPathOptions` from the writer, the two share the same path source, and the class's read-only promise
> does not have to be broken for a single exception. See [ADR-035](adr-035-business-logic-plugin.md) for details.

## Affected areas

| Scope | Impact |
|------|------|
| `src/Polhem.Definition` | New `CustomizeOnlyPathOptions` / `CustomizeOnlyStorage` / `ICustomizeDefineReader`; `PathOptions` gains `CustomizePath`; `ILanguageService` / `IDefineAccess` gain overloads; `CompanyInfo` / `SessionInfo` gain `CustomizeId` |
| `src/Polhem.ObjectCaching` | New `ICacheContainerProvider` / `CacheContainerProvider` / `CustomizeDefineReader`; `CacheDefineAccess` gains overlay overloads |
| `src/Polhem.Business` | `ProgramSettingsBoTypeResolver` overlay (the type cache switches to a `(customizeId, progId)` composite key); `IBoTypeResolver` gains an overload; `SystemBusinessObject` EnterCompany / LeaveCompany / Logout set / clear `CustomizeId` |
| `src/Polhem.Repository` | `CompanyRepository.GetById` loads `customize_id` |
| `src/Polhem.Hosting` | DI registration of the provider / reader; the reader is injected into the three consumers |
| `src/Polhem.Api.Client` / `src/Polhem.UI.Core` | `ClientDefineAccess.ClearCache()` / `ClientInfo.ResetDefineCache()` |
| Tests | Override layer, consumer-side stacking, cross-tenant isolation, short-circuiting, backward compatibility, EnterCompany→CustomizeId, clearing the cache on client switch |

## Related

- [ADR-012: Session company context model](adr-012-session-company-context.md) — the `CustomizeId` carrier follows its
  `CompanyInfo` / `SessionInfo` pattern
- [ADR-009: Cache implementation](adr-009-cache-implementation.md) — the override layer reuses its `CachePrefix`
  isolation mechanism
