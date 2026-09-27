# DatabaseSettings & DbCategorySettings Guide

[繁體中文](../zh-TW/database-settings-guide.md) · [← Docs Index](README.md)

> This document explains the structure, positioning, access patterns, and runtime behavior of the two database-related settings files in the Polhem framework, helping developers understand the full chain of settings → connection → category routing.

## Table of Contents

1. [Overview](#1-overview)
2. [DatabaseSettings](#2-databasesettings)
3. [DbCategorySettings](#3-dbcategorysettings)
4. [Access Entry Points & Caching](#4-access-entry-points--caching)
5. [CategoryId Wiring](#5-categoryid-wiring)
6. [File Locations & Examples](#6-file-locations--examples)

---

## 1. Overview

The two settings files together support the chain of "FormSchema definition → logical category → physical connection". Their responsibilities split as follows:

| Settings File | Question Answered | Entities |
|---------------|-------------------|----------|
| **DatabaseSettings** | What "physical" database connections exist in the system? | DatabaseServer (server config) + DatabaseItem (connection items) |
| **DbCategorySettings** | What "logical" database categories exist? Which tables belong to each? | DbCategory (category) + TableItem (table registry) |

### Wiring Diagram

```text
FormSchema.CategoryId ─────┐
                           │
                           ├──► DbCategory.Id  (in DbCategorySettings)
                           │       └─ Tables  (tables registered for this category)
DatabaseItem.CategoryId ───┘

DatabaseItem.ServerId  ────► DatabaseServer.Id  (in DatabaseSettings.Servers)
```

Key concepts:
- **FormSchema.CategoryId** declares which logical category the form's tables belong to. At design time it decides the TableSchema output directory; at runtime it decides which scope (`DbScope`) the form's repository routes through (§5.4).
- **DatabaseItem.CategoryId** is used at deployment time: it declares which logical category the physical connection belongs to, used to derive "which tables this physical DB should contain".
- **DbCategory** is the common target of both. Its `Id` is one of the three framework categories `common`, `company` and `log` (the [`DbCategoryIds`](../../src/Polhem.Definition/Database/DbCategoryIds.cs) constants); see §3.4 for what rejects other values.
- ⚠️ **At runtime, a connection is looked up by `DatabaseItem.Id` alone; neither `DatabaseItem.CategoryId` nor `DbCategorySettings` is read.**

### Logical vs Physical: Mapping Model

`DbCategory` is a **pure logical abstraction** — it only defines "which tables this category contains"; it does not correspond to a number of physical databases or specify where they should be deployed. `DatabaseItem.CategoryId` and `DbCategory.Id` form a **many-to-one** relationship — the same category can have multiple DatabaseItems. Common scenarios:

- **Single physical carrier** (e.g. `common`): 1 DatabaseItem
- **Multi-tenant partitioning** (e.g. `company`): N companies have N DatabaseItems (`company001`, `company002`...), each pointing to a separate physical DB for that company
- **Time-based archival partitioning** (e.g. `log`): partitioned by year with multiple DatabaseItems (`log_2024`, `log_2025`...), each pointing to a separate physical DB for that year
- The two partitioning dimensions can stack (e.g. partition a category by both company and year)

Multiple DatabaseItems may point to the same physical DB (consolidated deployment) or to separate physical DBs (distributed or multi-carrier deployment).

Four typical deployment scenarios (all using the three logical categories common / company / log as examples):

**Scenario 1: Consolidated deployment (one physical DB contains all three categories)**

```text
DatabaseSettings.Items (3 entries)                                Physical DBs (1)
─────────────────────────────                                     ────────────────
DatabaseItem  Id="common"   CategoryId="common"   DbName=erp ──┐
DatabaseItem  Id="company"  CategoryId="company"  DbName=erp ──┼──► erp (contains the common and
DatabaseItem  Id="log"      CategoryId="log"      DbName=erp ──┘     company tables, business tables
                                                                     such as ft_project, and the
                                                                     st_log_* log tables)
```

> `st_log_*` = the framework's opt-in log tables — the audit trail (`st_log_login`, `st_log_change`, `st_log_access`) plus the execution-anomaly records (`st_log_anomaly_api`, `st_log_anomaly_db`); the later scenarios abbreviate them as "log tables". "Common tables" and "company tables" likewise mean the framework tables registered under those categories. The tables of each category are listed in [`src/Polhem.Definition/Defaults/DbCategorySettings.xml`](../../src/Polhem.Definition/Defaults/DbCategorySettings.xml); see also [Framework-Reserved Names §1](framework-reserved-names.md#1-system-tables-st_).

**Scenario 2: Distributed deployment (three physical DBs, one per logical category)**

```text
DatabaseSettings.Items (3 entries)                  Physical DBs (3)
─────────────────────────────                       ────────────────
DatabaseItem  Id="common"   CategoryId="common"   DbName=erp_common  ──► erp_common  (common tables)
DatabaseItem  Id="company"  CategoryId="company"  DbName=erp_company ──► erp_company (company tables, ft_project)
DatabaseItem  Id="log"      CategoryId="log"      DbName=erp_log     ──► erp_log     (log tables)
```

**Scenario 3: Multi-tenant deployment (1 entry each for shared categories + 1 entry per company for company)**

```text
DatabaseSettings.Items (2 + N entries)              Physical DBs (2 + N)
─────────────────────────────                       ────────────────
DatabaseItem  Id="common"      CategoryId="common"   ──► erp_common
DatabaseItem  Id="company001"  CategoryId="company"  ──► company001  (company tables, ft_project)
DatabaseItem  Id="company002"  CategoryId="company"  ──► company002  (company tables, ft_project)
DatabaseItem  Id="company003"  CategoryId="company"  ──► company003  (company tables, ft_project)
   ⋮          (one entry per company)
DatabaseItem  Id="log"         CategoryId="log"      ──► erp_log
```

The `companyXXX` physical DBs all have identical table structures (all derived from `DbCategory["company"].Tables`), but their data is separate. Which entry a company uses is not derived from its id: each company's row in `st_company` names its entry in the `company_database_id` column.

**Scenario 4: log archived by year (one log entry per year)**

```text
DatabaseSettings.Items (2 + Y entries)              Physical DBs (2 + Y)
─────────────────────────────                       ────────────────
DatabaseItem  Id="common"     CategoryId="common"   ──► erp_common
DatabaseItem  Id="company"    CategoryId="company"  ──► erp_company
DatabaseItem  Id="log_2024"   CategoryId="log"      ──► log_2024  (log tables)
DatabaseItem  Id="log_2025"   CategoryId="log"      ──► log_2025  (log tables)
DatabaseItem  Id="log_2026"   CategoryId="log"      ──► log_2026  (log tables)
   ⋮          (new entry per year)
```

The `log_YYYY` physical DBs all have identical table structures (all derived from `DbCategory["log"].Tables`). The application writes using the DatabaseId for the current year, and queries can aggregate across multiple DatabaseItems. Scenarios 3 and 4 can stack (e.g. partitioning by both company and year).

> ⚠️ Year-based entries serve **application-owned** log tables that the application routes itself (§5.4). The framework's own `st_log_*` writes and reads go through `DbScope.Log`, which always resolves to the entry with `Id="log"`; a deployment that turns on `AuditLogOptions` needs that entry in addition to any `log_YYYY` entries.

The application is unaware of the deployment shape: it always retrieves connections through `IDatabaseSettingsProvider.GetItem(databaseId)` (DI ctor injected). The only difference is how the application layer decides which `databaseId` to pass:

- Scenarios 1, 2: fixed mapping (category → DatabaseId)
- Scenario 3: looked up from the session's company: `SessionInfo.CompanyId` → `ICompanyInfoService` → `CompanyInfo.CompanyDatabaseId` (§5.4)
- Scenario 4: derived by the application from the current year (e.g. `$"log_{DateTime.UtcNow.Year}"`) for its own log tables; cross-year queries aggregate multiple entries

**Scenarios can be freely combined**: the four above are basic patterns — actual deployments can combine them in any way to meet cost / performance / operational needs. For example, in a multi-tenant scenario, to avoid "log centralizing all tenants" becoming a performance bottleneck, you can switch to "**company and log share the same physical DB per tenant**":

```text
DatabaseSettings.Items                                Physical DBs
─────────────────────────                             ────────────
DatabaseItem  Id="common"           CategoryId="common"   ──► erp_common
DatabaseItem  Id="company001"       CategoryId="company"  ──┐
DatabaseItem  Id="log_company001"   CategoryId="log"      ──┴► company001  (contains both ft_* and log_* tables)
DatabaseItem  Id="company002"       CategoryId="company"  ──┐
DatabaseItem  Id="log_company002"   CategoryId="log"      ──┴► company002  (contains both ft_* and log_* tables)
   ⋮          (2 DatabaseItems per company, sharing the same physical DB)
```

Each company has 2 DatabaseItems declaring company and log categories respectively, but their DbName points to the same physical DB — the physical DB contains both ft_* and log_* tables. Log data is distributed across tenants, avoiding cross-tenant centralization. As with Scenario 4, this applies to log tables the application routes itself: the framework's `st_log_*` tables stay on the single `Id="log"` entry, because `CompanyInfo` carries no per-company log database id.

Key principle: **Logical categories and physical deployment are two independent dimensions, freely combinable**. Choose the partitioning strategy based on data volume, query patterns, and operational cost. The framework's own routing uses the entries `common` and `log` and the `company_database_id` each company names (§5.4); any other entry is for the application to route.

---

## 2. DatabaseSettings

Definition location: [`src/Polhem.Definition/Settings/DatabaseSettings/`](../../src/Polhem.Definition/Settings/DatabaseSettings/)

### 2.1 Structure

```text
DatabaseSettings
├── Servers : DatabaseServerCollection   shared server configurations (connection templates)
│     └── DatabaseServer
└── Items   : DatabaseItemCollection     actual database connection entries
      └── DatabaseItem
```

### 2.2 DatabaseServer Fields

Defines a "shared server configuration" that multiple DatabaseItems can reference to share connection templates and credentials.

| Field | Type | Purpose |
|-------|------|---------|
| `Id` | string | Server identifier (Key) |
| `DisplayName` | string | Display name |
| `DatabaseType` | DatabaseType | `SQLServer` / `PostgreSQL`, etc. |
| `ConnectionString` | string | Connection string template, can contain `{@DbName}` / `{@UserId}` / `{@Password}` placeholders |
| `UserId` | string | Login id, replaces `{@UserId}` |
| `Password` | string | Login password, replaces `{@Password}`; encrypted when saved (§2.6) |

### 2.3 DatabaseItem Fields

Defines a "physical connection entry" — the unit that actually establishes connections at runtime.

| Field | Type | Purpose |
|-------|------|---------|
| `Id` | string | Connection identifier (Key); callers retrieve connections by this id |
| `CategoryId` | string | Logical category id this entry belongs to (corresponds to `DbCategory.Id`) |
| `DisplayName` | string | Display name |
| `DatabaseType` | DatabaseType | `SQLServer` / `PostgreSQL`, etc. (ignored when `ServerId` is set; the Server's applies) |
| `ServerId` | string | Referenced DatabaseServer Id (optional) |
| `ConnectionString` | string | Standalone connection string (ignored when `ServerId` is set) |
| `DbName` | string | Database name, replaces `{@DbName}` |
| `UserId` | string | Login id (overrides Server settings) |
| `Password` | string | Login password (overrides Server settings); encrypted when saved (§2.6) |

> 📌 **DatabaseItem and logical category are many-to-one**: `CategoryId` is a single string, not a collection — each DatabaseItem belongs to exactly one category; but **the same category can have multiple DatabaseItems**, with common scenarios including multi-tenant partitioning (e.g. one company per entry under company) and time-based archival partitioning (e.g. one year per entry under log). See [§1 Logical vs Physical: Mapping Model](#logical-vs-physical-mapping-model).

### 2.4 Choosing Server vs Item

Two usage patterns:

- **Reference a Server**: `DatabaseItem.ServerId` points to a Server; the connection string template and `DatabaseType` come from the Server, and the Item only supplies `DbName` and (when needed) overrides `UserId` / `Password`. Suitable when multiple Items share the same server but use different DBs. A `ServerId` that names no Server is not caught when the file is loaded; it fails when the first connection for that Item is built.
- **Standalone configuration**: `ServerId` is left blank; specify `ConnectionString`, `UserId`, `Password` directly on the Item. Suitable for a single connection or when connection settings vary significantly.

### 2.5 Connection String Template Substitution

The connection string can use three placeholders that the framework substitutes when establishing connections ([`ConnectionStringTemplate.Resolve`](../../src/Polhem.Db/Manager/ConnectionStringTemplate.cs)):

| Placeholder | Replacement Source |
|-------------|--------------------|
| `{@DbName}` | `DatabaseItem.DbName` |
| `{@UserId}` | `DatabaseItem.UserId` (falls back to `DatabaseServer.UserId` if empty) |
| `{@Password}` | `DatabaseItem.Password` (falls back to `DatabaseServer.Password` if empty) |

The template is parsed as a connection string and written back with `DbConnectionStringBuilder`, so a substituted value is quoted as the connection string grammar requires: a password containing `;` or `=` stays one value instead of ending the pair. Placeholders match case-insensitively and may sit inside a value (for example `Data Source=file:app_{@DbName}.db`). A placeholder whose value is empty is left in place. Because the whole string is rewritten when it contains a placeholder, keywords may come back in lower case and values quoted; both are ordinary connection string syntax.

Example:
```xml
<DatabaseServer Id="sql_main" DatabaseType="SQLServer"
                ConnectionString="Server=sql.example;Database={@DbName};User ID={@UserId};Password={@Password};" />
<DatabaseItem Id="company_main" CategoryId="company" ServerId="sql_main"
              DbName="erp_company" UserId="erp_user" Password="..." />
```

### 2.6 Password Encryption

`DatabaseServer.Password` and `DatabaseItem.Password` are encrypted in `DatabaseSettings.xml`. They are the only encrypted fields: the rest of the file, including the connection string template, `DbName` and `UserId`, is stored in plain text, so keep the password out of `ConnectionString` and supply it through `{@Password}`.

- **Encryption**: AES-CBC-HMAC with the `ConfigEncryptionKey` of `SystemSettings` (`BackendConfiguration.SecurityKeySettings`, itself stored encrypted under the master key). `AddPolhemFramework` decrypts it and passes it to `CacheDefineAccess` through its constructor.
- **Storage format**: `enc:` prefix + Base64-encoded ciphertext
- **Timing**: `CacheDefineAccess` runs the cryptor, not the XML serializer:
  - `SaveDatabaseSettings` encrypts every plain-text Password (`EncryptInPlace`) on a copy and writes the copy; the instance you pass in keeps its plain-text passwords. Values that already start with `enc:` pass through.
  - `GetDatabaseSettings` decrypts Passwords starting with `enc:` (`DecryptInPlace`). A value that fails to decrypt (bad Base64, HMAC mismatch) becomes an empty string. A plain-text password written into the file by hand is used as it is, and stays plain text until the settings are saved through `SaveDatabaseSettings`.
- **Without a key**: if `ConfigEncryptionKey` is empty, encryption and decryption are skipped: passwords are stored in plain text and `enc:` values are never decrypted. When the settings contain any password, `CacheDefineAccess` logs a warning the first time it loads them and every time it saves them. Use this only in development.

Implementation: [`DatabaseSettingsCryptor.cs`](../../src/Polhem.Definition/Settings/DatabaseSettings/DatabaseSettingsCryptor.cs) — `EncryptInPlace` / `DecryptInPlace`.

---

## 3. DbCategorySettings

Definition location: [`src/Polhem.Definition/Settings/DbCategorySettings/`](../../src/Polhem.Definition/Settings/DbCategorySettings/)

### 3.1 Structure

```text
DbCategorySettings
└── Categories : DbCategoryCollection
      └── DbCategory
            └── Tables : TableItemCollection
                  └── TableItem
```

### 3.2 DbCategory Fields

| Field | Type | Purpose |
|-------|------|---------|
| `Id` | string | Category identifier (Key); FormSchema / DatabaseItem map by this id |
| `DisplayName` | string | Display name (e.g. "Common database") |
| `Tables` | TableItemCollection | Table registry under this category |

### 3.3 TableItem Fields

| Field | Type | Purpose |
|-------|------|---------|
| `TableName` | string | Table name (Key) |
| `DisplayName` | string | Display name (e.g. "User") |

The `Tables` child node is the **registry of which tables belong to this category**. It is what a deployment step reads to know which tables to create in a database of that category (§5.3), and analyzer POLHEM2001 warns when a form's table is not registered under the form's category (see [Analyzer Rules](analyzer-rules.md)). The runtime does not read it, and the table structure itself comes from the `TableSchema` and `FormSchema` files.

### 3.4 The Three Categories

The framework recognises exactly three logical categories, the constants of [`DbCategoryIds`](../../src/Polhem.Definition/Database/DbCategoryIds.cs):

| Category Id | Purpose | Examples |
|-------------|---------|----------|
| `common` | Shared database — system tables shared across companies | `st_user`, `st_session`, `st_company` |
| `company` | Company database — business data, separate per company | `st_department`, `st_employee`, business tables such as `ft_project` |
| `log` | Log database — audit trail and execution anomalies, with frequent writes | `st_log_login`, `st_log_change` (opt-in), application log tables |

The framework's own tables and the category each one belongs to are listed in [`src/Polhem.Definition/Defaults/DbCategorySettings.xml`](../../src/Polhem.Definition/Defaults/DbCategorySettings.xml), the file the framework ships; start from it instead of writing the framework's tables by hand. [Framework-Reserved Names](framework-reserved-names.md) explains what each table is for.

No other category id is accepted for a form or a `DbCategory` (a `DatabaseItem.CategoryId` is not checked by anything):

- At build time, analyzers POLHEM1001 and POLHEM1002 report an error for a `FormSchema/@CategoryId` or `DbCategory/@Id` outside the three, in the definition files the build sees (see [Analyzer Rules § Where the definition file rules read from](analyzer-rules.md#where-the-definition-file-rules-read-from)).
- At runtime, building the repository of a form whose `CategoryId` is anything else throws `InvalidOperationException` ("Unknown schema.CategoryId").

Custom tables, including an application's own log tables, are registered under one of the three categories.

**`common` is a framework contract**: the framework's system services (sessions, users, companies, API keys, cache notifications) connect to the fixed `databaseId = "common"`, so a deployment needs a `DatabaseItem` with `Id="common"`, and by convention its `CategoryId` is `common` too. Nothing checks this at startup: a missing entry fails the first time one of those services connects (the lookup throws `KeyNotFoundException`). A host that wants to fail fast can call `IDatabaseSettingsProvider.ValidateRequired()` once its service provider is built; it throws when there is no `common` entry.

`company` and `log` are the other framework categories. The framework ships opt-in `st_log_*` tables in `log` (off by default via `AuditLogOptions`) and reaches them through the entry with `Id="log"` (§5.4). A single-tenant setup may leave out the `log` entry only while auditing and anomaly logging stay disabled.

---

## 4. Access Entry Points & Caching

### 4.1 Unified Entry

Both settings are accessed through `IDefineAccess` (DI ctor injected):

```csharp
public class MyService(IDefineAccess defineAccess)
{
    public void Demo()
    {
        // Read
        DatabaseSettings dbSettings = defineAccess.GetDatabaseSettings();
        DbCategorySettings catSettings = defineAccess.GetDbCategorySettings();

        // Write
        defineAccess.SaveDatabaseSettings(dbSettings);
        defineAccess.SaveDbCategorySettings(catSettings);
    }
}
```

`IDefineAccess` is registered as a singleton during `AddPolhemFramework`, defaulting to `CacheDefineAccess`, which reads through `IDefineStorage`. Swap either one through the XML `Components` registry — a database-backed `IDefineStorage` is the supported way to move definitions off the file system. `DbCategorySettings` follows the storage; `DatabaseSettings` does not: `CacheDefineAccess` reads and writes it at `<DefinePath>/DatabaseSettings.xml` (§6.1) whatever the storage, because a database-backed storage needs it to connect in the first place. Clients do not implement `IDefineAccess`; they fetch definitions over the API through `ClientDefineAccess`.

### 4.2 Caching

Both settings are held centrally by the DI-registered [`ICacheContainer`](../../src/Polhem.ObjectCaching/ICacheContainer.cs) (default implementation `CacheContainerService`). The holders are the cache objects themselves; loading is lazy per key on a cache miss (the underlying `ObjectCache<T>` calls `CreateInstance()` the first time a key is requested):

| Cache | Holder |
|-------|--------|
| `DatabaseSettings` | `ICacheContainer.DatabaseSettings` (`DatabaseSettingsCache`) |
| `DbCategorySettings` | `ICacheContainer.DbCategorySettings` (`DbCategorySettingsCache`) |

Behavior:
- **20-minute sliding expiration**: reloaded if not accessed for 20 minutes
- **Change detection on read**: a read compares the source with what was loaded and reloads when it changed. For a file source that is the file's last-write time, checked at most once per second per entry; for `DbCategorySettings` in a database-backed storage it is that entry's cache-notify version. An edit made outside the process is therefore picked up by the next read after it, not pushed.
- **Save-then-invalidate**: calling `Save*` immediately clears the corresponding cache; the next `Get*` reloads

### 4.3 Common Lookups

```csharp
// Get a single connection entry (DI-injected IDatabaseSettingsProvider)
DatabaseItem item = dbSettingsProvider.GetItem("company_main");

// Get all tables under a category (via the indexer)
DbCategory company = catSettings.Categories!["company"];
foreach (var table in company.Tables!) { ... }
```

`IDatabaseSettingsProvider.GetItem` throws `KeyNotFoundException` when the id is not found, allowing callers to detect unknown connections.

### 4.4 API Access Restrictions

| Settings | Accessible via remote API? |
|----------|----------------------------|
| DatabaseSettings | ❌ No. A local call receives the file as stored, with passwords still in their `enc:` form |
| DbCategorySettings | ❌ No |

`SystemBusinessObject.GetDefine` serves a remote caller only the definition types a client needs to render forms and menus, and refuses every other type; both settings files are outside that list. A local call (an in-process `LocalApiProvider`, for example tooling) may read them, which is how `ClientDefineAccess.GetDbCategorySettingsAsync` works. Saving goes through `SaveDefine`, which is local-only. The allow-list itself is in the XML documentation of `SystemBusinessObject.GetDefine`.

---

## 5. CategoryId Wiring

`CategoryId` is the **central correlation key** of this design, threading through three layers:

### 5.1 FormSchema Definition Phase

Every FormSchema must declare its category:

```xml
<FormSchema ProgId="Project" CategoryId="company" ...>
  <FormTable TableName="Project" DbTableName="ft_project" ...>
    ...
  </FormTable>
</FormSchema>
```

When persisting, [`CacheDefineAccess.SaveFormSchema`](../../src/Polhem.ObjectCaching/CacheDefineAccess.cs) enforces that `CategoryId` is non-empty (via [`TableSchemaGenerator.GetCategoryId`](../../src/Polhem.Definition/Database/TableSchemaGenerator.cs)); otherwise it throws `InvalidOperationException`.

### 5.2 TableSchema Output Path

TableSchemas derived from FormSchemas are stored in directories grouped by CategoryId:

```text
<DefinePath>/TableSchema/
              ├── common/
              │     ├── st_user.TableSchema.xml
              │     └── ...
              ├── company/
              │     ├── st_employee.TableSchema.xml
              │     ├── ft_project.TableSchema.xml
              │     └── ...
              └── log/
                    ├── st_log_login.TableSchema.xml
                    └── ...
```

The framework's own TableSchema files ship under [`src/Polhem.Definition/Defaults/TableSchema/`](../../src/Polhem.Definition/Defaults/TableSchema/) in the same layout.

Path resolution: [`PathOptions.GetTableSchemaFilePath(categoryId, tableName)`](../../src/Polhem.Definition/PathOptions.cs) (DI ctor injected).

### 5.3 Deployment Phase: Deriving the Table List for Each Physical DB

`DatabaseItem.CategoryId` is used at the schema deployment phase (when creating or upgrading physical database table structures). The framework supplies the pieces (the category registry, the TableSchema files, and the schema comparison and upgrade API described in [Database Schema Upgrade](database-schema-upgrade.md)) but no runner that walks the DatabaseItems: that step belongs to the host, for example the schema seeder of the Northwind demo (`apps/Polhem.Northwind/Polhem.Northwind.Server/NorthwindSchemaSeeder.cs`). **The unit of computation is DatabaseItem**: for each DatabaseItem, the derivation and DDL flow runs once, applying to the physical DB pointed to by that DatabaseItem's connection.

Derivation flow for a single DatabaseItem:

1. Read `DatabaseItem.CategoryId` (e.g. `"company"`)
2. Get the registered table list from `DbCategorySettings.Categories["company"].Tables`
3. For each `TableName`, load the physical table structure from `<DefinePath>/TableSchema/company/{TableName}.TableSchema.xml`
4. Execute DDL on the physical DB through this DatabaseItem's connection (table creation or schema upgrade)

```text
[DatabaseItem]            [DbCategorySettings]              [TableSchema files]
 └─ CategoryId ─────────► DbCategory[id]                    └─ TableSchema/{cid}/{table}.xml
   ("company")             └─ Tables (tables for category)      └─ provides table structure
```

Because derivation runs independently per DatabaseItem, both deployment scenarios (single physical DB vs multiple physical DBs, see §1) are handled identically:

- **Scenario 1** (3 DatabaseItems all pointing to the same physical DB): derivation runs 3 times, all connecting to the same physical DB, building the common / company / log tables coexisting in that DB.
- **Scenario 2** (3 DatabaseItems pointing to 3 separate physical DBs): derivation runs 3 times, building tables on each respective physical DB; each physical DB only contains the tables for its category.

This design fully decouples schema definitions (which tables, what structure) from physical deployment (whether to split, how many DBs). Adding a new logical category requires only changes to `DbCategorySettings.xml` and the corresponding FormSchema; changing the physical deployment layout requires only changes to the connection info of each DatabaseItem in `DatabaseSettings.xml`, leaving the definition files untouched.

### 5.4 Runtime: Database Access

⚠️ **At runtime, retrieving a connection goes entirely through `DatabaseItem.Id` and is completely independent of `DbCategorySettings`**:

```csharp
DatabaseItem item = dbSettingsProvider.GetItem(databaseId);
// Use item.ConnectionString / DbName / UserId / Password to establish the connection and run SQL
```

Application code chooses `databaseId` based on "which logical category the data belongs to" + "the current context (tenant, time, etc.)":

| Data to Access | Category | Deployment Scenario | DatabaseId Used |
|----------------|----------|---------------------|-----------------|
| Common tables (`st_user`, `st_session`, ...) | common | Any | Fixed string `"common"` (framework contract: `DatabaseItem.Id == "common"`) |
| Company tables (`st_employee`, business tables, ...) | company | Single company | The `company_database_id` of the company's `st_company` row, normally the one entry with `CategoryId=company` |
| Company tables | company | Multi-tenant | The `company_database_id` of the session's company (`SessionInfo.CompanyId` → `CompanyInfo.CompanyDatabaseId`) |
| Framework log tables (`st_log_*`) | log | Any | Fixed string `"log"` |
| Application log writes | log | Year-based archival | Chosen by the application, e.g. the `log_YYYY` for the current year (`$"log_{DateTime.UtcNow.Year}"`) |
| Application log cross-year queries | log | Year-based archival | Multiple DatabaseIds for the year range, queried separately and aggregated |

Regardless of the underlying scenario, the application always uses the same entry `IDatabaseSettingsProvider.GetItem(databaseId)`; the only difference is "how to derive the databaseId string from the current context".

For bo repos (the BO-layer Repositories) the framework provides `IRepositoryDatabaseRouter` (see [ADR-010 § "Later extension: runtime routing"](../adr/adr-010-logical-database-category.md)) so that BO code does not have to derive the databaseId by hand:

| Source | How the databaseId is derived |
|--------|------------------------------|
| `DbScope.Common` | Fixed string `"common"` (no session required) |
| `DbScope.Log` | Fixed string `"log"` (no session required; `Login` / `Logout` etc. can write audit log pre-EnterCompany) |
| `DbScope.Company` | `SessionInfo.CompanyId` (set by `EnterCompany`) → `CompanyInfo.CompanyDatabaseId` (looked up from `ICompanyInfoService`) |

BO methods consume the router via `BusinessObject.ResolveDatabaseId(DbScope)` or, for FormSchema-driven CRUD, the `CreateDataFormRepository(progId)` helper which routes automatically based on the schema's `CategoryId`.

Cross-DatabaseItem aggregation (e.g. log year-range queries) and explicit archive access (e.g. reading `log_2024`) are not part of the default routing path — the application layer specifies the target databaseId directly and creates a custom bo repo via `IDbAccessFactory`.

`DatabaseItem.CategoryId` and `DbCategorySettings` are used only at the design phase (§5.1–5.2) and the deployment phase (§5.3) described above. At runtime, BO methods speak in terms of `DbScope` (the runtime access intent) rather than CategoryId strings; the one place a CategoryId string is read at runtime is a form repository turning its schema's `CategoryId` into a `DbScope` (§3.4).

---

## 6. File Locations & Examples

### 6.1 File Paths

Both settings files are located at the root of `PathOptions.DefinePath`:

| Settings | File Path | Resolution |
|----------|-----------|------------|
| DatabaseSettings | `<DefinePath>/DatabaseSettings.xml` | `PathOptions.GetDatabaseSettingsFilePath()` |
| DbCategorySettings | `<DefinePath>/DbCategorySettings.xml` | `PathOptions.GetDbCategorySettingsFilePath()` |

### 6.2 DatabaseSettings.xml Example

```xml
<?xml version="1.0" encoding="utf-8"?>
<DatabaseSettings xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                  xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <Servers>
    <DatabaseServer Id="sql_main" DisplayName="Main SQL Server"
                    DatabaseType="SQLServer"
                    ConnectionString="Server=sql.example;Database={@DbName};User ID={@UserId};Password={@Password};" />
  </Servers>
  <Items>
    <!-- common category: single physical carrier; Id matches CategoryId -->
    <DatabaseItem Id="common" CategoryId="common" DisplayName="Shared Database"
                  DatabaseType="SQLServer" ServerId="sql_main"
                  DbName="erp_common" UserId="erp_user"
                  Password="enc:base64encodeddata..." />

    <!-- company category: multi-tenant partitioning, one entry per company -->
    <DatabaseItem Id="company001" CategoryId="company" DisplayName="Company 01 Database"
                  DatabaseType="SQLServer" ServerId="sql_main"
                  DbName="company001" UserId="erp_user"
                  Password="enc:base64encodeddata..." />
    <DatabaseItem Id="company002" CategoryId="company" DisplayName="Company 02 Database"
                  DatabaseType="SQLServer" ServerId="sql_main"
                  DbName="company002" UserId="erp_user"
                  Password="enc:base64encodeddata..." />

    <!-- log category: the framework's st_log_* tables are written through Id="log" -->
    <DatabaseItem Id="log" CategoryId="log" DisplayName="Log Database"
                  DatabaseType="SQLServer" ServerId="sql_main"
                  DbName="erp_log" UserId="erp_user"
                  Password="enc:base64encodeddata..." />

    <!-- log category: optional year-based entries for application-owned log tables -->
    <DatabaseItem Id="log2026" CategoryId="log" DisplayName="2026 Application Log Database"
                  DatabaseType="SQLServer" ServerId="sql_main"
                  DbName="log2026" UserId="erp_user"
                  Password="enc:base64encodeddata..." />
  </Items>
</DatabaseSettings>
```

### 6.3 DbCategorySettings.xml

Start from the file the framework ships, [`src/Polhem.Definition/Defaults/DbCategorySettings.xml`](../../src/Polhem.Definition/Defaults/DbCategorySettings.xml), which registers every framework table under its category, and add your own tables to it. The shape, with the framework's entries shortened to `...`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<DbCategorySettings xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Categories>
    <DbCategory Id="common" DisplayName="Common database">
      <Tables>
        <TableItem TableName="st_user" DisplayName="User" />
        ...
      </Tables>
    </DbCategory>
    <DbCategory Id="company" DisplayName="Company database">
      <Tables>
        <TableItem TableName="st_department" DisplayName="Department" />
        ...
        <!-- your business tables -->
        <TableItem TableName="ft_project" DisplayName="Project" />
      </Tables>
    </DbCategory>
    <DbCategory Id="log" DisplayName="Log database">
      <Tables>
        <TableItem TableName="st_log_login" DisplayName="Login log" />
        ...
      </Tables>
    </DbCategory>
  </Categories>
</DbCategorySettings>
```

---

## Related Documents

- [Architecture Overview](architecture-overview.md) — Definition-Driven architecture overview
- [Development Cookbook](development-cookbook.md) — framework initialization and development flow
- [Database Naming Conventions](database-naming-conventions.md) — table / column naming rules
- [ADR-005: FormSchema-Driven Architecture](../adr/adr-005-formschema-driven.md)
- [ADR-010: Logical Database Category](../adr/adr-010-logical-database-category.md) — why DbCategory was introduced
