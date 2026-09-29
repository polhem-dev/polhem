# Architecture decision records (ADR)

[繁體中文](README.zh-TW.md)

← Back to the [documentation index](../en/README.md)

An ADR records **the context and the reasons at the time a decision was made**. It is the main source for
understanding why the design is the way it is.

> An ADR is not rewritten as the implementation evolves. When a decision is overturned, it is marked "Superseded" and
> points to the new ADR. When the implementation details drift but the decision still holds, an "Implementation
> evolution" section is added at the end and the original text is kept.
>
> When part of a decision is overturned and the rest still holds, the status is "Accepted, partially superseded". The
> Status section of the ADR names the part that no longer holds and what replaced it: a later ADR, or an entry in its
> Implementation evolution section.

> Polhem continues the Bee.NET framework under a new name. Most of these decisions were taken while it was Bee.NET, so
> a version number `4.x` in an ADR, and a pointer to "CHANGELOG 4.x", refers to a Bee.NET release, not to Polhem,
> whose changelog starts at 1.0.0. Those releases are described in the
> [Bee.NET changelog](https://github.com/jeff377/bee-library/blob/7d6cc9d9/CHANGELOG.md) and the per-version notes
> under `docs/changelogs/` in the same repository.

| # | Decision | Status |
|---|----------|--------|
| [001](adr-001-dataset-as-dto.md) | Use DataSet as the cross-layer DTO | ✅ Accepted |
| [002](adr-002-newtonsoft-json.md) | Choosing and migrating the JSON serialization library | 🔁 Superseded |
| [003](adr-003-static-service-locator.md) | Use a static Service Locator instead of dependency injection | 🔁 Superseded |
| [004](adr-004-messagepack-payload.md) | Use MessagePack as the API Payload serialization format | ✅ Accepted, partially superseded |
| [005](adr-005-formschema-driven.md) | FormSchema definition-driven architecture | ✅ Accepted |
| [006](adr-006-dual-target-framework.md) | Dual target framework strategy (netstandard2.0 + net10.0) | 🔁 Superseded |
| [007](adr-007-convention-based-type-resolution.md) | Derive API types automatically by naming convention | ✅ Accepted |
| [008](adr-008-polhem-db-namespace-layout.md) | Polhem.Db namespace layout: separating the syntax layer from the model layer | ✅ Accepted |
| [009](adr-009-cache-implementation.md) | Polhem.ObjectCaching adopts Microsoft.Extensions.Caching.Memory + IChangeToken | ✅ Accepted |
| [010](adr-010-logical-database-category.md) | Logical database categories (DbCategory) decouple database deployment flexibility | ✅ Accepted, partially superseded |
| [011](adr-011-di-replaces-service-locator.md) | Adopt DI to replace the static Service Locator | ✅ Accepted |
| [012](adr-012-session-company-context.md) | Session company context model (two-phase session lifecycle) | ✅ Accepted |
| [013](adr-013-frontend-api-connection-strategy.md) | Front-end API connection strategy — separate `Polhem.UI.*` and `Polhem.Web.*` families | ✅ Accepted |
| [014](adr-014-jsonrpc-plain-public-default.md) | Opening JSON-RPC `Plain` — `Public` as the default protection level, HTTPS as the trust boundary | ✅ Accepted |
| [015](adr-015-master-key-environment-default.md) | `MasterKeySource` defaults to `Environment` — aligning with 12-factor "config in env" | ✅ Accepted |
| [016](adr-016-multitenant-customization-overlay.md) | Multi-tenant customization overlay (two read-only layers stacked) | ✅ Accepted |
| [017](adr-017-db-cache-invalidation.md) | Database cache dependency and invalidation (notify table + polling + convention-based dispatch) | ✅ Accepted |
| [018](adr-018-db-define-storage.md) | Storing definitions in the database (a single `st_define` table of XML blobs) | ✅ Accepted |
| [019](adr-019-permission-authorization-model.md) | Permission authorization model (two-layer enforcement + record scope) | ✅ Accepted |
| [020](adr-020-avalonia-datagrid-binding-strategy.md) | How the Avalonia DataGrid binds to DataTable rows | ✅ Accepted |
| [021](adr-021-avalonia-datagrid-editing-strategy.md) | In-cell editing strategy for the Avalonia DataGrid | ✅ Accepted |
| [022](adr-022-avalonia-datagrid-cell-recycling.md) | Avalonia DataGrid list cells do not enable template recycling | ✅ Accepted |
| [023](adr-023-lookup-relation-mechanism.md) | A definition-driven lookup relation mechanism | ✅ Accepted |
| [024](adr-024-dataform-save-dataadapter.md) | DataForm persistence moves to a DataTable-level DataAdapter | ✅ Accepted |
| [025](adr-025-define-types-aot-xmlserializer-compat.md) | Definition types compatible with the AOT reflection XmlSerializer (a single Add + parameterless constructors) | ✅ Accepted |
| [026](adr-026-numeric-semantics-rounding.md) | Numeric semantics, company/currency/unit decimals, and round-then-sum | ✅ Accepted |
| [027](adr-027-audit-trail.md) | Data trail / audit log (the six-axis `st_log_*` design) | ✅ Accepted |
| [028](adr-028-expression-rule-engine.md) | Custom expressions and a rule engine (less hand-written BO code) | ✅ Accepted, partially superseded |
| [029](adr-029-lowercase-field-names.md) | Field names are always lowercase (consistent across the definition, data and UI layers) | ✅ Accepted |
| [030](adr-030-messagepack-name-based-keys.md) | MessagePack contracts switch to property-name keys (keyAsPropertyName) | ✅ Accepted, partially superseded |
| [031](adr-031-calendar-day-column-semantics.md) | Calendar-day column semantics are carried by an explicit marker, not by changing the CLR type | ✅ Accepted |
| [032](adr-032-datetime-timezone.md) | DateTime uses UTC as the single time zone source, and the Connector is the only conversion point | ✅ Accepted |
| [033](adr-033-time-of-day-semantics.md) | Time-of-day semantics (`FieldDbType.Time`) carried as a fixed-width string | ✅ Accepted |
| [034](adr-034-progid-type-registry.md) | ProgramSettings as the framework-wide type registry | ✅ Accepted |
| [035](adr-035-business-logic-plugin.md) | Business logic plugins (hooking into the existing flow rather than replacing the whole BO) | ✅ Accepted |
| [036](adr-036-wire-serialization-externalized.md) | Wire serialization moves out to the API layer; the definition layer no longer carries MessagePack | ✅ Accepted, partially superseded |
| [037](adr-037-wire-explicit-registration.md) | Every wire type registers a formatter explicitly; `object` values use a discriminated envelope | ✅ Accepted |
| [038](adr-038-definition-dependency-boundary.md) | The definition layer's dependency boundary: the expression abstraction moves down to `Polhem.Base`, and the criterion is enforced by gates | ✅ Accepted |
| [039](adr-039-formlayout-design-time-only.md) | `FormLayout` returns to design time; the runtime no longer derives it from `FormSchema` | ✅ Accepted |
| [040](adr-040-audit-trail-taxonomy.md) | Classification axes and write strategy of the audit trail | ✅ Accepted |
| [041](adr-041-per-form-audit-rule.md) | Per-form audit rules: change and access logging are configured form by form | ✅ Accepted |
| [042](adr-042-api-replay-protection.md) | API replay protection: a wire frame inside the encrypted envelope | ✅ Accepted |
| [043](adr-043-error-contract-single-registry.md) | The error contract is expressed as a single registry that both ends consume from one declaration | ✅ Accepted |
| [044](adr-044-payload-codec-negotiation.md) | The body codec is declared by each request; JSON and MessagePack coexist | ✅ Accepted |
| [045](adr-045-language-policy-and-local-plans.md) | English for everything maintained together; plans stay out of the repository | ✅ Accepted, partially superseded |
| [046](adr-046-api-evolution-policies-for-1-0.md) | API evolution policies for 1.0: a synchronous server path, growable host interfaces, process-wide configuration | ✅ Accepted |
| [047](adr-047-documents-split-by-reader.md) | Documents are split by reader: user documents are multilingual, maintainer documents are English only | ✅ Accepted |
