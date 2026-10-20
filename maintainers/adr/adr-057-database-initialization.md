# ADR-057: The framework initializes a new deployment's databases, and the host runs it as `db` commands

## Status

**Accepted (2026-10-20)**

## Context

A new deployment needs three things before its first sign-in: the databases its `DatabaseSettings` names, the tables
its `DbCategorySettings` registers, and a first user with a company to enter. Until now the framework did none of
them. Each application wrote its own seeder (Northwind, the samples, the load tests), each with the same table loop
and its own `INSERT`s into `st_user`, `st_company` and `st_user_company`, and only the load tests and the test fixture
could create a database. A developer starting a new application from a template had no supported way to get from an
empty server to a sign-in.

The loop the seeders share also assumed that a database item's id equals its category id, which only holds for a
single company.

## Decision

### 1. `Polhem.Hosting` provides the initialization; the host runs it as `db` commands

`IDatabaseInitializer` (registered by `AddPolhemFramework`) has three operations: `CreateDatabases`, `UpgradeTables`
and `SeedAdministrator`. `DatabaseCommandLine.TryRun` maps them to `db create`, `db upgrade` and `db seed-admin`, so a
host adds one line after building its service provider and before it starts serving. The commands do not start the
host, so no hosted service runs.

The host is the one place that has the ADO.NET providers: it registers them for its own use. DefineEditor carries no
provider and does not take part; a definition tool should not need a database driver to edit definitions.

### 2. Creating a database lives in `Polhem.Db`, behind a name allowlist

`DatabaseCreator` takes a resolved connection string, reads the database it names, and creates it through the engine's
administrative database when it is missing. A database name is an identifier and cannot be a parameter, so it is
concatenated into the DDL; what makes that safe is `DatabaseCreator.ValidateDatabaseName`, which accepts only ASCII
letters, digits and underscore, up to 63 characters (PostgreSQL's limit, the shortest), and runs on the same value
right before the concatenation. The existence probes compare a value and are parameterised.

It is a static class rather than a new member of `IDialectFactory`, because adding a member to a public interface
breaks every implementation outside the framework.

- **Oracle is never created.** Its "database" is a schema owned by a user, which only a DBA can create; the command
  reports it as having nothing to create.
- **SQLite is a file.** Opening it creates it; a missing directory is created first. An in-memory database has
  nothing to create.

### 3. Tables are built per database item, not per category id

`UpgradeTables` takes each category of `DbCategorySettings`, finds every database item whose `CategoryId` is that
category, and builds every table of the category in each of them. A deployment with several company databases gets
all of them upgraded. An item with no `CategoryId` counts as belonging to the category its id names, so older
settings keep working. A category with tables and no database item is reported as a problem rather than skipped
silently. A failing table does not stop the others; the report lists each table as created, upgraded, unchanged,
planned (`--dry-run`) or failed.

### 4. Seeding the first administrator never changes what exists

`SeedAdministrator` creates a user, a company and the user's access to it, in one transaction, in the common database.
A row that already exists is left as it is. In particular an existing user's password is never replaced, so running the
command again, or against a deployment that is already in use, cannot be used to take over an account.

- The password is hashed with `PasswordHasher` and never written to any output or message. The command does not accept
  it as an argument, where it would stay in the shell history; it reads `POLHEM_ADMIN_PASSWORD` or prompts without
  echoing.
- The company's database must be a database item of the `company` category. Otherwise the user could sign in and then
  fail on the first company-scoped request.
- The user is created as a deployment administrator unless `--no-deployment-admin` is given.
  `SystemBusinessObject.SetDeploymentAdmin` remains the way to appoint one later; a deployment with no user has nobody
  to make that call, which is the gap this fills. Because the seed runs without a session, it writes no audit entry.
- No role is created. A form without a permission model needs none; a form with one needs grants the deployment
  defines.

### 5. Making a repeated upgrade report "unchanged"

The tests run every operation twice, and the second run exposed two existing defects that made some tables upgrade on
every run. Both are fixed here:

- `TableSchemaComparer` now treats a defined default equal to the dialect's built-in default as no default, which is
  how every table schema provider already reads such a column back. `st_audit_rule` and `st_log_change` declare
  `DbType="Boolean" DefaultValue="0"` and were rebuilt on every upgrade.
- `OracleTableSchemaProvider` reads `DATA_DEFAULT_VC` instead of the LONG `DATA_DEFAULT`, which ODP.NET does not fetch by
  default. Every non-built-in default on Oracle read back empty, and each such column was modified on every upgrade.

## Consequences

- A template-generated server can go from an empty database server to a sign-in with three commands.
- The application seeders still build tables with their own loop. They can move to `IDatabaseInitializer` later; the
  load tests already create their databases through `DatabaseCreator`.
- The login in the connection string needs permission to create databases for `db create`. Where it has none, a DBA
  creates the databases and `db upgrade` runs as before.
