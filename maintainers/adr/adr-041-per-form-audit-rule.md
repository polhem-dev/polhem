# ADR-041: Per-form audit rules: change and access logging are configured form by form

## Status

**Accepted (2026-08-26)**

This closes the first item under "To do" in [ADR-027](adr-027-audit-trail.md), and fills in the two requirements of
[ADR-040](adr-040-audit-trail-taxonomy.md) decision 4 that had not been implemented, "driven by sensitivity" and
"limited entry points". ADR-027's six-axis taxonomy, DiffGram storage and best-effort write strategy are all
unchanged.

## Context

The change and access axes originally had only deployment-level global switches (`AuditLogOptions.ChangeEnabled` /
`AccessEnabled`), and **once turned on they applied to every form**. When you wanted "a trail only for important data"
there was no middle setting: either everything on (volume and noise) or everything off (no trail). At the same time,
`WriteChangeAudit` hard-coded `IsSensitive` to `false`, so sensitivity could not be expressed at all.

### Checking the blueprints: neither mature ERP logs everything

SAP and Odoo were checked before deciding on the approach. **Neither logs every object, and both use a two-layer
structure.**

| Mechanism | What it records | Who decides, and where |
|------|--------|----------------|
| SAP **Change Documents** (`CDHDR`/`CDPOS`) | Field-level changes to business objects | **Three-layer opt-in at development time**: tick "Change document" on the field's data element → create a Change Document Object in `SCDO` listing the tables to record → the program calls the generated `*_WRITE_DOCUMENT` FM |
| SAP **Table Logging** (`DBTABLOG`) | Table-level changes | **Two layers ANDed**: the table-level "Log Data Changes" in `SE13` × the system-level profile parameter `rec/client`. SAP states explicitly that it is meant for manual changes to customizing tables |
| SAP **Read Access Logging** | Views | **Purely runtime configuration** (`SRALMANAGER`); customers define their own log purpose / channel / fields |
| Odoo core **chatter tracking** | Field changes | Development time, `tracking=True` written on the model's field definition |
| Odoo OCA **`auditlog`** | CRUD + read | **A runtime table** `auditlog.rule`, one row per model; `log_read` explicitly defaults to `False` |

Three observations had a decisive influence on this design:

1. **Neither logs everything.** The framework's original behavior has no counterpart in either blueprint.
2. **Change and access are always configured separately, and access is off by default** (SAP RAL is opt-in, Odoo has
   `log_read=False`).
3. **Both are two-layer structures**: a master gate × a per-object declaration. SAP's `rec/client` × `SE13` has
   exactly this shape.

### Odoo's per-model unit is a result of its mechanism, not a design choice

OCA `auditlog` works by **monkey-patching ORM methods at runtime**: on subscribe it hooks five wrappers, `create` /
`read` / `write` / `unlink` / `export_data`, onto `self.env.registry[model._name]`; on cancel it has to revert them and
reload the registry, and after a restart it re-hooks them through `_register_hook()`.

**The target of the patch is the model class, so the granularity can only be the model.** This is not a chosen design,
and it has a cost: Odoo itself says read logging does not work for every model, and paths that bypass the ORM are not
recorded either.

**This framework's instrumentation points are native**: `Save` / `Delete` / `GetData` of `FormBusinessObject` are the
path every FormSchema-driven CRUD operation must take, so there is no patching, no re-hooking after a restart, and no
"some objects cannot be recorded". **This is a structural advantage and is not given up just to match the
blueprints.**

## Decision

### 1. Rules are stored in a runtime table, not in definition files

A new `st_audit_rule` holds one row per form. **Audit policy is the customer's operational decision, not a definition
delivered with the application**: definition files get overwritten when the application is upgraded, and policy should
not. This matches Odoo `auditlog.rule` and SAP RAL, and explicitly excludes the SAP Change Documents route of "writing
it into development-time definitions".

### 2. Company scope (per tenant)

Each company decides for itself which forms to record. `st_role` / `st_department` are existing precedents of "owned
by the framework but located in the company database".

The cache is **a whole snapshot per company** (`CompanyAuditRules`, cache key = companyId), not a cache per ProgId.
The reason is that **"no rule found" is the normal case**: the three-state default is `Inherit`, and the vast majority
of forms will have no rule row. Caching per ProgId would turn every form into a cache miss + a query + a negative
entry, while a whole snapshot makes "this form has no rule" a single in-memory dictionary miss. It is the same reason
`CompanyRolePermissions` chose a whole snapshot over per-permission entries.

Cross-process invalidation follows the existing pattern: the data is in the company database and the notify row goes
into `st_cache_notify` in common (the poller watches only one database), the same approach `CompanyRolePermissions`
wrote into its contract.

### 3. The unit is the ProgId, not the table

**An SAP Change Document Object is not per table either**: one object covers several tables, header + items, as a
business object unit. Odoo's model is what is per table: a purchase order needs one row each for `purchase.order` and
`purchase.order.line`.

In this framework a ProgId = one FormSchema = an aggregate of master + detail, which **matches both SAP's aggregate
concept and the actual shape of business documents**, and since the instrumentation is in `FormBusinessObject`, the
unit is naturally this.

> **Cost**: per ProgId, you cannot "record only the master, not the details": a DiffGram stores master + detail as one
> row (ADR-027 D5). That need has to wait for table / column level granularity.

### 4. Three states `Inherit` / `On` / `Off`, defaulting to `Inherit`

No rule row = everything `Inherit` = the global switches apply = **behavior after upgrading is completely unchanged**.
Zero breakage.

### 5. `Enabled` is the only hard master gate; the axis switches are not a second gate

| Switch | Role |
|------|------|
| `AuditLogOptions.Enabled` | **The only hard master gate**. When off it short-circuits immediately, without even consulting the rule cache (equivalent to SAP `rec/client=OFF`) |
| `ChangeEnabled` / `AccessEnabled` | The **default value** of that axis, inherited by `Inherit`. **Not a gate** |

```
Enabled = false                          → not recorded (short circuit, zero cost)
Enabled = true, rule = On                → recorded (even if the axis default is false)
Enabled = true, rule = Off               → not recorded (even if the axis default is true)
Enabled = true, rule = Inherit / no row  → follows ChangeEnabled / AccessEnabled
```

**This rule was once written the wrong way round during design**, and is recorded here so the mistake is not
repeated: the first draft also made the axis switches gates, but `AccessEnabled` defaults to `false`, so "record views
of just one important form" would not have worked at all, and that is the main use of this feature.

The cost is one extra in-memory dictionary lookup per Save / GetData when `Enabled = true`. `Enabled` defaults to
`false`, so deployments that do not use auditing still pay nothing.

### 6. The policy form itself is hard-exempted from the rule table

The `AuditRule` maintenance form **always has both axes `On` and is always marked sensitive**, and that decision does
not go through the rule table.

**This is a security requirement, not a convenience.** If the policy form were governed by ordinary rules, anyone who
can maintain rules could set the `AuditRule` row to `Off`, after which every policy change would leave no trace:
**auditing could be quietly turned off by the audit policy itself, with no record showing it ever happened.**

This deliberately mirrors the deployment-level auditing of `SystemBusinessObject`: that too is governed only by
`Enabled` and cannot be turned off individually, for the same reason. A deployment with auditing on cannot choose not
to record "who granted a capability", and audit policy is exactly that kind of grant.

### 7. The maintenance form declares a `PermissionModelId`, the only one among the framework's default forms

Audit policy is a privileged operation (SAP's `SE13` needs Basis authorization, Odoo's `auditlog.rule` sits behind
Technical Features). Enforcement is **fail-closed**: without an authorized model, `ForbiddenException`.

Decision 6 is **detection** (proving that someone touched the policy); this item is **prevention** (stopping it from
happening). The two complement each other and cannot replace each other. A concrete scenario: someone sets a form's
`change_mode` to `Off`, changes data, then sets it back to `Inherit`, and the period in between has no trail at all.

Cost: after copying the definition files into a deployment, a model has to be created and authorized before the form
can be used; by default "nobody can open it". `Defaults/` was always a scaffolding source rather than a runtime load
path, so this "configure before use" cost falls where it belongs. The framework therefore ships a sample
`PermissionModels.xml`.

## Explicitly not included

| Item | Reason |
|------|------|
| **Field-level sensitivity** | The complete form of ADR-040 decision 4; it requires changing the DiffGram filtering logic and is scoped separately |
| **Action-level switches** (`GetData` / `Save` / `Delete` separately) | Views are currently instrumented at a single point, `GetData`, so the action level makes no practical difference for now |
| **User filters** (Odoo `user_ids` / `users_to_exclude_ids`) | No need yet |
| **Not copying Odoo's `state: draft / subscribed`** | That state exists **because subscribing has to do the patching**. This framework has no patch to hook, so `draft` has no mechanical meaning; the three states already fully express "not in effect". **This is the thing most likely to be cargo-culted in** |
| **Not copying Odoo's `log_type: full / fast`** | A DiffGram always contains before/after, so there is no cheaper "record only the incoming values" level to save on |

## Rationale

**Why copy the two-layer structure of two ERPs instead of inventing one.** The same reason as ADR-040: the cost of an
audit taxonomy is not in writing the code but in discovering afterwards that it was cut wrong. SAP and Odoo converged
independently on the same structure for "master gate × per-object", and that agreement is itself evidence.

**Why a table and not definition files.** The difference between the two is not technical but **who owns the
decision**. Definition files are delivered with the application and overwritten on upgrade, so they belong to the
developer; audit policy is the customer's judgement of its own operational risk, and must be in the customer's hands
and not be wiped out by the next upgrade.

**Why keep native instrumentation instead of imitating patching.** This framework's CRUD has a single path every
operation must take, a condition general-purpose ORM frameworks do not have. Odoo needs patching because it has to
intercept any method of any model; we do not, and giving up this advantage to "look like the blueprint" gains nothing.

## Consequences

- **Positive**: volume is reduced at the source (instead of relying on retention-period purges); the access axis is
  finally usable (off by default + switched on form by form); `is_sensitive` has a real source; policy changes
  themselves leave a trail and are guarded by permissions.
- **Trade-offs**: one extra dictionary lookup per operation when `Enabled = true`; per ProgId, master and detail
  cannot be distinguished; the maintenance form is fail-closed and needs authorization after installation.
- **Compatibility**: no rule rows means the current behavior, so upgrading breaks nothing. **A missing table is treated
  as no rules**: existing deployments from before the upgrade do not have this table, and throwing when it cannot be
  read would make every Save fail. This is the only real regression risk of the design, and it has been verified
  separately on all five providers.
- **Upgrade path**: field-level sensitivity and action-level switches are both additive, so adding them later does not
  require working around this design.
- **Related**: the table and progId registration are in [Framework-Reserved Names](../../docs/en/reference/framework-reserved-names.md);
  the classification axes are in [ADR-040](adr-040-audit-trail-taxonomy.md); the cross-node invalidation mechanism is
  in [ADR-017](adr-017-db-cache-invalidation.md); the permission model is in
  [ADR-019](adr-019-permission-authorization-model.md).
