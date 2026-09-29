# ADR-027: Data trail / audit log (the six-axis `st_log_*` design)

## Status

Accepted (2026-07-08)

## Context

A data trail is a core ERP requirement: "who logged in, who changed which field from what to what, who viewed which
sensitive record, which action / DB command went wrong" is the basis of compliance auditing, accountability and
performance tuning. The situation before the implementation:

| Aspect | Current state | Gap |
|------|------|------|
| The `log` database category | Exists (`DbCategoryIds.Log`), but `<Tables />` is empty | The framework ships no audit tables at all |
| Diagnostic logging | `ILogWriter` / `LogEntry` / `Tracer` / `TraceContext` | In-memory / UI oriented, **not persisted**, observability rather than business auditing |
| DB anomaly thresholds | `DbAccessAnomalyLogOptions` (`ExecutionTimeThreshold` and so on) | **Defined but with no consumer** (0 callers) |

The design draws on SAP (Security Audit Log, Change Documents `CDHDR`/`CDPOS`, Table Logging `DBTABLOG`, Read Access
Logging, System Log `SM21`, `SLG1`/`SM37`) and Odoo (Chatter `mail.tracking.value`, OCA `auditlog`, `ir.logging`,
`res.users.log`), simplified to fit this framework's DataSet-centric and FormSchema-driven architecture. It spans
`Polhem.Definition` (types / settings), `Polhem.Business` (BO instrumentation), `Polhem.Db` (DB anomalies),
`Polhem.Api.Core` (API anomalies) and `Polhem.Hosting` (background writing / DI), and is a structural contract of the
framework's external API surface, hence this ADR. This ADR collects the "why" and the rejected alternatives.

## Options considered

The following lists, one by one, the alternatives at the key decision points that "looked reasonable but were
rejected" (the adopted design is in the next section).

1. **The execution log records every `JsonRpcExecutor` call**: intuitive, full coverage of "who did what".
   **Rejected**: reads far outnumber writes, so recording everything explodes the volume, and it duplicates the login
   (item 1) / change (item 2) records. SAP (SAL's selective filter, STAD's short-term statistics, SLG1 opt-in) and
   Odoo (`ir.logging` opt-in, OCA per-rule, an expensive read mode) **both avoid recording everything**. Instead, only
   "what went wrong" is recorded = **anomaly records**.

2. **The change log uses per-field EAV (`st_log_change_field`, SAP `CDPOS` style) as the default**: field-level SQL
   queries / statistics. **Rejected as the default**: the row count = number of changes × number of fields, and it
   needs custom diff code; the field-level query power it buys is not needed by most ERP use ("see what was changed on
   a document"). Instead, a **single DataSet DiffGram column** is used (the framework-native `GetChanges()` records the
   old and new values of master+detail in one go, with zero custom diff, and can be displayed simply by restoring
   XML→DataSet). EAV is demoted to an optional "queryable mode".

3. **The change log uses a transactional outbox (strong consistency)**: business commit ⇔ log persisted.
   **Rejected**: implementing it needs a per-company outbox table, multi-tenant cross-database polling to flush, a
   change to the `IDataFormRepository.Save` signature, and writing inside the transaction; the cost is too high.
   Instead it is **best-effort and asynchronous** (the BO goes through the existing `IAuditLogWriter` after the commit;
   the window for loss is very small, and it can be narrowed further by forcing change entries to be synchronous).
   Upgrade to an outbox when a zero-loss requirement appears (additive).

4. **Make system / error events an observability audit table (`st_log_trace`, sending a copy of `Tracer` /
   `ITraceWriter` there)**: covers system events in one go. **Rejected**: `Tracer` / `TraceContext` are for
   **debugging the execution flow during development** and cannot be an audit source; system / error events are
   observability by nature (operations / debugging) and belong to `ILogWriter` / the host's `ILogger` (files / Seq /
   APM), separate from business auditing (aligned with SAP SM21 / Odoo `ir.logging`). Axis ⑤ is implemented instead
   as precisely defined **anomaly records**.

5. **Log rows store `user_rowid` (a normalized reference)**: saves columns, points to `st_user`. **Rejected**: the
   `log` database is **physically separate** from `common`/`company`, so cross-database joins are not feasible; a bare
   rowid only means something with a join. Instead the rows are **denormalized**, storing `user_id` + `user_name` and
   `company_id` + `company_name`, so every row is **self-contained**.

6. **Merge API / DB anomalies into one table (distinguished by a `layer` column)**: one table, one type fewer.
   **Rejected**: the two have different viewpoints and record different information (API = which action + who; DB =
   which `database_id` + command), and `DbAccess` has no session context and cannot get the who. Instead **API and DB
   get separate tables** (`st_log_anomaly_api` / `st_log_anomaly_db`), and the DB table is lean, with no who.

7. **Inject `IAuditLogWriter` directly into `DbAccessFactory`**: the most direct. **Rejected**: it forms the
   construction cycle `IDbAccessFactory → IAuditLogWriter → AuditLogDbSink → IDbAccessFactory`. Instead it is resolved
   lazily through `Func<IAuditLogWriter?>` (taken only in `Create()`), and the `DbAccess` of the log DB itself does no
   anomaly detection, to avoid recursion.

## Decision

Adopt an overall design of "a unified `IAuditLogWriter` for writing, six-axis `st_log_*` tables, opt-in, best-effort,
denormalized and self-contained". Core decisions:

- **D1: converging the six axes**: infrastructure + login (①) + change (③, with security ⑥ folded in through an
  `is_sensitive` flag) + view (②) + anomaly (④/⑤). Axis ⑤, system, is implemented as **anomaly records**, not an
  observability audit table.
- **D2: a unified writing abstraction, `IAuditLogWriter`**: an abstract base `AuditEntry` (with an overridable
  `AddCommonColumns` for the common columns) + typed subclasses; by default it writes in background batches (a bounded
  channel that degrades to synchronous when full, and falls back to a file when the log DB is unavailable), and writes
  synchronously and directly when there is no host. Placed in `Polhem.Definition.Logging`.
- **D3: `st_log_*` in the `log` category, opt-in**: `AuditLogOptions` (hung on `BackendConfiguration`) has an
  independent switch per axis, **all off by default**, so nothing regresses. 5 tables: `st_log_login` /
  `st_log_change` / `st_log_access` / `st_log_anomaly_api` / `st_log_anomaly_db`.
- **D4: log independence**: log rows are self-contained, and queries do not join; who / company are denormalized
  (**since 2026-07-30 the calling application is included too**: `api_key_id` + `api_key_name`, for the same reason:
  `st_api_key` is in `common`, and a cross-database join is not feasible. The four tables that carry who all have these
  columns; `st_log_anomaly_db` naturally does not, because it overrides `AddCommonColumns`); `log` can be split into
  databases by year (`log_YYYY`, the current year writable and past years read-only), and in the future the write
  target will be chosen by a resolver as the current year's writable DB (currently the fixed `DbCategoryIds.Log`).
- **D5: change = a single DataSet DiffGram table**: `st_log_change.changes_xml` stores the DiffGram of `GetChanges()`
  (the old and new values of master+detail). Two iron rules: **capture before `AcceptChanges` in `Save`**, and
  **always serialize with `DiffGram`** (a plain `WriteXml` drops the old values). `Delete` loads the record before
  deleting and stores the **complete before-image**.
- **D6: anomaly = five kinds, API/DB in separate tables**: `Error` / `Timeout` (separate from errors; an
  infrastructure/performance signal) / `Slow` / `LargeAffected` / `LargeResult`. Hooked into
  `JsonRpcExecutor.ExecuteAsyncCore` (API) and `DbAccess.Execute` (DB), implementing the existing thresholds of
  `DbAccessAnomalyLogOptions`.
- **D7: security**: complete SQL and parameter values are not recorded (only the `{0}` template is stored), and error
  messages are sanitized (no stack traces, no internal paths); the existing rules of `security.md` / `scanning.md`
  apply.

## Consequences

- **Positive**: a traceable trail of business data (login / change with old and new values and the delete
  before-image / view / anomaly); a consistent design that is opt-in, best-effort, denormalized and self-contained, and
  security-sanitized; volume under control (view logging driven by sensitivity, anomalies recording only problems).
- **Trade-offs**: the change log trades "field-level SQL query power" for "simplicity + restorable display" (the
  optional EAV can be turned on if needed); best-effort has a very small window for loss (upgrade to a transactional
  outbox if needed, with the entries / schema unchanged).
- **Related**: the registration of the system tables is in
  [framework-reserved-names §1.3](../../docs/en/framework-reserved-names.md); `DbScope.Log` routing is in
  [ADR-010](adr-010-logical-database-category.md); the DataForm Save pipeline is in
  [ADR-024](adr-024-dataform-save-dataadapter.md); the classification axes and write strategy of the audit trail,
  recorded afterwards, are in [ADR-040](adr-040-audit-trail-taxonomy.md).
- **To do**: ~~per-form audit rules~~ (**implemented, see [ADR-041](adr-041-per-form-audit-rule.md)**); DB anomaly
  detection for `ExecuteBatch` / `UpdateDataTables` (currently only the main `Execute` path); an existing SQL Server
  upgrade idempotency bug in `st_cache_notify` (a separate issue).

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

### 2026-08-07: the diagnostic logging types have changed

The `ILogWriter` / `LogEntry` mentioned in the "Context" table and in option 4 above **were removed in Phase 5
together with the dead Logging code of `BackendInfo`**
([`5037c128`](https://github.com/jeff377/bee-library/commit/5037c128) /
[`32f84941`](https://github.com/jeff377/bee-library/commit/32f84941)). The decision of this ADR is unaffected: at the
time of the decision those two types had already been classified as "not persisted, observability rather than
business auditing", and removing them only cleared out dead code.

The current diagnostics / tracing surface is `Tracer` / `TraceContext` / `ITraceWriter` (`src/Polhem.Base/Tracing/`)
plus the host's own `ILogger`. The text above is kept as it was to preserve the inventory at the time of the decision;
it is not a description of the current state.

### Later changes

- **2026-08-24: the write interface is split in two.** Anomaly producers write through `IAnomalyLogWriter`, and
  `IAuditLogWriter` carries the login, change and access entries; one write pipeline still implements both. The lazily
  resolved factory of option 7 is now `Func<IAnomalyLogWriter?>` (`src/Polhem.Db/DbAccessFactory.cs`). The reasons are
  in [ADR-040](adr-040-audit-trail-taxonomy.md) § 7.
- **2026-09-27: the tracing subsystem is removed.** `Tracer`, `TraceContext`, `ITraceWriter` and
  `src/Polhem.Base/Tracing/`, which the 2026-08-07 subsection names as the current diagnostics surface, no longer
  exist. Diagnostics go through the host's `ILogger`; for example `JsonRpcExecutor.Logger` records the real message of
  a failure that the remote caller only sees as a generic one.
- **2026-09-27: log rows store a token fingerprint.** The common columns include `token_fingerprint`
  (`AuditEntry.TokenFingerprint`, computed by `AccessTokenHasher.ComputeFingerprint`) instead of the access token, so
  a log row can still be correlated with its session without holding a usable credential
  (`src/Polhem.Definition/Logging/AuditEntry.cs`).
- **2026-09-27: the write pipeline.** The write repository persists a batch as one transactional `DbBatchSpec`
  instead of one commit per entry (`src/Polhem.Repository/AuditLog/AuditLogWriteRepository.cs`). The terminal writer behind the log database and
  the fallback file is the public `IAuditLogSink` (`src/Polhem.Hosting/Audit/IAuditLogSink.cs`), which a deployment can
  replace to ship records elsewhere.
