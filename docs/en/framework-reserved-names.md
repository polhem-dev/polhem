# Framework-Reserved Names

[繁體中文](../zh-TW/framework-reserved-names.md) · [← Docs Index](README.md)

> Registry of names owned by the **polhem** framework: which `st_*` system tables exist, and which `progId`s the framework reserves.
>
> - Naming **rules** (`st_` vs `ft_` prefix, column/index conventions) live in [Database Naming Conventions](database-naming-conventions.md).
> - Full **API method reference** (action signatures, `[ApiAccessControl]`, purpose) lives in [API Method Reference](api-method-reference.md).
> - This document lists **which specific names** are reserved by the framework. The authoritative sources are the table definitions under [`src/Polhem.Definition/Defaults/TableSchema/`](../../src/Polhem.Definition/Defaults/TableSchema/) and `Polhem.Business.ReservedProgIds`. When extending or integrating, do not collide with anything listed here.

---

## 1. System tables (`st_*`)

The `st_` prefix means "framework-owned table". It is **orthogonal to which database** the table lives in: an `st_*` table can live in the common database, the per-company database, or the log database — what matters is who owns it.

**These tables are required runtime infrastructure, not optional scaffolding.** The framework reads and writes them while serving ordinary requests, so a deployment creates them whether or not it has a use for them of its own. Overriding a framework behaviour does not exempt a deployment from the tables behind it: replacing authentication (`AuthenticateUser`) substitutes the credential check alone — signing in still reads the user's time zone and culture from `st_user` and persists the session seed to `st_session` afterwards. Missing tables surface as database errors from whichever operation reached them first, not as a configuration diagnostic.

### 1.1 Common database (shared globally)

| Table | Purpose |
|-------|---------|
| `st_user` | Global user master (login id, password hash, profile). |
| `st_company` | Company list (per-tenant root). |
| `st_user_company` | Which users can enter which companies. |
| `st_session` | Session seeds, keyed by a hash of the access token (the token itself is not stored). |
| `st_api_key` | Issued API keys (`X-Api-Key`): application identity, stored as a hash. |
| `st_define` | DB-backed definition storage (FormSchema / TableSchema / etc., when not stored as XML files). |
| `st_cache_notify` | Cross-node cache invalidation channel ([ADR-017](../adr/adr-017-db-cache-invalidation.md)). |

### 1.2 Company database (per-tenant)

| Table | Purpose |
|-------|---------|
| `st_role` | Role definitions ([ADR-019](../adr/adr-019-permission-authorization-model.md)). |
| `st_role_grant` | Role-to-resource grants (per model / action). |
| `st_user_role` | User-to-role bindings. |
| `st_department` | Organisational departments. |
| `st_employee` | Employees (links a common-DB `st_user` to a per-company organisational position). |
| `st_audit_rule` | Per-form audit rules (which forms record changes / views, see [ADR-027](../adr/adr-027-audit-trail.md)). |

> `st_department` / `st_employee` live in the company database despite their `st_` prefix — they are **framework-owned** (the record-scope and organisation tree features need them), not business data. Per-company business tables should use the `ft_` prefix.

### 1.3 Log database (audit trail and anomalies)

| Table | Purpose |
|-------|---------|
| `st_log_login` | Login events (the kinds are the members of `LoginEvent`). |
| `st_log_change` | Data-change records — one row per Save / Delete. A Save's `changes_xml` carries a DataSet DiffGram of the changed rows with their before/after values; a Delete carries the complete pre-delete record. |
| `st_log_access` | Record-view access records (who viewed which record). |
| `st_log_anomaly_api` | API-layer anomalies — which action deviated (the kinds are the `(API)` and shared members of `AnomalyKind`). |
| `st_log_anomaly_db` | DB-layer anomalies — which database + command deviated (the `(DB)` and shared members of `AnomalyKind`). |

> These tables are two different things sharing one database. `st_log_login`, `st_log_change` and `st_log_access` are the **audit trail** — who did what to which record, written through `IAuditLogWriter`. The two `st_log_anomaly_*` tables are **execution anomalies** — which execution deviated from the normal envelope, written through `IAnomalyLogWriter`; they are an operational signal rather than a business record, which is why `st_log_anomaly_db` has no acting user at all. See [ADR-040](../adr/adr-040-audit-trail-taxonomy.md).
>
> Log tables are **opt-in** (`AuditLogOptions.Enabled` is off by default) and self-sufficient: they denormalise the acting user / company so a query never joins across databases (the log database may be physically separate). Rows that refer to a session store a token fingerprint (`token_fingerprint`), never the access token. Writes go to the fixed `log` database id today; splitting the log database by year is a direction recorded in [ADR-027](../adr/adr-027-audit-trail.md), not a current feature.

---

## 2. Reserved `progId`s

### 2.1 System axis

- **`System`** (formalised as `Polhem.Definition.SysProgIds.System`) — the singleton entry point for the system-level business object (`SystemBusinessObject`). All framework-level actions that are not form-scoped (login, ping, get-define, etc.) are dispatched here.
- **`AuditLog`** (formalised as `Polhem.Definition.SysProgIds.AuditLog`) — the singleton entry point for the audit-log business object (`AuditLogBusinessObject`): read-only queries over the `st_log_*` tables. Also serves as the permission model id gating audit-trail reads.

These two, together with the `AuditRule` form below (`SysProgIds.AuditRule`), are the reserved progIds of `Polhem.Business.ReservedProgIds`: the host adds them to `ProgramSettings.xml` at startup when they are absent, and refuses to start when one is bound to a type outside the base the framework expects for it. See [Definition Files Overview §4](definition-files-overview.md#4-programsettings-is-the-type-registry).

See [API Method Reference §Axis: System](api-method-reference.md#axis-system-systembusinessobject) for the full action list.

### 2.2 Framework-shipped forms

The framework ships these forms as part of its organisation / record-scope feature:

| progId | Backing table | Purpose |
|--------|---------------|---------|
| `Department` | `st_department` | Department maintenance form. |
| `Employee` | `st_employee` | Employee maintenance form. |
| `AuditRule` | `st_audit_rule` | Audit rule maintenance form. **The only framework-shipped form that declares a `PermissionModelId`** — audit policy is a privileged operation, and enforcement is fail-closed, so the model must be granted before the form can be used. |

See [API Method Reference §Axis: Form](api-method-reference.md#axis-form-formbusinessobject) for the standard FormBO actions inherited by every form progId.

---

## 3. Consumer guidelines

When extending polhem or building applications on top of it:

- **Use `ft_` for your own business tables.** Never `st_` — that prefix is reserved for the framework. (See [Database Naming Conventions](database-naming-conventions.md).)
- **Avoid reserved `progId`s** for your own forms — `System`, `AuditLog`, `AuditRule`, `Department`, `Employee` are taken. Pick distinct progIds; convention is `PascalCase`, often prefixed with your module abbreviation.
- **To extend a framework table** (e.g. add a custom column to `st_employee`): drop a same-named `.TableSchema.xml` in your application's `DefinePath`. The framework reads only from `DefinePath` at runtime; your file is the single source the framework sees. The framework's embedded defaults are not consulted at runtime — they exist only for one-shot extraction via the API below.
- **To get the base XML to start customising from** — three options, in order of typical preference:
    - **Programmatic API** (canonical): `Polhem.Definition.Defaults.MaterializeTo("./Define")` writes every embedded framework default XML into the given directory. Skip-existing by default — re-runs are safe and won't clobber your customisations. See `Polhem.Definition.Defaults` in [`src/Polhem.Definition/Defaults.cs`](../../src/Polhem.Definition/Defaults.cs).
    - **CLI** (recommended for CI / setup scripts): install once with `dotnet tool install -g Polhem.Cli` (upgrade later via `dotnet tool update -g Polhem.Cli`), then `dotnet polhem defines materialize --path ./Define` — thin shell over the same API. Run `dotnet polhem defines list` to see every embedded file, `dotnet polhem defines materialize --filter TableSchema/` to materialise a subset.
    - **Browse on GitHub**: every embedded default lives under [`src/Polhem.Definition/Defaults/`](../../src/Polhem.Definition/Defaults/) in this repo — open the file you care about, copy its contents into your `DefinePath`.
- **Framework updates that change `st_*` tables** are flagged as breaking changes in the [CHANGELOG](../../CHANGELOG.md). Renaming a table requires a manual `RENAME TABLE` — see [Table Schema Upgrade Guide §Renaming framework tables](database-schema-upgrade.md).

---

## See also

- [Database Naming Conventions](database-naming-conventions.md) — naming rules behind the `st_` / `ft_` split.
- [API Method Reference](api-method-reference.md) — full BO method catalogue.
- [Architecture Overview](architecture-overview.md) — how `st_*` tables fit into the broader N-tier + clean architecture.
- [ADR-019: Permission Authorisation Model](../adr/adr-019-permission-authorization-model.md) — why `st_role` / `st_user_role` / `st_employee` are framework-owned.
