# ADR-019: Permission authorization model (two-layer enforcement + record scope)

## Status

Accepted (2026-06-05)

## Context

Polhem originally had only **authentication** (`Login` / `EnterCompany` from
[ADR-012](adr-012-session-company-context.md)) and the encryption/login gate at the API layer
(`ApiAccessControlAttribute`). What an ERP really needs is **authorization**: "who may perform which action on which
business entity", and "on which rows". These are different concerns:

- **The action axis (may it be done)**: a purchasing clerk may not delete purchase orders.
- **The data scope axis (on which rows)**: a purchasing clerk may only view / change the purchase orders of their own
  department.

Hard constraints of the design:

1. **Multi-tenant, per-company**: roles and grants are configured within a company, so they are necessarily
   per-company (isolated the same way as `st_employee`).
2. **Zero DB for permission checks**: every API request checks permissions, so it cannot query the DB every time; it
   must go through an in-memory cache plus a session snapshot.
3. **Decoupled from forms** (aligned with Odoo's `ir.model.access` / `ir.rule`): permissions are bound to the
   "business entity (model)", not to the form (progId) and not to tables / columns; one model can be consumed by
   several progIds, so one grant takes effect for all three functions.
4. **The security boundary is in the backend**: the payload sent by the frontend is not trusted, and the
   authorization decision cannot depend on values provided by the client.
5. **Declarable and verifiable**: scope uses a menu of business-semantic choices (named strategies) instead of
   hand-written predicates, so the correctness of the bindings can be checked in full by a single pure function
   (`PermissionBindingValidator`, called by the host).

## Decision

Adopt a permission model with **two axes and three stages**; the data flow is:

```
Line A (definition layer)     declares "which model a function needs, and the scope role of each field"
Line B layer 1 (action)       checks (model, action): may it be done (zero-DB)
Line B layer 2 (scope)        filters / guards rows by the scope of (model, action) (zero-DB)
```

### Line A: the definition layer (declarable, verifiable, zero enforcement)

- **`PermissionModels`** (a single-file registry, `DefineType.PermissionModels`): each `PermissionModel` (`ModelId` =
  the business entity in PascalCase, such as `PurchaseOrder`) declares its set of `PermissionRule`s; each rule =
  `(PermissionAction, ScopeStrategy)`. `ModelId` is deliberately distinct from a form's progId: **bind to the model,
  not the form**.
- **`FormSchema.PermissionModelId`**: the form declares which primary model it consumes (the BO method layer already
  has the FormSchema at hand → enforcement is most direct there).
- **`FormField.ScopeRole`** (`None` / `Owner` / `Dept`): marks "which column is the owner / department". **Scope
  strategies stay purely semantic** (`Own`→the `Owner` column, `Dept`/`DeptAndSub`→the `Dept` column); column names
  stay in the FormSchema, and the model is decoupled from tables / columns.
- **Scope is master-table only**: `ScopeRole` is only on the master table; a detail table marked with `ScopeRole` is
  caught by `PermissionBindingValidator`. **That validator is a validation API the host calls itself; the framework
  does not run it automatically** (Line A is positioned as "declarable, verifiable, zero enforcement" to begin with).

### Line B layer 1: the action gate (zero-DB)

- Data model (in each company DB, `st_` framework-level tables): `st_role` / `st_role_grant` / `st_user_role`.
- **Per-company permission cache** (`CompanyRolePermissions`, modeled on `CompanyInfoCache`, DB source + cache-notify
  invalidation): user→role / role→grant are loaded in full, and **permission checks run entirely in memory**.
- `EnterCompany` takes the user's role list for this company from the cache and **snapshots it into
  `SessionInfo.Roles`**; after that, `CompanyAuthorizationService.Can(token, model, action)` is zero-DB throughout.
- Integration point = **the gate at the `FormBusinessObject` method layer**: `GetList`/`GetData`→Read, `Save`→per-row
  RowState (Added→Create / Modified→Update / Deleted→Delete), `Delete`→Delete. With several roles, the masks are
  combined as an **OR union** (capabilities accumulate).

### Line B layer 2: record scope (zero-DB at query time)

- **Scope is per action** (`st_role_grant` = one row per `(role, model, action)` carrying a `scope`): this implements
  "being able to view does not mean being able to change". The Read scope can be `Dept` while the Update scope is
  `Own`. `Inherit` → take `PermissionModel.Rules[action].Scope` (the model default).
- **Named strategies** (`ScopeStrategy`): `All` (unrestricted) / `Own` / `Dept` / `DeptAndSub` / `Inherit`.
- **`IScopeResolver`**: resolves by `(model, action, session, FormSchema)`. Rule for combining several roles: **if any
  role is `All` → no filtering**; otherwise the per-role predicates are combined as an **OR union**.
  - `Own` = owner column `IN {UserRowId, EmployeeRowId}` (**two identities**: the column may hold a user rowid (such as
    the person who keyed it in) or an employee rowid (such as the employee taking leave); GUIDs do not collide, so a
    single strategy covers both; a user does not necessarily map to an employee).
  - `Dept` = `dept column = DeptRowId` **OR Own** (implies Own).
  - `DeptAndSub` = `dept column IN GetSelfAndDescendants(DeptRowId)` **OR Own**, expanded with the per-company
    `DepartmentTree` cache.
- **"User → department" with zero DB**: `st_employee.user_rowid` links common `st_user` ↔ company `st_employee`;
  `EnterCompany` resolves `user→employee→dept` once and **snapshots** `UserRowId`/`EmployeeRowId`/`DeptRowId` **into
  `SessionInfo`**, so queries need zero DB.
- **Read side**: `GetList`/`GetData` `AND` the scope filter into the query (out-of-scope rows are filtered out / an
  out-of-scope single record returns `null`, indistinguishable from "not found").
- **Write side (Update / Delete) = an authoritative backend re-query**: an existence query
  `sys_rowid = id AND scope` against the target rowId (`ExistsInScope`) confirms that **the row in the DB** is in
  scope; **the row values sent by the client are not evaluated** (a forged payload that relabels them cannot get
  around it either). `Save` first determines that the master row is "saving an existing record" (not `Added`) before
  querying; an out-of-scope `Delete` → deletes 0 rows, no cascade.
- **Create does not apply scope**: a new row has no "existing scope" to violate; action authorization (layer 1)
  guards it.
- **Scope is master-only / whole-record integrity**: only the master row is checked, only the master table is
  queried → once the master passes, the details go through with the whole record, so there is no half-way "master
  passes, one detail is blocked".

  > ⚠️ The last two items describe the design at the time of the decision. The current save path also checks the
  > values new and changed master rows leave behind, and does not let detail rows pass on the master check alone; the
  > checks that do this are named under "Implementation evolution" at the end.

## Consequences and trade-offs

- ✅ **Zero DB for permission checks**: identity snapshot + per-company caches (roles / permissions / department
  tree); the DB is touched only at login, when entering a company and when the configuration changes.
- ✅ **Action and scope are orthogonal**: layer 1 handles (model, action), layer 2 handles rows; each is independent
  and they compose.
- ✅ **Per-action precision**: the scope for viewing and for changing can differ.
- ✅ **Symmetric reads and writes, security boundary in the backend**: the write side uses an authoritative re-query
  and does not trust the payload.
- ✅ **Decoupled from forms/tables**: several progIds of one model share one authorization.
- ⚠️ **Snapshot semantics**: `Roles` / employee / dept are a snapshot in a session that has already entered a
  company, so configuration changes made in the meantime are not reflected immediately (acceptable; if it must be
  immediate, re-entering the company to refresh or cache-notify can be added).
- ⚠️ **Fail-closed boundary**: if a column the scope needs is missing or the identity is empty → no rows match (a safe
  default). `PermissionBindingValidator` can detect such definition gaps in advance, but **the framework does not run
  it automatically**; the host has to wire it up itself (see the
  [user guide](../../docs/en/security/permission-authorization.md#definition-validation-host-invoked)).
- ✅ **Frontend capability (fine-grained element degradation) implemented (2026-07-03)**: layers 1 and 2 are still
  enforced authoritatively at the backend method layer and do not rely on the frontend. Permissions can be seen as
  **three dimensions × two checkpoints**: the **action** dimension is gated authoritatively in the backend and is also
  projected to the frontend to decide the state of toolbar commands / buttons; the **row** dimension is backend only;
  the new **field** dimension (`FormField.SensitiveCategory` → a well-known category model, hidden / read-only by
  Read/Update) is frontend only. The capability snapshot rides along with the `EnterCompany` response
  (`EnterCompanyResponse.Capabilities`), is cached in `ClientInfo.Capabilities`, and is resolved by
  `Polhem.UI.Core.Permissions.ElementCapabilityResolver`. The frontend is **pure UX, not a data boundary** (the
  backend does not mask sensitive column values). See Part 2 of the
  [user guide](../../docs/en/security/permission-authorization.md) for details.

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing
with the current code:

- **2026-09-27: Detail rows are checked on their own.** "Once the master passes, the details go through" no longer
  describes the save path. `EnforceWriteScope` and `EnforceDetailOwnership`
  (`src/Polhem.Business/Form/FormBusinessObject.WriteScope.cs`) refuse a payload that carries detail rows but no
  master table; require every written detail row's `sys_master_rowid` to name a master row of the payload; require
  every modified or deleted detail row to already belong, in the database, to an existing master row of the payload;
  check master scope on the Original rowid that the UPDATE and DELETE bind; and refuse a row whose rowid changes.
  The regression tests are in `FormBusinessObjectWriteScopeTests`
  (`tests/Polhem.Business.UnitTests/Form/FormBusinessObjectWriteScopeTests.cs`).
- **2026-09-27: New values are scope-checked.** "Create does not apply scope" no longer holds: `EnforceNewValueScope`
  requires the values an Added or Modified master row leaves behind to be inside the caller's Create or Update scope,
  evaluated after the `BeforeSave` step, so a user limited to their own records cannot create a record owned by
  someone else or move one out of their reach.
- **2026-09-27: Read paths are narrowed.** `GetList` accepts only filter and sort fields that the form's table
  declares, and refuses `ProtectedFields` columns. `GetLookup`, which is not gated by the Read action, applies the
  Read record scope; a business object opts out by overriding `LookupAppliesRecordScope`
  (`src/Polhem.Business/Form/FormBusinessObject.Read.cs`).
- **2026-09-27: Unguarded forms are reported at startup.** The host logs a warning naming the registered forms whose
  FormSchema declares no `PermissionModelId` (`src/Polhem.Hosting/Registry/UnguardedFormWarningService.cs`).
- **2026-09-27: The session snapshot is one immutable object.** The roles and the `UserRowId` / `EmployeeRowId` /
  `DeptRowId` identities that `EnterCompany` snapshots now form one `SessionCompanyScope`, swapped in a single write;
  see [ADR-012](adr-012-session-company-context.md) "Implementation evolution".
- **2026-09-27: The capability resolver moved.** `ElementCapabilityResolver` is now
  `Polhem.Api.Client.Permissions.ElementCapabilityResolver` (`src/Polhem.Api.Client/Permissions/`), so both UI heads
  can use it; the frontend capability entry above names its location at the time.
- **2026-09-27: The action enum is `PermissionActions`.** The `PermissionAction` in a rule's `(PermissionAction,
  ScopeStrategy)` pair above is now the flags enum `PermissionActions`
  (`src/Polhem.Definition/Settings/Permission/PermissionActions.cs`); the pair is otherwise unchanged.

## References

- Related ADRs: [ADR-005 (FormSchema-driven)](adr-005-formschema-driven.md),
  [ADR-010 (logical DB categories)](adr-010-logical-database-category.md),
  [ADR-012 (session company context)](adr-012-session-company-context.md),
  [ADR-017 (DB cache invalidation)](adr-017-db-cache-invalidation.md)
- User guide: [en/permission-authorization.md](../../docs/en/security/permission-authorization.md)
