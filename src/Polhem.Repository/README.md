# Polhem.Repository

> Default implementation of the repository abstractions: form data access, the framework system tables, and the repository factory.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: Data Access Layer (implementation)
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/architecture/dependency-map.md). Not enumerated here — the csproj files are the authority, and a prose copy in every package README drifts with nothing to catch it. These did: `Polhem.Hosting` was missing as a dependent from four of them for months after it was extracted.
- Consumed by application code through the `I*Repository` interfaces of `Polhem.Repository.Abstractions`; the
  repositories are built by `RepositoryFactory`.

## Target Framework

- `net10.0` -- access to modern runtime APIs and performance improvements

## Key Features

### Form Data Access

- `DataFormRepository` -- default `IDataFormRepository` for FormSchema-driven CRUD, resolved by ProgId. A
  program can bind its own subclass through `ProgramItem.Repository` in `ProgramSettings`.

### Framework Repositories (internal)

The repositories of the framework tables (sessions, users, companies, departments, employees, roles, API keys,
audit logs and audit rules, and database administration) are `internal`. A host reaches them through their
interfaces in `Polhem.Repository.Abstractions` via `IRepositoryFactory.Create<T>(accessToken)`.

- Sessions -- the `st_session` row holds the session seed: `SessionUser` serialized to XML, keyed by a hash of
  the access token (`AccessTokenHasher`), so the token itself is not stored. The access token is issued by the
  business layer at sign-in, not by the repository. Reads filter out expired rows without deleting them; the
  expired rows are removed by the host's expired session cleanup service (`Polhem.Hosting`).
- Database administration -- connection testing resolves the `{@DbName}`, `{@UserId}` and `{@Password}`
  placeholders through `ConnectionStringTemplate`; schema upgrades go through `TableSchemaBuilder`.

### Factory Implementation

- `RepositoryFactory` -- default `IRepositoryFactory`: builds every repository, on both axes, from one
  shared `IRepositoryContext`. The framework axis is a type table rather than a method apiece, so it
  does not grow a member per system table.
- `IRepositoryTypeResolver` / `ProgramSettingsRepositoryTypeResolver` -- resolves the repository type bound to a progId.

## Key Public APIs

| Class | Purpose |
|-------|---------|
| `DataFormRepository` | Data form data access implementation |
| `RepositoryFactory` | Default `IRepositoryFactory` implementation |
| `RepositoryBase` | Base class of the repositories (context, access token, progId) |
| `IRepositoryContext` / `RepositoryContext` | The shared application-lifetime services handed to every repository |
| `RepositoryDatabaseRouter` | Default `IRepositoryDatabaseRouter` |

## Design Conventions

- **Parameterized queries** -- the repositories pass values through `DbCommandSpec` placeholders, which the framework turns into command parameters.
- **One factory, two axes** -- progId-bound repositories and framework repositories are both built by `RepositoryFactory`.
- **Nullable reference types** enabled (`<Nullable>enable</Nullable>`).

## Directory Structure

- project root -- `RepositoryBase`, `IRepositoryContext`, `RepositoryContext`, `RepositoryDatabaseRouter`
- `AuditLog/` -- the audit log and audit rule repositories (internal)
- `Factories/` -- `RepositoryFactory`, `IRepositoryTypeResolver`, `ProgramSettingsRepositoryTypeResolver`
- `Form/` -- `DataFormRepository`
- `System/` -- the framework system-table repositories (internal)
