# ADR-010: Logical database categories (DbCategory) decouple database deployment flexibility

> **Note**: the `BackendInfo.GetDatabaseItem(databaseId)` examples in this document are now equivalent to
> `IDatabaseSettingsProvider.GetItem(databaseId)` (DI constructor injection). The design idea is unchanged.

## Status

Accepted (2026-05-10), partially superseded: the category ids are fixed to `common`, `company` and `log`, and
`FormSchema.CategoryId` is read at runtime, which overturns key point 1 of the Decision and its "not involved at
runtime" (see "Implementation evolution").

## Context

Enterprise application systems usually contain tables with several purposes: system tables shared across companies
(users, sessions), business data kept separate per company, and frequently written audit / operation records. For
physical deployment these tables have two typical needs:

- **Consolidated deployment**: all tables in a single physical database, to save operating cost (small and medium
  implementations)
- **Distributed deployment**: split into different physical databases by purpose (for example, the business DB uses
  master-replica replication and the log DB is tuned separately for writes)

The same system definition also often faces multi-company / multi-tenant deployment and cross-environment deployment
(dev / staging / prod) at the same time, and the number and names of the physical databases change accordingly.

If the framework had only the concept of a "physical database" (that is, `DatabaseSettings.Items` maps directly to
physical DBs), it would run into three structural problems:

1. **Schema deployment has nothing to go on**: table creation / upgrade tools need to answer "which tables should this
   physical DB contain". Without a category dimension, tools can only scan all schema files and classify them by name
   prefix or hardcoded rules.
2. **Deployment flexibility is hardcoded by callers**: without framework support, the "consolidated" or "distributed"
   decision means every project writes its own dispatch logic (which tables are written to which DB).
3. **Business code is sensitive to deployment details**: physical DB names (such as `erp_acme_main_v2`) change with
   the company and the environment. If business code binds directly to physical DB names, deploying across
   environments requires code changes or large-scale string replacement.

What is needed is an intermediate layer that is **stable, environment-independent and expresses purpose**, fully
decoupling "the purpose category of the data" from "the physical deployment settings".

## Decision

Introduce `DbCategory` (logical database category), declared centrally in `DbCategorySettings.xml`, as the mapping
layer for physical deployment. `DbCategory` and `DatabaseItem` are mapped **many-to-one** by string, used only during
schema design and deployment, and not involved at runtime.

### Four key points

1. **DbCategory is a purely logical abstraction**

   DbCategory only answers "**which tables does this category contain**"; it does not map to any physical connection.
   Three customary categories are provided by default, but they are **not hardcoded in the framework**:

   | Id | Purpose |
   |----|---------|
   | `common` | System tables shared across companies (such as users and sessions) |
   | `company` | Business data, kept separate per company |
   | `log` | Frequently written audit / operation records |

   The category definitions are controlled entirely by `DbCategorySettings.xml`, and a project can define its own
   categories.

2. **A DatabaseItem is the physical carrier of a logical category; one category can map to several DatabaseItems**

   `DatabaseItem.CategoryId` is a single string declaring which logical category's tables this physical connection
   carries. But `DbCategory` and `DatabaseItem` have a **many-to-one** relationship: one category can have several
   DatabaseItems mapped to it, each pointing to a different physical DB, while the table structure in every one of
   those DBs is exactly the same (all taken from `DbCategory[cid].Tables`). Common triggering situations:

   - **A single physical carrier** (such as `common`): 1 DatabaseItem
   - **Multi-tenant split** (such as `company`): N companies have N DatabaseItems (`company001`, `company002`...), each
     pointing to that company's own physical DB
   - **Split by archive period** (such as `log`): split by year into several DatabaseItems (`log_2024`,
     `log_2025`...), each pointing to that year's own physical DB
   - The two split dimensions can be stacked (for example, a category split by both company and year)

3. **The number of physical databases and the deployment split are entirely free**

   Combined with the design above, DatabaseSettings can express several deployment shapes:

   - **Consolidated deployment**: 1 DatabaseItem per category, all with the same DbName → all category tables live
     together in one physical DB
   - **Distributed deployment**: 1 DatabaseItem per category, each with its own DbName → one physical DB per category
   - **Several carriers for one category**: add DatabaseItems to a category along a split dimension (tenant / time /
     other), each pointing to its own physical DB

   Business code is unaware of the deployment shape: it always gets connections through
   `BackendInfo.GetDatabaseItem(databaseId)`, and the business layer decides which `databaseId` to pass according to
   the current context (tenant, time, other dimensions).

4. **At runtime, connections are obtained only by `DatabaseItem.Id`, never through CategoryId**

   `BackendInfo.GetDatabaseItem(databaseId)` fetches the DatabaseItem directly by Id and creates the connection.
   CategoryId and DbCategorySettings play no part at runtime at all.

> **Side note**: FormSchema also carries a `CategoryId` property, but FormSchema itself remains a pure description of
> the form structure and is not directly tied to a database. This `CategoryId` is needed purely at design time: when
> the schema editing tool derives a TableSchema from a FormSchema, it tells the tool which category folder under
> `TableSchema/{categoryId}/` the result belongs in. It does not affect the core design of this ADR (the decoupling
> between DbCategory and DatabaseItem).

## Rationale

### Why a logical abstraction layer instead of using physical DBs directly

Letting business code / schema tools work directly with the concept of a physical DB tangles three things together:
"purpose category", "physical split" and "deployment environment":

- Changing a physical DB name (for example `erp_v2` for a new environment) affects the schema tools and the business
  code
- Changing between "consolidated or distributed" requires changes at every call site that works with a DB
- In a multi-company deployment there is no uniform abstraction to express "this is some company's business DB"

Once DbCategory pulls out "category purpose", the three things "structure definition / category declaration /
physical deployment" no longer interfere with each other and each evolves independently. The meaning of a logical
category (`company` is always company data) is stable across environments; deployment-site information (concrete
DatabaseId names such as `erp_acme_main`) exists only in DatabaseSettings.

### Why a string mapping instead of strong typing

`DbCategory.Id` is a string, and a DatabaseItem maps to it by string value. Alternatives considered:

- **enum**: hardcoded in the framework, which defeats the goal of "categories are controlled by settings"; projects
  could not extend it
- **Strongly typed reference** (such as `[XmlReference]`): System.Xml.Serialization does not support resolving
  references across files, and a hand-written resolver is highly complex

The advantages of a string mapping:
- **Native XML serialization support**: written directly with `[XmlAttribute]`, no custom parsing needed
- **Cross-layer decoupling**: `Polhem.Definition.Settings.DatabaseItem` and `Polhem.Definition.Settings.DbCategory`
  do not need to reference each other; they map only through the string value
- **NoCode / LowCode friendly**: definition files can be edited by non-engineers, and string values are intuitive

## Outcome

### The mapping, settled

```text
DatabaseItem.CategoryId  ──►  DbCategory.Id  (in DbCategorySettings)
                                 └─ Tables  (the list of tables in the category)

DatabaseItem.Id          ──►  The entry point through which business code gets connections (not through CategoryId at runtime)
```

### Roles across the three stages

| Stage | Subject | Purpose |
|-------|---------|---------|
| Schema design | `FormSchema.CategoryId` (secondary use) | When deriving a TableSchema, indicates which category folder under `TableSchema/{cid}/` it belongs in |
| Deployment / table creation | `DatabaseItem.CategoryId` | For each DatabaseItem, derives the list of tables to create (look up `DbCategory.Tables` → take the structure from `TableSchema/{cid}/` → run DDL on the physical DB that the DatabaseItem's connection points to) |
| **Runtime** | **Not used** | Connections are obtained directly by `DatabaseItem.Id`, entirely unrelated to CategoryId and DbCategorySettings |

### Examples of deployment flexibility

Suppose the logical categories are `common` / `company` / `log`, N is the number of tenants (companies), and Y is the
number of archived log years:

| Mode | Number of DatabaseItems | Connection configuration | Number of physical DBs |
|------|-------------------------|--------------------------|------------------------|
| Consolidated deployment | 3 (1 per category) | The three share the same DbName / Server | 1 (containing all tables) |
| Distributed deployment | 3 (1 per category) | Each of the three has its own DbName | 3 |
| Multi-tenant deployment | 2 + N (common 1 + company N + log 1) | N items in the `company` category, mapped to `company001`, `company002`... | 2 + N |
| Log archived by year | 2 + Y (common 1 + company 1 + log Y) | Y items in the `log` category, mapped to `log_2024`, `log_2025`... | 2 + Y |
| Multi-tenant + log archiving | 1 + N + Y | The two dimensions stacked | 1 + N + Y |

Business code is unaware of the deployment shape; the only difference is "how the business layer decides which
`databaseId` to pass":

- **Consolidated / distributed**: a fixed mapping (category → DatabaseId) that can be written as constants
- **Multi-tenant**: derived from the current tenant ID (such as `$"company{tenantId:D3}"`)
- **Log archived by year**: derived from the current year (for writes, `$"log_{DateTime.UtcNow.Year}"`) or from the
  queried year range (aggregating across several DatabaseItems)

**These are the typical basic patterns; in practice they can be combined freely.** For example, in a multi-tenant
setup, to keep logs from concentrating into a performance bottleneck, "each tenant's company and log can share the
same physical DB" (the DbNames of the two DatabaseItems `company001` + `log_company001` both point to the `company001`
physical DB). **Logical categories and physical deployment are two independent dimensions**; the framework does not
restrict how they are combined, and the deployment designer decides the split strategy according to data volume,
query patterns and operating cost.

### TableSchema folder layout

File layout:
```text
<DefinePath>/TableSchema/
              ├── common/
              ├── company/
              └── log/
```

Deployment scripts can process the folders in batches (for example, sync only the schemas of the `company` category).

### External API changes

- Added `DatabaseItem.CategoryId` (commit [`f4cc1bd7`](https://github.com/jeff377/bee-library/commit/f4cc1bd7)),
  default value `""`
- Existing `DatabaseSettings.xml` files do not have to be migrated; but to enable validation in the future (see
  "Trade-offs"), every existing Item should get a CategoryId

## Trade-offs

### Compile-time relationship checks are lost

A string mapping cannot detect typos at compile time (such as `commen` instead of `common`). The current mitigation:

- `CacheDefineAccess.SaveFormSchema` checks that `FormSchema.CategoryId` is **not empty** (through
  `TableSchemaGenerator.GetCategoryId`)
- **But it does not currently check whether the CategoryId actually exists in `DbCategorySettings`**

Existence validation is a known trade-off. A `DbCategoryValidator` could be added in the future to validate everything
before files are written, so that a wrong category Id is not written into a definition file and the deployment /
table creation stage does not end up unable to find the matching category.

### Selecting the connection at runtime is left to the business layer

This is the cost of "why many-to-one is allowed": when one category has several DatabaseItems
(multi-tenant, archive by period and similar situations), "which DatabaseItem a business operation should use" has to
be decided by the business layer according to the current context. This ADR does not prescribe that selection logic,
but the following points should be kept in mind in design:

- When business code accesses several categories (for example, reading users from common and employees from company
  at the same time), it needs to hold the context of the current situation (tenant ID, operation time and so on)
- The framework provides no mechanism to manage the context → databaseId mapping; the business layer maintains it
  itself
- Aggregating across several DatabaseItems (such as querying logs across several years) requires the business layer
  to coordinate several connections, fetch the data and merge it
- DbCategorySettings is completely independent of the split dimensions; adding a tenant or archiving a new year needs
  no change to the category definitions

### The `DbCategory.Tables` child nodes are a documentary index

Each `DbCategory` in `DbCategorySettings.xml` has a `Tables` child node, which currently serves as a documentary index
of "the tables registered under this category". It has no automatic synchronization with the actual files under
`TableSchema/{cid}/` or with the FormTables of FormSchema. Strict consistency would need a separate rule.

## Scope of impact

| Scope | Impact |
|-------|--------|
| `Polhem.Definition.Settings.DbCategorySettings` | Defines all logical categories centrally; `DbCategorySettings.xml` is the single source of truth |
| `Polhem.Definition.Settings.DatabaseItem` | New `CategoryId` field (commit [`f4cc1bd7`](https://github.com/jeff377/bee-library/commit/f4cc1bd7)) |
| `Polhem.Definition.Forms.FormSchema` | As a side effect must declare `CategoryId`, otherwise SaveFormSchema rejects it |
| `Polhem.Definition.PathOptions.GetTableSchemaFilePath` | The path gains a `categoryId` segment |
| `Polhem.ObjectCaching.CacheDefineAccess.SaveFormSchema` | Checks that CategoryId is not empty before writing the file |

## Later extension: runtime routing (DbScope + IRepositoryDatabaseRouter, 2026-05-15)

This ADR originally stated explicitly that "at runtime, connections are obtained only by `DatabaseItem.Id`, never
through CategoryId", but it left unprescribed "how the business layer decides the `databaseId` from the current
context". `DbScope` + `IRepositoryDatabaseRouter` fill in this layer, taking shape together with the session company
context model of [ADR-012](adr-012-session-company-context.md):

### The `DbScope` enum: a bo repo's runtime access intent

`schema.CategoryId` is a schema attribute (XML configuration), while `DbScope` is a runtime intent (a decision in
code). The two are **conceptually completely decoupled**, even though their values currently correspond one to one.
`DbScope` provides a type-safe enum in place of magic strings:

```csharp
namespace Polhem.Definition;

public enum DbScope { Common, Company, Log }
```

### `IRepositoryDatabaseRouter`: the single source of resolution

The mapping from `DbScope` → `databaseId` is performed in one place by
`IRepositoryDatabaseRouter.Resolve(scope, accessToken)`:

| `DbScope` | Resolution path |
|-----------|-----------------|
| `Common` | Always `"common"`, no accessToken needed |
| `Log` | Always `"log"`, no accessToken needed (so that pre-EnterCompany methods such as `Login` / `Logout` can also write the audit log) |
| `Company` | accessToken → `SessionInfo.CompanyId` → `CompanyInfo.CompanyDatabaseId` |

In a multi-company setup, several companies can share the same `CompanyDatabaseId` string (for example, several small
companies sharing the `"biz_shared_01"` physical DB); separating their rows then relies on a company column on the
tables, which the framework's form repositories neither add nor filter by. The router treats the "several companies on one databaseId" and "separate databaseId" setups
exactly the same: the flexibility is decided by the CompanyInfo settings, and the routing logic itself does not
change.

### Connecting to FormSchema

A repository created by `IRepositoryFactory.CreateFormRepository<T>(accessToken, progId)` converts `schema.CategoryId`
into a `DbScope` at construction time, then calls the router to resolve the actual databaseId:

```text
schema.CategoryId (string)
    ↓ RepositoryFactory.ParseCategoryId(string)
DbScope (enum)
    ↓ IRepositoryDatabaseRouter.Resolve(DbScope, accessToken)
databaseId (string, DatabaseItem.Id)
```

The BO side does not need to worry about this layer: `BusinessObject` adds two protected helpers,
`ResolveDatabaseId(DbScope)` and `CreateDataFormRepository(progId)`, which pass in the current `AccessToken`
automatically.

### Runtime selection among several `DatabaseItem`s per category

This ADR originally pointed out that "when one category has several carriers, the business layer decides which
DatabaseItem a business operation should use". Once the session model was settled
([ADR-012](adr-012-session-company-context.md)), this selection logic took concrete form:

- **Several carriers for the `company` category (a separate DB per company)**: `EnterCompany` writes
  `SessionInfo.CompanyId`, and the router then takes the matching DatabaseItem from `CompanyInfo.CompanyDatabaseId`
- **Several carriers for the `log` category (such as archives by year, `log_2024` / `log_2025`)**: this ADR only
  covers the "current active log" (always `"log"`); querying archived data is a separate topic, handled by a custom
  bo repo that explicitly passes the databaseId of the archive year
- **The `common` category**: always a single databaseId (`"common"`); there is no several-carrier scenario

### `CompanyInfo.LogDatabaseId` removed

When `CompanyInfo` landed in P1, it originally had a `LogDatabaseId` field, in anticipation of some companies wanting
a separate log DB. But it was later decided that `DbScope.Log` is always `"log"`, to support writing logs before
EnterCompany, so `LogDatabaseId` became a dead field and was removed. Log isolation between companies is handled at
row level by the `company_id` column of the log tables, with no need for physical DB isolation.

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **2026-09-27: the category ids are fixed.** Key point 1 says the three categories are not hardcoded and that a
  project can define its own. The current code accepts only `common`, `company` and `log`:
  `RepositoryFactory.ParseCategoryId` (`src/Polhem.Repository/Factories/RepositoryFactory.cs`) throws for any other
  `FormSchema.CategoryId`, and the analyzers report other ids at build time (POLHEM1001 for a FormSchema, POLHEM1002
  for a category in DbCategorySettings). The many-to-one mapping between a category and its DatabaseItems is unchanged.
- **2026-09-27: `CategoryId` at runtime.** The Decision, the side note under it and "Roles across the three stages"
  limit `CategoryId` to design and deployment. `FormSchema.CategoryId` is also read at runtime, where it selects the
  `DbScope` a form repository routes through (see "Later extension: runtime routing" above); connections are still
  obtained by `DatabaseItem.Id`.
- **2026-09-27: validation.** No `DbCategoryValidator` was added. Unknown category ids are reported by POLHEM1001 and
  POLHEM1002 and refused at runtime as described above, and POLHEM2001 reports a FormSchema table that is not
  registered under its category, so the `DbCategory.Tables` child nodes are no longer only a documentary index. The
  rules are listed in [Analyzer rules](../../docs/en/analyzer-rules.md).

## Related documents

- [ADR-005: FormSchema definition-driven architecture](adr-005-formschema-driven.md)
- [ADR-012: Session company context model](adr-012-session-company-context.md) — the session model that
  `DbScope.Company` routing depends on
- [DatabaseSettings & DbCategorySettings Guide](../../docs/en/database-settings-guide.md) — structure and operational
  details
