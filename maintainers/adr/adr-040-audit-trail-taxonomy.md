# ADR-040: Classification axes and write strategy of the audit trail

## Status

**Accepted (decided 2026-07-05; recorded as an ADR on 2026-08-20)**

The decision was made on 2026-07-05 and landed item by item; both the write side and the query side are complete.
This ADR is recorded after the fact: the original context was written in a parent plan, and a plan is a document for
one stage of work that is cleared once archived, so a lasting reason such as "why the audit trail is split into these
axes" should not live only there.

## Context

The framework originally had **only diagnostic logs, no trail of business data**:

- `ILogWriter` / `LogEntry`: system diagnostic output
- `TraceContext` / `Tracer`: request-level tracing, memory / UI oriented, not persisted

What was missing was "who logged in when, who viewed which sensitive record, who changed which field from what to
what, which call failed": that is business auditing, a different matter from technical observability. `DbScope.Log`
and the log database category were already in place at the time, but there was not a single table under them.

The design took **the mature approaches of SAP and Odoo as its blueprint**, because the difficulty of an audit
taxonomy lies not in the implementation but in "how many things to cut it into". The structure the two have in common:

| Concern | SAP | Odoo |
|--------|-----|------|
| Login / security events | Security Audit Log (`SM19`/`SM20`) | `res.users.log` |
| Field-level changes to business objects | Change Documents (`CDHDR` / `CDPOS`) | `mail.tracking.value`, OCA `auditlog` |
| Table-level changes (mostly for config) | Table Logging (`DBTABLOG`) | `auditlog` on ACL / groups |
| Sensitive data being **read** | Read Access Logging (`SRALMANAGER`) | `auditlog` read mode |
| Technical / system errors | System Log (`SM21`) | `ir.logging` |
| Batch / application processing messages | `SM37`, Application Log (`SLG1`) | `ir.logging`, server actions |

Three observations from this had a decisive influence on the design:

1. **"An event happened" and "what a field changed" are two sets of tables with two different volume
   characteristics**: SAP handles them separately with SAL and Change Documents, not as one table with an extra
   column.
2. **Read logging must be selective**. SAP RAL logs only data marked as sensitive, and Odoo states officially that
   logging every read costs too much and advises against enabling it for a whole model. Reads far outnumber writes, so
   logging everything inevitably explodes in volume.
3. **How to store before/after involves a real trade-off**: SAP `CDPOS` uses a single string column (generic, but type
   information is lost), while Odoo `mail.tracking.value` has a column per type (correctly typed, but a wide schema).

## Decision

### 1. Six classification axes, consolidated into four implementations

The analysis produced six axes (login / view / change / execution / system / security configuration), consolidated
into four items at implementation time:

| Implementation | Axes covered | Reason |
|--------|---------|------|
| Login log | (1) Login | — |
| Change log | (3) Change + (6) Security configuration | See decision 3 |
| Access log | (2) View | See decision 4 |
| Anomaly log | (4) Execution + (5) System | See decision 2 |

The common minimal field model: `who` (user) / `when` (UTC) / `what` (object + key + field, or the action name) /
`where` (method / channel / IP / session) / `before-after` (changes only) / `result`.

> The actual table and column names follow [Framework-Reserved Names](../../docs/en/reference/framework-reserved-names.md) §1 and the
> source code; this ADR does not copy them.

### 2. "Log every execution" is dropped in favor of an anomaly log

The original plan was to record every execution, which was overturned before implementation: the value of logging
everything lies mainly in the anomalous part, while the rest duplicates the login and change logs. Instead, only
**API and DB anomalies** are persisted (errors, timeouts, slowness) for bug tracking and performance tuning. Timeouts
and slowness are separate from errors: they are infrastructure / performance signals, not program defects.

Purely technical observability still goes through `ILogWriter` / the host's `ILogger` (file / Seq / APM), **separate
from business auditing**, matching the positioning of SAP `SM21` and Odoo `ir.logging`. `Tracer` / `TraceContext` are
development-time debugging tools and are not a source for auditing.

### 3. The security / configuration axis merges into the change log, with no separate table

Axis (6) (changes to permissions and settings) is in essence "something was changed", isomorphic to axis (3). It is
distinguished by the `is_sensitive` flag and by filtering on `prog_id`, rather than getting a separate table of the
same structure.

### 4. The access log is off by default and driven by sensitivity

This is the only one of the six axes that "cannot log everything":

1. **Off by default**, enabled by opt-in
2. **Driven by sensitivity**: only fields marked as sensitive are logged
3. **Limited entry points**: logging happens only for specified ProgIds / actions, not on every read

Sampling is only suitable for behavior analysis and **must not be used as compliance evidence**: compliance scenarios
usually require every access to sensitive data to be logged.

> **Implementation progress note (2026-08-26).** Of the three requirements in this item, "off by default" held from
> the start, while "limited entry points" and "driven by sensitivity" only landed with
> [ADR-041](adr-041-per-form-audit-rule.md), and **only at the form level**:
>
> | Decision 4 requirement | Current state |
> |-----------|------|
> | Off by default, opt-in | ✅ `AuditLogOptions.AccessEnabled` defaults to `false` |
> | Limited to ProgIds / actions | ✅ The ProgId dimension (`st_audit_rule`); **the action dimension is not done**: views are currently instrumented at a single point, `GetData`, so for now it makes no practical difference |
> | Driven by sensitivity | ⚠️ **Form level** (`st_audit_rule.is_sensitive`); **field level not done**: "log only fields marked as sensitive" requires changing the DiffGram filtering logic, handled separately |
>
> In other words, this item holds for **volume control** (it can be switched on or off per form), but the finer
> literal requirement **"log only sensitive fields"** is not yet complete. Judgements about compliance evidence depend
> on this difference.

### 5. before/after uses a single DataSet DiffGram column

Of the four candidates, the framework-native one was chosen: a DataSet's `GetChanges()` + DiffGram already keeps both
the old and new values, covers master + detail and multiple rows and columns in one go, needs no custom diff
algorithm, and can be restored into a DataSet on reading for direct display.

> The "restored into a DataSet" half **did not hold** in the original implementation, and only became true in 4.30.0
> when an embedded schema was added. The story and the rules for the two coexisting payloads are in "8. The payload
> carries an embedded schema" below.

The cost is that the field level cannot be queried or aggregated directly with SQL (the XML has to be parsed). Query
needs are carried by **physical columns in the header** (who / when / prog_id / row_key...); only when "field-level
statistics across records" are really needed is an optional EAV mode turned on for a specified table. This is
equivalent to the fast (default) / full (optional) levels of Odoo auditlog.

> **Iron rule**: serialization must use **DiffGram** (including the before block); a plain `WriteXml` writes only the
> current values and the old values are lost. Capture must also happen **before** `Save` applies `AcceptChanges`.

### 6. Writes are best-effort and asynchronous, not a transactional outbox

The original design was a transactional outbox: inside the business transaction an outbox row is written first
(committed in the same transaction, strongly consistent), then a background worker moves it to the log DB. It was
re-evaluated at implementation time and **overturned**: it needs an outbox table per company DB, cross-database flushes
for multiple tenants, and changes to repository signatures, a cost out of proportion to the benefit.

Instead, the BO goes through `IAuditLogWriter` after the commit, and change log entries can be forced to write
synchronously to narrow the loss window. **The outbox is kept as an upgrade path**: it is added when a real "zero loss"
requirement appears, and that change is additive.

> **Best-effort covers the whole audit step after the commit** (added 2026-09-11). Originally only the writer itself
> was best-effort, and exceptions thrown while building the payload or resolving the operator's identity went straight
> up to the caller. The consequence: **the data had been written, yet the API returned a failure**, the audit entry was
> not recorded, and AfterSave / AfterDelete and plugins were skipped. The actual trigger is quite ordinary: a user
> pastes a control character into a field, and XML serialization throws. The deployment-level `CreateApiKey` was worse:
> the key had been written, yet the caller never received the one and only copy of the secret part.
>
> Now the form's Save / Delete and the deployment-level operations wrap the whole audit step in a single guard: on
> failure it writes an error log (with the prog id, operation name and record key, but no field values), and the call
> completes normally. This does not change the loss window this decision accepts; it only makes a loss **leave a
> record**, and it no longer makes a completed write look like a failure as well.

### 7. The write interface is split in two along decision 2's boundary (added 2026-08-24)

Decision 2 classified "system / errors" as observability, separate from business auditing, but **the write side always
had just one `IAuditLogWriter`**: login / change / access logs and API / DB anomalies all went through it. The reason
they were combined at implementation time was **a shared write pipeline** (a bounded queue, batching, a fallback file,
and the log database's own `DbAccess` doing no anomaly detection), not that the two answer the same kind of question.

They were split after surveying the consumers: **the seven call sites divide cleanly along this boundary, and not one
writes both kinds**. The four in `Polhem.Business` write only audit entries, the three in `Polhem.Db` and
`Polhem.Api.*` write only anomalies, and the fields and parameters of the latter **were already called
`anomalyWriter`**, using naming to make a distinction the type system did not express.

| Aspect | Handling |
|------|------|
| Interfaces | `IAuditLogWriter` (takes `AuditEntry`) and `IAnomalyLogWriter` (takes `AnomalyEntry`) |
| Entry types | A new intermediate base `AnomalyEntry : AuditEntry`; `ApiAnomalyEntry` / `DbAnomalyEntry` now inherit it, and the five fields the two duplicated (`Kind` / `ElapsedMs` / `ThresholdMs` / `ErrorType` / `ErrorMessage`) move up |
| Write pipeline | **Not split**. The sink, write repository, queue, batching and fallback file are fully shared; one instance implements both interfaces |
| Switches | `AuditLogOptions` is **not split**. Splitting out separate anomaly options would change the structure of `SystemSettings.xml`, a breaking change every existing deployment would have to follow, and `AnomalyEnabled` is already separate |

> **The protection is one-way; do not read it as two-way.** `AnomalyEntry` inherits `AuditEntry` (the two share one
> write pipeline), so `IAuditLogWriter` can still accept an anomaly entry. The type system blocks only the other
> direction: **producers of anomalies cannot write login, change or access entries**. That is exactly the direction
> the risk calls for guarding. Making it two-way would require parallel bases, at the cost of duplicating the common
> fields and changing the public signature of `IAuditLogWriteRepository`, which is not worth it.

**Why who / company are not pushed down to the intermediate layer**: `ApiAnomalyEntry` has a session context and fills
the common columns as usual; only `DbAnomalyEntry` does not have one. It overrides `AddCommonColumns` to be empty, and
that stays as it is. What a shared structure has to decide is not which columns are common, but who may do without the
whole set.

**Not included this time**: the read side is still served entirely by `LogBusinessObject`, and its query methods
share the authorization of the reserved progId `AuditLog`. In an ERP, compliance auditing and operational
troubleshooting are two different roles, so splitting read permissions would be more valuable, but that is a question
for the permission model, not for the write interface, and is handled separately.

### 8. The payload carries an embedded schema (added 2026-09-09)

Decision 5 said one of the benefits of DiffGram is that "it can be restored into a DataSet on reading for direct
display", but at the time the write side produced **a bare DiffGram without a schema**, and reading such a payload back
with `DataSet.ReadXml` into a fresh `DataSet` yields **zero tables**; measured, all six `XmlReadMode`s behave the same.
The read side therefore had to parse it itself with `XDocument` and pair up before rows through `diffgr:id`. In other
words, **that benefit was never delivered**, and no mechanism would have noticed: the compiler does not read prose, and
the tests verified the read side's own path.

The fix is for the write side to write an embedded XSD before the DiffGram, both wrapped in a single outer element
`AuditChanges` (`DataSet.WriteXmlSchema` + `DataSet.WriteXml`, see `src/Polhem.Business/AuditLog/AuditDiffGram.cs`).
This way the payload carries its own column structure and can be rebuilt into a real `DataSet` with its own schema, and
the change details are derived by comparing `DataRowVersion.Original` with `Current`.

| Aspect | Decision |
|------|------|
| Old and new coexisting | Each kind of payload is dispatched by its **root element** (the new format `AuditChanges`, the old format `diffgr:diffgram`, the minimal deletion marker `DeletedRow`, the deleted original record `AuditDeletedRecord`: see section 10); they are mutually exclusive and need no version field |
| Existing data | **Not a single row is migrated**; the old format continues to be read by `SchemalessDiffGramReader`, **with no sunset date** |
| Size | The schema is a fixed cost (about +4.2 KB per row for the Northwind order set of 26 columns / 2 tables), independent of the data volume; change log entries are written to a separate `log` database and do not weigh on the business database |
| Stringifying values | Always `XmlConvert`, identical character for character to the XML text of the old format and culture-independent; using `ToString()` would make the same change display differently depending on the storage format |
| Characters XML does not allow (added 2026-09-11) | Control characters and U+FFFE / U+FFFF are written as character references (such as `&#x1;`), and the read side reads the original value back with character checking turned off; CR is written as `&#xD;` so that it is not normalized to LF on reading; a lone surrogate has no XML representation at all and is replaced with U+FFFD. The cost is that a payload containing such characters is not strictly valid XML 1.0, and external tools that parse `changes_xml` directly must turn off character checking |

**Three things deliberately not done**: the declared type (`Date` vs `DateTime`) is not tracked, because `FormSchema`
is the authoritative source of the column structure and the payload should not copy it again; `RecordFieldChange` is
not changed, so there is **no wire shape change**; the database level is not touched.

**Why not `XmlSerializer`.** `DataSet` implements `IXmlSerializable`, and its `WriteXml` is exactly the two BCL
methods above, so the payload `XmlSerializer` produces would be equivalent. Not going through it means the reflection
path concerns of [ADR-025](adr-025-define-types-aot-xmlserializer-compat.md) never need to be argued again. Also
verified along the way: the reader of `changes_xml` exists only on the server (`Polhem.Business` is not referenced by
any mobile / WASM head), and `changes_xml` never goes on the wire; the client receives the already flattened
`RecordFieldChange`.

### 9. The payload stays XML and does not switch to JSON (added 2026-09-11)

Storing the payload with the existing wire serialization's `DataSetJsonConverter` / `DataTableJsonConverter` was
evaluated. Measured with a Northwind order (16 master columns, 10 detail columns) and with a table having "one column
per `FieldDbType`, including extreme values and DBNull":

| Metric | JSON relative to the current XML |
|---|---|
| Size | 54% to 70%; 67% to 82% compared with unindented XML, so part of the gap comes from indentation |
| Serialization time | 24% to 30% |
| Restore time | 37% to 82% (the more modified rows, the smaller the gap) |
| Fidelity | For every `FieldDbType` (including inserts / updates / deletes and multiple languages) both restore completely, and the field change lists the read side produces are identical entry by entry |

The performance numbers were measured before section 8 added the handling of disallowed characters; that addition
leaves the payload for ordinary data unchanged character for character (measured), so the size and fidelity numbers
are unaffected.

**The decision is to stay with XML.** The reasons:

- **Fidelity is equivalent.** What the change log is viewed for is "which fields changed from what value to what
  value", and the two formats are equivalent on that point. After section 8 added the handling of disallowed
  characters, CR, CRLF, control characters and NUL were measured to read back as the original values in an XML round
  trip, and a lone surrogate becomes U+FFFD in both formats.
- **The performance gap is not a reason.** A change log entry writes or reads only a single form record at a time, and
  the serialization gap is on the order of tens to a little over a hundred microseconds each time.
- **The size gap is not a reason.** Change log entries are written to a `log` database separate from the business
  database. The framework currently always writes to a single `log` database, and when the volume grows the deployment
  can mitigate it by splitting databases or archiving.
- **The approach has been proven over a long time.** The maintainer has recorded changes as `DataSet` XML in existing
  systems for more than ten years.

Switching to JSON would instead cost:

- To store characters outside the BMP (emoji, CJK Extension B) as raw UTF-8, every built-in encoder of
  System.Text.Json escapes them, which needs a custom `JavaScriptEncoder`, and the methods it must override have
  pointer signatures, so unsafe code has to be enabled;
- Persisted data would from then on depend on the JSON shape of the wire, so changing the wire would affect whether
  existing audit rows can be read back;
- JSON only supports the CLR types corresponding to `FieldDbType` (`TimeSpan` and `DateTimeOffset` throw; `double` and
  `char` come back with a different type);
- The read side would get one more format branch, while the two existing XML formats must remain readable forever.

**Nor does `DateOnly` / `TimeOnly` need special handling.** Date columns are stored in the `DataSet` as `DateTime` and
Time columns as strings, and both the values and the column markers restore completely. Showing only the date for a
date column and converting time zones for an instant column are matters for the display layer to handle according to
the `FormSchema` field type, consistent with section 8's "the declared type is not tracked".

### 10. A deletion record stores the complete original record and no longer marks rows as Deleted (added 2026-09-11)

The actual deletion of `Form.Delete` is `DELETE … WHERE sys_rowid = …` (details are conditioned on `sys_master_rowid`)
and does not go through a DataSet. What the audit needs to record is **what the deleted record looked like**: a
deletion has no field changes.

The original implementation, however, marked every row of the original record as Deleted with `row.Delete()` before
deletion, then wrote it as a DiffGram with `GetChanges()`, so that the deleted content ended up in the
`diffgr:before` block. In other words, it disguised "a record that was deleted" as "a change set in which every row
was deleted", just to reuse the payload shape of the Save path.

The disguise had a real cost: the marking acted on `DeleteContext.Snapshot` itself, and `DoAfterDelete` and AfterDelete
plugins receive exactly that same object. **With auditing on, a plugin reading fields with the default version threw
`DeletedRowInaccessibleException`; with auditing off, the same code worked**: whether a plugin worked depended on the
audit switch.

| Aspect | Decision |
|------|------|
| Payload | Root `AuditDeletedRecord`, containing the XSD and the DiffGram. The original record is written as is, without `GetChanges()`; the rows stay Unchanged and there is no before block |
| Character handling | Shares the same writer settings as `AuditChanges`, so the character handling of section 8 applies as well |
| Reading | Each row produces `Delete` field entries from its current values (the old value is the original value); the output is identical entry by entry to the old deletion records, pinned by `DeletedRecordPayloadTests` |
| `Snapshot` | The audit only reads and never changes it, so the row state AfterDelete sees does not depend on the audit switch, pinned by `Delete_AfterDeletePlugin_ReadsSnapshotWithAuditEnabled` |
| Existing data | **Not a single row is migrated**. Deletion records marked as Deleted since 4.30.0 are still read |
| Save path | Unchanged: the DataSet the client sends carries real row states, and deleting a whole record is still a change set |
| Downgrade | An older read side does not recognize the new root and treats `xs:schema` as data, reading out one meaningless field; the data itself is intact and reads correctly again after returning to the new version |

## Rationale

**Why copy the taxonomy of two ERPs instead of inventing one.** The cost of an audit taxonomy is not in writing the
code but in discovering afterwards that it was cut wrong: by then the tables are full of data, and changing the
taxonomy means migrating data. The way SAP and Odoo cut it has been proven in long practice, and the two **converged
independently on the same structure** (events separated from field changes, selective read logging); that agreement is
itself evidence.

**Why the access log sacrifices completeness.** Because there is no choice: reads outnumber writes by one to several
orders of magnitude, and logging everything would drag down both performance and storage. SAP and Odoo each reached
the same conclusion independently; there is no third way.

**Why DiffGram beats the correctly typed option.** This is a trade-off between "dogfooding an existing mechanism" and
"query convenience". The framework's unit of data exchange is the DataSet already, and DiffGram is its native
representation of differences: choosing it introduces no new concept. Field-level queries are a minority scenario,
left to the optional EAV level.

## Consequences

**Positive**:

- The audit trail and technical observability belong to two separate pipelines, so their retention periods and volume
  strategies do not interfere with each other.
- The change log has zero custom diff logic (differences come from `GetChanges()`), and since 4.30.0 it can be
  restored into a DataSet for direct display.
- With the access log off by default, "turning on auditing" does not accidentally turn into a performance incident.

**Negative / costs**:

- Field-level queries on the change log require parsing XML, or switching to the optional EAV level.
- Best-effort writes have a loss window. This is deliberately accepted; when zero loss is needed, upgrade to the
  outbox.

**Later enhancement**: per-form audit rules have been implemented. Administrators use a runtime rule to choose which
ProgIds get change / access logging, matching Odoo `auditlog.rule`; see [ADR-041](adr-041-per-form-audit-rule.md).

## References

- The design direction for retention and partitioning (databases split by year, append-only, hash chain) is in the
  multi-database scenarios of the [database settings guide](../../docs/en/database/database-settings-guide.md).
- Related ADRs: [ADR-017](adr-017-db-cache-invalidation.md), [ADR-018](adr-018-db-define-storage.md),
  [ADR-019](adr-019-permission-authorization-model.md), [ADR-027](adr-027-audit-trail.md) (the earlier audit trail
  design this ADR classifies), [ADR-041](adr-041-per-form-audit-rule.md).

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **The diagnostic types named in the Context and in decision 2 are gone.** `ILogWriter` / `LogEntry` were removed as
  dead code (see [ADR-027](adr-027-audit-trail.md), Implementation evolution), and on 2026-09-27 the hand-rolled
  tracing subsystem (`Tracer` / `TraceContext`) was removed as well. Purely technical observability now goes through
  the host's `ILogger` only; the separation from business auditing that decision 2 makes is unchanged.
- **The optional EAV level was never built.** Decision 5 and the Consequences describe it as the way to get field-level
  statistics; nothing in `src/` implements it, and [ADR-041](adr-041-per-form-audit-rule.md) deliberately does not copy
  Odoo's full / fast levels. Field-level queries on the change log parse the DiffGram.
- **There is no per-entry synchronous write.** Decision 6 says change log entries can be forced to write synchronously.
  What exists is the deployment-wide switch `AuditLogOptions.UseBackgroundWriter` (`false` writes every entry
  synchronously), and the background writer falls back to a synchronous write when its bounded queue is full
  (`src/Polhem.Definition/Settings/SystemSettings/AuditLogOptions.cs`). The background writer persists each batch in
  one transaction (`src/Polhem.Repository/AuditLog/AuditLogWriteRepository.cs`).
- **2026-09-27: names on the read and write sides.** The read-side business object is now `AuditLogBusinessObject`
  (`src/Polhem.Business/AuditLog/`), still under the reserved progId `AuditLog`; its database anomaly queries require a
  deployment administrator (`DeploymentAction.ReadDbAnomalyLog`) rather than the company-scoped `AuditLog` read
  permission. The no-op writer is `NullLogWriter`, and the terminal sink behind the background and synchronous writers is
  the public, replaceable `IAuditLogSink` (`src/Polhem.Hosting/Audit/`). The log tables store a token fingerprint (`token_fingerprint`) instead
  of the access token.
