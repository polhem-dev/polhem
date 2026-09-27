# Permission & Authorization Guide

[繁體中文](../zh-TW/permission-authorization.md) · [← Docs Index](README.md)

Polhem permissions span **three dimensions**, applied at **two enforcement points** — the **back end** is the authoritative security boundary; the **front end** degrades UI elements as a UX affordance (never a security boundary):

| Dimension | Question | Back end | Front end |
|-----------|----------|----------|-----------|
| **Action** | Can this user perform this *action* on the model? | ✅ authoritative gate — `Can(model, action)` | ✅ toolbar command / button state |
| **Record (row)** | On *which rows* may they do it? | ✅ authoritative scope filter + write re-query | — |
| **Field (column)** | May they *see* / *edit* this sensitive field? | — *(not masked server-side)* | ✅ hide (no `Read`) / lock (no `Update`) |

The **Action** dimension applies at *both* points: the back end enforces it at the method layer (the real boundary), and the front end mirrors it as command/button state so users are not offered actions they cannot perform. **Record** is back-end only. **Field** is front-end only — a UX affordance, not a data boundary (see the caveat in [section 10](#10-enabling-capability-in-a-host-app-opt-in)).

The permission data both back-end dimensions consult — roles, grants, the department tree and the session's identity — comes from in-memory snapshots at request time (the database is read for it only when loading the caches, at login, on `EnterCompany`, or when configuration changes). The record-scope checks on writes still query the rows themselves, as described in section 5. Authorization is **orthogonal** to `ApiAccessControlAttribute` (which governs encryption level and whether login is required). See [ADR-019](../adr/adr-019-permission-authorization-model.md) for the design rationale.

All three dimensions are **company-scoped**. Assets that belong to the installation rather than to any company — API keys, and whatever a deployment adds later — are governed by a separate, parallel decision described in **Part 3**.

---

# Part 1 — Back-end enforcement (Action + Record)

The Action and Record dimensions are the authoritative gate. Both read their permission data from in-memory snapshots and are decoupled from forms.

## 1. Define permission models

A **permission model** is a business entity (e.g. `PurchaseOrder`), deliberately distinct from a form's `progId`. Models live in a single registry (`PermissionModels`, `DefineType.PermissionModels`). Each model declares, per action, a default record-scope strategy:

```xml
<PermissionModels>
  <Models>
    <PermissionModel ModelId="PurchaseOrder" DisplayName="Purchase Order">
      <Rules>
        <PermissionRule Action="Read"   Scope="DeptAndSub" />
        <PermissionRule Action="Update" Scope="Own" />
        <PermissionRule Action="Delete" Scope="Own" />
        <PermissionRule Action="Create" Scope="All" />
        <!-- Print / Export omit Scope → inherit the model's Read scope -->
      </Rules>
    </PermissionModel>
  </Models>
</PermissionModels>
```

- `ModelId` is a PascalCase business entity. **One model may be consumed by many forms** (`PO001` create, `PO002` query, `PO009` report all reference `PurchaseOrder`) — granting once enables all three.
- The model is bound to neither a table nor a column. Scope strategies are pure semantics; the concrete columns come from the FormSchema (next section).

## 2. Bind a form to the model

A `FormSchema` declares which model it consumes, and marks which **master-table** columns play the owner / department role:

```xml
<FormSchema ProgId="PO001" PermissionModelId="PurchaseOrder" ...>
  <Tables>
    <FormTable TableName="PO001" ...>
      <Fields>
        <FormField FieldName="buyer_rowid" Caption="Buyer"      ScopeRole="Owner" />
        <FormField FieldName="dept_rowid"  Caption="Department" ScopeRole="Dept" />
        <!-- ... other fields ... -->
      </Fields>
    </FormTable>
  </Tables>
</FormSchema>
```

Rules:

- `ScopeRole` is **master-table only**. Marking it on a detail table is reported by `PermissionBindingValidator` (see [Definition validation](#definition-validation-host-invoked)) — record scope is decided on the master record; details follow it.
- **A master table may mark multiple `Owner` / `Dept` columns** — each contributes an OR branch. For example a transfer form marks both a from-department (`from_dept`) and a to-department (`to_dept`), so *both* departments' managers can see the record.
- An empty `PermissionModelId` makes the form **unscoped** — both back-end layers are skipped (gradual adoption / backward compatible).

## 3. Grant roles

Roles, grants and assignments live in each **company database** (`st_` framework tables, per-company config):

| Table | Columns | Meaning |
|-------|---------|---------|
| `st_role` | `sys_id`, `sys_name` | a role (a unit you assign) |
| `st_role_grant` | `role_id`, `model_id`, `action`, `scope` | one (role, model, action) the role is granted, plus its record scope |
| `st_user_role` | `user_id`, `role_id` | user ↔ role assignment (`user_id` is `st_user.sys_id`) |

`st_role_grant` is **per-action**: the presence of a row is the layer-1 grant; its `scope` (a `ScopeStrategy`) drives layer-2. This is what lets a role *read* its whole department but *update* only its own records.

```sql
-- "Buyer" can read the department-and-sub PurchaseOrders, but only update/delete its own:
INSERT INTO st_role_grant (role_id, model_id, action, scope) VALUES
  ('Buyer', 'PurchaseOrder', 2 /*Read*/,   4 /*DeptAndSub*/),
  ('Buyer', 'PurchaseOrder', 4 /*Update*/, 2 /*Own*/),
  ('Buyer', 'PurchaseOrder', 8 /*Delete*/, 2 /*Own*/);
-- scope = ScopeStrategy: Inherit=0, All=1, Own=2, Dept=3, DeptAndSub=4
-- action = PermissionActions (flags): Create=1, Read=2, Update=4, Delete=8, Print=16, Export=32
```

`scope = Inherit (0)` defers to the model's per-action default (section 1).

## 4. Link users to employees (for department scope)

Department / owner scope needs to resolve **the current user → their department**. A user (`st_user`, common DB) is linked to an employee (`st_employee`, company DB) via `st_employee.user_rowid`:

```
st_user.sys_rowid  ──(st_employee.user_rowid)──▶  st_employee  ──(dept_rowid)──▶  st_department
```

On `EnterCompany`, the framework resolves `user → employee → department` once and snapshots `UserRowId`, `EmployeeRowId`, `DeptRowId` onto the session. Scope filtering then runs zero-DB. A user without a linked employee gets empty employee/department — `Own` still matches their `UserRowId`, while `Dept`/`DeptAndSub` match nothing (fail-closed).

## 5. How enforcement behaves

### Layer 1 — action gate

`FormBusinessObject` checks `(model, action)` before running:

- `GetList` / `GetData` / `GetNewData` → `Read`
- `Save` → per row by `RowState`: `Added`→`Create`, `Modified`→`Update`, `Deleted`→`Delete`
- `Delete` → `Delete`
- `GetLookup` is **not** action-gated: a user who may not browse the target form still needs to pick a reference value from it. Its exposure is bounded by the form's `LookupFields` and by the record scope below.

Multiple roles **OR-merge** (capabilities accrue). A failing check throws `ForbiddenException`.

### Layer 2 — record scope

**Reads** (`GetList`, `GetData`, `GetLookup`) AND the `Read` scope filter into the query. Out-of-scope rows are filtered out; an out-of-scope single-row fetch returns `null` (indistinguishable from "not found", so a caller cannot probe records they may not see). A business object whose lookup must offer every row, such as a shared unit or currency list, opts out for `GetLookup` by overriding `LookupAppliesRecordScope` to return `false`.

`GetList` also restricts what a caller may name: filter and sort fields must be fields the form's table declares, and a protected column (see section 12) can be neither selected, filtered nor sorted on. A call without paging returns the first page of `PagingOptions.MaxPageSize` rows.

**Writes** are gated by an **authoritative re-query** against the database — `WHERE sys_rowid = id AND <scope>` — *not* by evaluating the submitted payload. A forged DataSet cannot relabel its way past the boundary.

- `Save` checks every existing master row of the payload (any master `RowState` other than `Added`) against the `Update` scope, or the `Delete` scope for a deleted row, on the row id the UPDATE or DELETE statement binds (the row's Original version). A details-only edit leaves the master `Unchanged` but still counts as an `Update`.
- The values an `Added` or `Modified` master row leaves behind must be inside the caller's `Create` or `Update` scope too. The check runs after the `BeforeSave` step, on the row's current values, so owner or department values that a default expression, a `DoBeforeSave` override or a plugin fills in are the ones checked. Without it a user limited to their own records could create a record owned by someone else, or move one of theirs into another department.
- A row whose Original and Current `sys_rowid` differ is refused, and so is a payload that carries detail rows without the master table they belong to.
- Detail rows follow the master: each written detail row must name a master row of the payload, and every modified or deleted detail row must already belong, in the database, to an existing master row of the payload.
- `Delete(rowId)` affects no rows and cascades nothing when the row is out of scope.

A refusal throws `ForbiddenException`.

### Scope strategies

| Strategy | Read filter (and write re-query) |
|----------|----------------------------------|
| `All` | no restriction |
| `Own` | `ownerField IN {UserRowId, EmployeeRowId}` |
| `Dept` | `deptField = DeptRowId` **OR** Own |
| `DeptAndSub` | `deptField IN (department + descendants)` **OR** Own |
| `Inherit` | the model's per-action default (else its Read scope, else `All`) |

- `Dept` / `DeptAndSub` **implicitly include `Own`** — a user always sees records they own.
- **Multiple scope columns**: when the master marks several `Owner` / `Dept` columns, the strategy **OR-unions across all of them** — e.g. a transfer form's `from_dept` and `to_dept` each `IN` the subtree, so a record shows to a manager of either department.
- **Multi-role merge**: if *any* role grants `All` for the action → no filter; otherwise the restrictive strategies are **OR-unioned**.
- The `Own` owner column may hold either a user row id or an employee row id (e.g. the *creator* vs the *employee on a leave form*); the `IN {UserRowId, EmployeeRowId}` set covers both, and a user need not map to an employee.

---

# Part 2 — Front-end capability (Action commands + Field)

The front end degrades UI elements from a per-model **capability snapshot**, so users are not offered commands or shown sensitive data they lack permission for. It surfaces **two** of the three dimensions: the **Action** dimension as toolbar command / button state, and the **Field** dimension as sensitive-field hiding / locking. This is **UX only** — the back end (Part 1) remains the authoritative boundary.

## 6. Mark sensitive fields

The Field dimension is **opt-in**: mark only the fields that need controlling. Most fields carry no marker and always render per their layout.

```xml
<FormField FieldName="unit_cost" Caption="Unit Cost" SensitiveCategory="Cost" />
```

`SensitiveCategory` (default `None` = not controlled) is a **named, finite classification** — `Amount`, `Cost`, `PersonalData` — parallel to `ScopeRole`. The designer picks a category rather than inventing an id, so the set is closed and checkable. It applies to **any field**, master or detail grid column.

## 7. Well-known category models

Each non-`None` category maps **by convention** to a permission model whose id equals the category name (`Cost` → the `"Cost"` model). These are ordinary entries in the same `PermissionModels` registry — declare and grant them like any other model. `PermissionBindingValidator` reports an error when a marked category has no matching model (see [Definition validation](#definition-validation-host-invoked)).

```sql
-- A viewer may see cost but not change it (Read only); an editor may change it (Read + Update).
-- Editing requires Read too — Update without Read leaves the field hidden.
INSERT INTO st_role_grant (role_id, model_id, action, scope) VALUES
  ('CostViewer', 'Cost', 2 /*Read*/,   1 /*All*/),
  ('CostEditor', 'Cost', 2 /*Read*/,   1 /*All*/),
  ('CostEditor', 'Cost', 4 /*Update*/, 1 /*All*/);
```

The category gate is **company-wide and orthogonal to the form's own model**: seeing a `Cost` column depends on `Cost.Read`, *independent* of `PurchaseOrder.Read`. A user may be permitted to read purchase orders yet still have their cost columns hidden. This matches ERP practice — cost/amount/PII visibility is a data-classification concern that should be consistent across every form, granted once.

## 8. How the capability snapshot reaches the client

On `EnterCompany`, the back end computes the per-model action mask for the session's roles (`CompanyRolePermissions.GetAllowedByModel`) and returns it on `EnterCompanyResponse.Capabilities` — a `Dictionary<modelId, PermissionActions>` — riding the existing `EnterCompany` round-trip, so there is **no extra request**. Only models the user holds a grant on appear in the map.

## 9. How the client degrades

`ClientInfo.Capabilities` (`Polhem.UI.Core`) caches the snapshot (nullable), and `Polhem.Api.Client.Permissions.ElementCapabilityResolver` (a pure, UI-agnostic resolver) reads it. The Avalonia views (`FormView`, `ListView`) apply it today; the Blazor components do not read the snapshot.

- **`null` → capability inactive → nothing is degraded.** An app that never enters a company, or does not use permissions, renders exactly as before.
- **Non-null → active.** A model absent from the map means *no permission* on it.

Two element kinds consume the resolver:

- **Commands** (toolbar buttons). Each button is tagged at creation with the `PermissionActions` it needs (`New`→`Create`, `Edit`→`Update`, `Save`→`Create|Update`, `Delete`→`Delete`, `View`→`Read`); the resolver's `Can(...)` checks the form's `PermissionModelId` with **any-of** semantics (`Save` shows if the user holds either `Create` or `Update`). An un-permitted button is hidden. This is the front-end **projection of the Action dimension** as UX.
- **Sensitive fields.** `ResolveField(...)` reads the field's `SensitiveCategory`, looks up the category model, and degrades on **two independent sub-gates** — `Read` controls *visibility*, `Update` controls *editability* — so a field can be viewable but not editable. Applied to master fields and detail grid columns alike.

  | `<category>.Read` | `<category>.Update` | Result |
  |:---:|:---:|---|
  | ✗ | (either) | **Hidden** — the column is not rendered |
  | ✓ | ✗ | **Visible, read-only** — *e.g. see the cost, cannot change it* |
  | ✓ | ✓ | **Visible, editable** |

  Hidden wins over read-only — there is no point marking editability on a field you cannot see.

> **Detail grid actions (add/edit/delete rows) are not capability-gated.** A detail belongs to the same aggregate as its master, so whether its rows can be edited follows the form's edit mode — and the permission to enter that edit mode was already enforced by the toolbar command. Only the grid's sensitive *columns* are degraded.

Degradation never mutates cached definitions: the client applies it to the per-view generated layout, narrowing visibility/editability only.

## 10. Enabling capability in a host app (opt-in)

Capability is **inert until wired**, so existing apps are unaffected. To turn it on:

1. Declare the well-known category models (`Amount` / `Cost` / `PersonalData`) in `PermissionModels` and grant them (section 7).
2. After `SystemApiConnector.EnterCompanyAsync`, hand the response to the client cache:
   ```csharp
   var response = await ClientInfo.SystemApiConnector.EnterCompanyAsync(companyId);
   ClientInfo.ApplyEnterCompanyResult(response);   // caches the capability snapshot and drops the previous tenant's definitions
   ```
3. On `LeaveCompany` or logout, clear it: `ClientInfo.ClearCompanyContext();` (which drops the cached definitions too).

> **Caveat — the Field dimension is UX, not a data boundary.** `GetList` / `GetData` still return the sensitive column's value; the client merely hides or locks it. A client that bypasses the standard UI could still receive the raw value over the API. Treat field permission as *presentation*. Anything that must never leave the server belongs behind an **Action** or **Record** boundary (Part 1), or its own permission model — not solely a `SensitiveCategory`. Server-side column masking is a separate future concern (see Non-goals).

---

# Part 3 — Deployment-level administration (outside the company model)

Everything above is scoped to a company: roles, grants and the department tree all live in **each company's own database**, and `ICompanyAuthorizationService.Can` returns `false` when the session has entered no company.

Some assets belong to the **installation** rather than to any company — an API key identifies a calling *application*, not a tenant. Guarding those with company permissions would mean one tenant's administrator could act for all of them. They are therefore governed by a separate, parallel decision.

| | Company authorization | Deployment authorization |
|---|---|---|
| Interface | `ICompanyAuthorizationService.Can(token, modelId, action)` | `IDeploymentAuthorizationService.Can(token, action)` |
| Question | May this user do X **inside company C**? | May this user do X **to the installation**? |
| Identity source | `st_role` / `st_role_grant` / `st_user_role` in each company database | `st_user.deployment_admin` in the common database |
| Requires a company | Yes — no company context, no permission | No — by definition there is none |

> **The two never grant each other anything.** A deployment administrator gains **no data rights in any company**: reading or writing a company's records still runs the Action and Record gates of Part 1, which consult that company's roles and know nothing about the flag. A company administrator gains nothing here. Neither check falls back to the other.

## 11. What deployment authorization covers

`DeploymentAction` is a deliberately small enumeration; it lists only the actions the framework performs:

| Action | Gated operations |
|--------|------------------|
| `ManageApiKey` | `SystemBusinessObject.CreateApiKey`, `ListApiKeys`, `SetApiKeyEnabled`, `SetApiKeyExpiry` — issuing, listing, enabling or disabling, and expiring API keys |
| `ReadDbAnomalyLog` | `AuditLogBusinessObject.GetDbAnomalyLog` — the database anomaly log (`st_log_anomaly_db`), which carries no company and records every tenant's database ids and SQL command templates |

The API key methods run the check only for **remote** callers. An in-process call passes without an administrator, which is what keeps a fresh deployment able to mint its first key on the host before any administrator exists. The database anomaly log has no such bootstrap need, so a local call is held to the same check.

The check reads the flag from the database **on every call**, deliberately unlike the company path (which answers from cache and touches no database). Deployment operations are rare, and revoking an administrator has to take effect immediately — any cached form of the flag would delay it.

## 12. Appointing an administrator

`SystemBusinessObject.SetDeploymentAdmin` (`SystemApiConnector.SetDeploymentAdminAsync(userId, isDeploymentAdmin)` from a client) is the **only** write path the framework provides to `st_user.deployment_admin`, and it is `LocalOnly`: the first administrator is appointed on the host, after which appointments can be made through whatever administration surface the deployment builds on top of it.

The framework keeps the FormSchema-driven data path away from the column at runtime, not by convention: `ProtectedFields` lists it, and the write path (`DataFormRepository.Save`) strips it from every INSERT and UPDATE **even if a form declares it**. Without that, a deployment that built its own user-maintenance form over `st_user` would have handed its ordinary users a route to promote themselves. Reads are guarded too, because a column that can be filtered or sorted on can be read one comparison at a time: a query that names a protected column in its select list, filter or sort is refused, and a query that asks for every field leaves it out.

`st_user.password` is protected the same way, and for a sharper reason: the column grants the privilege of *being* the user, and its contents are load-bearing — `PasswordHasher.VerifyPassword` parses the iteration count, salt and hash out of the stored string. A form writing that column would be storing values the verifier never produced. The framework itself writes the column only to replace a weaker stored hash after a successful sign-in; it has no password-change operation.

> **Upgrading:** a deployment that set passwords through its own maintenance form over `st_user` will find that column silently dropped from the save after this change. Move it to a dedicated operation that hashes what it stores; a form has no way to produce a value `VerifyPassword` will accept.

The framework ships no `st_user` rows, so seeding a first administrator on a brand-new deployment is the deployment's own decision; the column defaults to "not an administrator" either way.

## 13. Audit trail

Deployment operations are recorded on the change axis (`st_log_change`) under the `System` prog id, with `source` naming the operation (for example `System.SetDeploymentAdmin`, `System.CreateApiKey`, `System.SetApiKeyEnabled`, `System.SetApiKeyExpiry`) and the acting user denormalised into `user_id` / `user_name` as with any other audit row. The `changes_xml` payload carries before/after values, so the trail distinguishes a **grant** from a **revoke** — both are an `Update` and would otherwise be indistinguishable.

Two properties are worth knowing:

- **Always marked sensitive** (`is_sensitive`), so a log filtered down to what matters still shows them.
- **Not subject to `AuditLogOptions.ChangeEnabled`.** That switch exists so a deployment can opt out of the volume of ordinary business-data history; appointing an administrator is neither ordinary nor voluminous. Turning it off leaves these entries in place — only turning off `AuditLogOptions.Enabled` entirely stops them.

An API key audit entry records the key's id, name, type, contact and expiry. It **never** records the secret or its hash: the log database is a separate store with its own, usually wider, readership.

## 14. Upgrading an existing deployment

1. **The column arrives automatically.** `st_user.deployment_admin` is added by the framework's schema upgrade (an `ALTER … ADD`); existing rows take the default and are *not* administrators. No manual DDL.
2. **Unless you have overridden the table's definition.** The framework reads table definitions only from your `DefinePath` at runtime. If your deployment ships its own `st_user.TableSchema.xml`, add the column there — the framework's embedded default is not consulted. (See [Framework-Reserved Names §3](framework-reserved-names.md#3-consumer-guidelines).)
3. **Appoint the first administrator on the host**, through an in-process call to `SetDeploymentAdmin`. Until then no remote caller can mint an API key, while local calls continue to work exactly as before the upgrade.

---

## Definition validation (host-invoked)

`PermissionBindingValidator.Validate(schemas, models)` checks the binding between forms and the
permission registry: every `FormSchema.PermissionModelId` references an existing model, `ScopeRole`
is marked on master tables only, and every non-`None` `SensitiveCategory` has a matching well-known
model. It returns one message per violation and an empty list when the definitions are valid.

**The framework does not call it for you.** An invalid binding does not stop the application from
starting; it surfaces later as a permission check that behaves differently from what was intended.
A `PermissionModelId` that names no model in the registry still runs the action gate against
whatever grants carry that id, but an `Inherit` grant then finds no model default and resolves to
`All`, so the record scope silently disappears. Invoke the validator yourself where a failure is
cheap to act on: at host startup, in a deployment smoke test, or in a CI step over the definitions
in your `DefinePath`.

What the framework does check at startup is the opposite case: a host built with
`AddPolhemFramework` logs one warning listing the forms registered in `ProgramSettings` whose
`FormSchema` declares no `PermissionModelId`, because every authenticated user of the company can
read and write such a form. The warning never stops the host.

```csharp
var errors = PermissionBindingValidator.Validate(allFormSchemas, permissionModels);
if (errors.Count > 0)
    throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
```

## Caching & invalidation

- Role/grant/user-role tables load into a per-company `CompanyRolePermissions` cache; the department tree into a per-company `DepartmentTree` cache. Both are DB-sourced and evicted via the common cache-notify poller.
- `SessionInfo.CompanyScope` (a `SessionCompanyScope`) holds the request-time snapshot (`Roles`, `UserRowId`, `EmployeeRowId`, `DeptRowId`), populated at `EnterCompany`, cleared at `LeaveCompany` / `Logout`. It is replaced as a whole, so a check never sees roles from one company and row ids from another.
- The client capability snapshot (`ClientInfo.Capabilities`) is also point-in-time: populated at `EnterCompany`, cleared on `LeaveCompany` / token change. Re-enter the company to refresh it after a grant change.
- Snapshots are point-in-time: configuration changed mid-session is reflected for cache-backed checks (`Can` reads the live cache) but role/employee/department snapshots on an already-entered session update on the next `EnterCompany`.

## Transport & credential hardening (production)

- **Require HTTPS.** `Login` is declared `ApiProtectionLevel.Public`: `SystemApiConnector.LoginAsync` sends the password as `PayloadFormat.Encoded` (serialize + compress + Base64 — *not* encryption), and a JavaScript client may send it as Plain. The RSA handshake only protects the session key the server returns, and a browser (WebAssembly) client skips it. Transport confidentiality therefore rests entirely on TLS. Serve every production endpoint over HTTPS (and enable HSTS); never expose the JSON-RPC endpoint over plain HTTP.
- **Issue an API key.** Until `st_api_key` holds an enabled key, the default `ApiAuthorizationValidator` accepts any non-empty `X-Api-Key`; once one exists, only issued, enabled, unexpired keys pass. `UsePolhemFramework` logs the missing gate at startup, as an error outside the Development environment. The API key identifies the calling application; user authentication runs on the Bearer access token either way. See [API Key Management](api-key-management.md). A host that needs a different transport check can still replace `ApiServiceOptions.AuthorizationValidator`.

## Non-goals

- **Declarative custom-command model** — standard toolbar commands are tagged in code (section 9); Print / Export / Approve as *data-defined* `FormLayout` elements are not modelled yet. When added, custom commands will carry their own opt-in `PermissionActions`.
- **Back-end field masking** — the Field dimension is front-end UX. Server-side masking of sensitive columns (so their values never leave the server) is not yet implemented; use an Action/Record boundary for hard data confidentiality today.
