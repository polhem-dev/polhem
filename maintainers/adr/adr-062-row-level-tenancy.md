# ADR-062: Row-level tenancy: companies sharing a company database

## Status

**Accepted (2026-11-10)**

## Context

Tenancy was per database. `FormSchema.CategoryId` picks the scope ([ADR-010](adr-010-logical-database-category.md)), and
`RepositoryDatabaseRouter` resolves the session's company to `CompanyInfo.CompanyDatabaseId`
([ADR-012](adr-012-session-company-context.md)). Companies were kept apart only because each had a database of its own.

The router already allowed several companies to name the same database, but nothing then separated their rows: every
company saw, changed and deleted every other company's data. A hosted deployment that gives trial companies one shared
database, rather than a database each, had no way to keep them apart.

## Decision

### 1. Both models side by side, with one table shape

`CategoryId` still chooses the database. A column `sys_company_id` on every company table says which company a row
belongs to. Paying customers keep a database each, trial companies share one, and the difference is only whether their
`CompanyDatabaseId` values are equal.

The column exists in single-company databases too, where it holds one value. A table schema is then the same in both
models, and moving a company to a database of its own is a data copy, not a schema change. Adding the column only to
shared databases would have meant two table shapes kept in step forever.

`TableSchemaGenerator` adds the column to every table of a `company` form schema and leads every unique index other than
the row identity ones with it (`TableSchema.ApplyCompanyScope`), so a document number is unique within one company.
`common` and `log` tables do not get it by default: `st_user` and `st_company` are shared on purpose, and the audit log
already carries a `company_id` of its own that records where an event happened rather than who owns a row.

### 2. The company id is the business id, stored lowercase, up to 10 characters

The column holds the company's business id (`st_company.sys_id`), the key the session, `ICompanyInfoService` and the
permission snapshot already use, rather than its row id. A shared database stays readable, and filtering needs no lookup.

The framework compares company ids ignoring case, while a database compares them by its collation: case-sensitive on
PostgreSQL and Oracle, case-insensitive on SQL Server and MySQL by default, and on SQLite through the `COLLATE NOCASE`
the framework gives its text columns. Ids are therefore stored in one form, lowercase (`CompanyInfo.NormalizeId`), at
every point an id enters the framework. They are up to 10 ASCII letters and digits (`CompanyInfo.IsValidId`): the column
is copied into every company row and leads every unique index, so its width is kept small and fixed, and
`st_company.sys_id` narrowed from 20 to the same width so the source cannot hold an id its copies cannot.

### 3. Confinement by construction, not by callers remembering

A query that misses the company condition leaks across companies, so the condition is never a parameter a caller can
omit. The record-scope filter, by contrast, is an optional parameter, which is acceptable for narrowing permissions but
not for this.

- **The form repository.** A repository for a `company` form is bound to the session's company when it is built and
  refuses to be built without one. Every SELECT, COUNT and DELETE goes through a command builder that AND-s the company
  condition outside the caller's filter, so no filter widens it. Saving pins the column: an INSERT is stamped with the
  repository's company whatever the data set holds, an UPDATE never changes it, and an UPDATE or DELETE of a row id that
  belongs to another company touches nothing. A relation join between two company forms also matches the company, so a
  foreign key set to another company's row id reads nothing through it.
- **Hand-written SQL.** `DbAccess.BindCompany` binds a database access to one company. Every command it runs must contain
  `{@CompanyId}`, which becomes a parameter carrying the company; a bound command without it, or an unbound one with it,
  is refused when the command is built. `RepositoryBase.CreateDbAccess()` hands out a bound access in a company-bound
  repository, so a statement that forgot the company fails the first time it runs instead of reading every company's
  rows. The POLHEM3004 analyzer reports a repository that creates a database access any other way.
- **The company comes from the session.** The router and the repository read it from the same session, and a change
  between the two reads refuses the request rather than pairing one company's database with another company's id.

The check on hand-written SQL proves that a statement names the company, not that every company table in it is compared
with it. A join of two company tables filtered on one is still possible and is left to review.

### 4. Hand-written APIs that read a company database take the company

`IDepartmentRepository`, `IEmployeeRepository`, `IRolePermissionRepository`, `IAuditRuleRepository` and
`IEmployeeContextResolver.Resolve` gained a `companyId` parameter in a 1.x minor version ([ADR-052](adr-052-breaking-changes-in-1-x-minors.md)).
The old signatures were removed rather than kept as obsolete, because on a shared database they read every company's rows.

### 5. Existing deployments are upgraded by `db upgrade`, which stops rather than guesses

Before it changes anything, `db upgrade` refuses to run while a company table schema lacks the column (written by an
earlier version; `db add-company-column` adds it) or a company id is not in its stored form. Renaming a company touches
every place its id is stored, which is the administrator's decision, so it is not rewritten automatically.

After the tables are upgraded, each row with no company is given the company whose `company_database_id` names its
database. Before this decision every company had a database of its own, so that answer is unambiguous. A database that
several companies name is reported and its rows are left for the administrator; one that no company names is skipped,
since no company could read its rows before either. Once the check has confirmed
that every company id fits, `st_company` may narrow without `--allow-narrowing`.

## Consequences

- A row whose `sys_company_id` is empty belongs to no company and is invisible. On Oracle an empty string is stored as
  `NULL`, which no comparison matches, so the upgrade's backfill looks for both.
- Hand-written SQL in a company form's repository has to name the company. Code that ran unbound statements against a
  company database fails at the first run after the upgrade, which is the intended signal.
- A host that replaces `IRepositoryDatabaseRouter` still needs an `ISessionInfoService` whose session carries the
  company, because a company-bound repository reads it from there.

## Alternatives considered

### Database row-level security

PostgreSQL and SQL Server could enforce the condition in the database. MySQL and Oracle differ, and SQLite has none, so
the framework would have needed this design anyway for the databases that lack it.

### Inject the condition into every `DbCommandSpec`

Rewriting SQL text to add the condition needs to know which tables a statement reads, which is fragile to infer from
text. The form path knows its tables from the schema, and hand-written SQL names the company itself.

### A filter the business object passes

It would be a parameter a caller can leave out, with no sign that it was.
