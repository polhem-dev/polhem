# Polhem Documentation

[繁體中文](../zh-TW/README.md)

The `docs/` folder contains the public-facing developer documentation for the Polhem framework. Every document listed below exists in English and Traditional Chinese at the same path: `docs/en/` holds the English versions and `docs/zh-TW/` the Traditional Chinese ones, with the same subfolders. English is the source; other languages are translations of it.

The first line of every translation is an HTML comment recording which version of the source it was translated from (the source file's git blob hash). When the source changes and the translation is not re-checked and its comment updated, [`check-docs-i18n.sh`](../../check-docs-i18n.sh) reports the translation as stale. Whether a stale translation fails CI or is only reported depends on the language; that policy lives in the script's header.

The listing is ordered by **where you are in the journey**, not by subject. Each entry is tagged with its **kind** (Tutorial / Concept / Guide / Reference). If you would rather browse by subject, see [Find by Topic](#find-by-topic) at the bottom.

**Reading paths**

- **First contact** → read the three documents under [Start Here](#1-start-here); that is enough to build something.
- **Want to understand the trade-offs** → add [Concepts](#2-concepts).
- **Stuck mid-build** → look up the matching document under [Guides](#3-guides).
- **Writing fields, naming things, calling an API** → go straight to [Reference](#4-reference).

---

## 1. Start Here

Read these three and you can build your first application.

| Document | Kind | Description |
|----------|------|-------------|
| [Getting Started](getting-started/getting-started.md) | Tutorial | Build your first Polhem backend from scratch: packages, `DefinePath`, DI wiring, your first form and business object, then calling it from a client |
| [Architecture Overview](architecture/architecture-overview.md) | Concept | Definition-Driven Architecture: the design philosophy and the practical patterns behind it |
| [Definition Files Overview](definitions/definition-files-overview.md) | Concept | The map of every definition file: what each one owns, how they connect, and what changing one affects |

> Unfamiliar term? Keep [Terminology](reference/terminology.md) open in another tab.
>
> Want the bird's-eye list of what the framework already does for you? See [Framework Capabilities](getting-started/framework-capabilities.md).

## 2. Concepts

Why the framework is built the way it is.

| Document | Kind | Description |
|----------|------|-------------|
| [FormSchema-Driven Database Access](definitions/formschema-data-access.md) | Concept | How Polhem.Db generates SQL dynamically from a FormSchema, and why it is not an ORM |
| [API ↔ BO Contract Design](api/api-bo-contract-design.md) | Concept | Three-tier API contract separation (Contracts / API Type / BO Type) and the naming conventions that drive it |
| [Project Dependency Map](architecture/dependency-map.md) | Concept | How the `src/` projects depend on each other, and the rules that keep the graph acyclic |
| [Caching](guides/caching.md) | Concept | How definition and database-backed caches work: the read path, the invalidation signals, and the notification-table mechanism for cross-process / multi-node deployments |

## 3. Guides

How to actually do a thing.

| Document | Kind | Description |
|----------|------|-------------|
| [End-to-End Development Cookbook](guides/development-cookbook.md) | Guide | The core development flow from definition to API: initialization order, request pipeline, ExecFunc pattern, cache invalidation |
| [Expressions and Rules](definitions/expression-rules.md) | Guide | Declarative field computation and pre-save / pre-delete validation in FormSchema, instead of hand-written BO code |
| [Tenant Customization](definitions/customization.md) | Guide | Giving one company different labels, a different form arrangement or extra behaviour, without forking the base definitions: which mechanism to reach for, how each one is written, and what cannot be customized |
| [Permission & Authorization](security/permission-authorization.md) | Guide | The two-layer authorization model (action gate + record scope): PermissionModels, `FormField.ScopeRole`, the role/grant tables, read filtering and authoritative write-side re-query — plus the separate deployment-level axis for installation-wide assets |
| [API Key Management](security/api-key-management.md) | Guide | What an API key identifies (the calling application, not the user), how the gate turns itself on, who may manage keys, and the rotation procedure |
| [JSON-RPC Frontend Integration](api/jsonrpc-frontend-integration.md) | Guide | Calling the JSON-RPC API from a JavaScript / TypeScript frontend with no .NET on the client: wire format, auth flow, TypeScript wrapper |
| [Wire Contract](../../wire-contracts/README.md) | Reference | The TypeScript contract generated from the message types — what a non-.NET client codes against |
| [Wire Fixtures](../../wire-fixtures/README.md) | Reference | Golden body samples for every wire message, to check a client implementation against |
| [DatabaseSettings & DbCategorySettings Guide](database/database-settings-guide.md) | Guide | Structure, access patterns and runtime behaviour of the two database-related settings files |
| [Database Schema Upgrade](database/database-schema-upgrade.md) | Guide | Synchronising definition changes to a live database: the diff → plan → execute pipeline, ALTER vs rebuild, dry runs |
| [Platform Support](getting-started/platform-support.md) | Guide | The supported heads (desktop, browser, iOS, Android, Blazor Server), the trim and AOT configurations that work, and the checklist for a browser or mobile head |

## 4. Reference

Look things up while you work.

| Document | Kind | Description |
|----------|------|-------------|
| [Framework Capabilities](getting-started/framework-capabilities.md) | Reference | Single-page catalogue of every mechanism the framework provides, grouped by area, one line each |
| [Terminology](reference/terminology.md) | Reference | English ↔ Chinese term reference, organised by layer |
| [API Method Reference](api/api-method-reference.md) | Reference | Every BO method exposed through JSON-RPC on one page, with its `[ApiAccessControl]` settings and purpose |
| [Framework-Reserved Names](reference/framework-reserved-names.md) | Reference | Registry of the `st_*` system tables and reserved `progId`s owned by the framework |
| [Database Naming Conventions](database/database-naming-conventions.md) | Reference | Naming rules for tables, columns, indexes and system fields; cross-database case-sensitivity reference |
| [Database Dialect Differences (DDL)](database/database-dialect-differences.md) | Reference | Cross-dialect DDL rules and exceptions (defaults, nullability, quoting, AutoIncrement); why text and numeric columns are NOT NULL |
| [Temporal Types: Date, DateTime and Time](database/temporal-types.md) | Reference | Choosing between the three, and how each is carried in the database, the `DataSet`, code and all three serialization formats |
| [Time Zones](database/datetime-timezone.md) | Reference | UTC storage, where conversion happens, configuring a user's zone, and what hand-written SQL and non-.NET clients must do |
| [Analyzer Rules](reference/analyzer-rules.md) | Reference | The build diagnostics shipped with the packages: rule list, how to adjust severity, versioning policy |
| [Development Constraints and Anti-Patterns](architecture/development-constraints.md) | Reference | Framework constraints and forbidden practices; also useful as a reference for AI coding tools |

## 5. Deep Dive

| Folder | Description |
|--------|-------------|
| [`maintainers/adr/`](../../maintainers/adr/README.md) | Architecture Decision Records — the primary source for *why* a design is the way it is. The index lists every ADR with its status (accepted / superseded). Written for maintainers, in English only |
| [`changelogs/`](changelogs/) | Per-version change detail behind the root `CHANGELOG.md` |

---

## Find by Topic

The same documents, grouped by subject. A document appearing under several topics is intentional.

| Topic | Documents |
|-------|-----------|
| **Database** | [Naming Conventions](database/database-naming-conventions.md) · [Reserved Names](reference/framework-reserved-names.md) · [Settings Guide](database/database-settings-guide.md) · [Schema Upgrade](database/database-schema-upgrade.md) · [Dialect Differences](database/database-dialect-differences.md) · [FormSchema-Driven Access](definitions/formschema-data-access.md) |
| **Definition layer** | [Definition Files Overview](definitions/definition-files-overview.md) · [Architecture Overview](architecture/architecture-overview.md) · [Expressions and Rules](definitions/expression-rules.md) · [Reserved Names](reference/framework-reserved-names.md) |
| **Multi-tenancy** | [Tenant Customization](definitions/customization.md) · [Definition Files Overview](definitions/definition-files-overview.md) · [Development Cookbook](guides/development-cookbook.md) · [Caching](guides/caching.md) |
| **Caching & performance** | [Caching](guides/caching.md) · [Development Cookbook](guides/development-cookbook.md) · [Development Constraints](architecture/development-constraints.md) |
| **Client heads & platforms** | [Platform Support](getting-started/platform-support.md) · [JSON-RPC Frontend Integration](api/jsonrpc-frontend-integration.md) · [Development Cookbook](guides/development-cookbook.md) |
| **API & frontend** | [Contract Design](api/api-bo-contract-design.md) · [API Method Reference](api/api-method-reference.md) · [JSON-RPC Frontend Integration](api/jsonrpc-frontend-integration.md) · [Permission & Authorization](security/permission-authorization.md) · [API Key Management](security/api-key-management.md) |
| **Types & time** | [Temporal Types](database/temporal-types.md) · [Time Zones](database/datetime-timezone.md) |
| **Quality & conventions** | [Analyzer Rules](reference/analyzer-rules.md) · [Development Constraints](architecture/development-constraints.md) · [Naming Conventions](database/database-naming-conventions.md) |

---

## Other Folders

Excluded from the listing above; consult them directly when needed.

- **`maintainers/`** (at the repository root) — Documents for the people who maintain Polhem: the ADRs, operational documentation (CI / branch protection) and pitfall logs. English only; apart from the ADRs, not relevant to framework users.
