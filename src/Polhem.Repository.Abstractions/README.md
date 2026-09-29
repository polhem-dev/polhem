# Polhem.Repository.Abstractions

> Abstract interface library for the data access layer, defining the repository contracts and the repository factory.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: Data Access Layer (contracts)
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/architecture/dependency-map.md). Not enumerated here — the csproj files are the authority, and a prose copy in every package README drifts with nothing to catch it. These did: `Polhem.Hosting` was missing as a dependent from four of them for months after it was extracted.

## Target Framework

- `net10.0` -- access to modern runtime APIs and performance improvements

## Key Features

### Factory Contract

- `IRepositoryFactory` -- the single entry point for obtaining a repository, on either axis:
  `CreateFormRepository<T>(accessToken, progId)` for the progId axis (the type varies per progId)
  and `Create<T>(accessToken)` for the framework axis (fixed types named by their interface).
  Both are generic, so adding a repository never widens the interface.

### Form Repository Contracts (`Form/`)

- `IDataFormRepository` -- repository interface for data form CRUD operations; `DataFormListResult` carries a list page

### System Repository Contracts (`System/`)

- `ISessionRepository` -- persistence of the session seed: `GetSession`, `InsertSession`, `UpdateSession`,
  `DeleteSession`, `DeleteExpiredSessions`
- `IDatabaseRepository` -- connection testing (`TestConnection`) and schema migration (`UpgradeTableSchema`)
- `IUserRepository`, `IUserCompanyRepository`, `ICompanyRepository`, `IDepartmentRepository`,
  `IEmployeeRepository`, `IRolePermissionRepository`, `IApiKeyRepository` -- the framework's user, company,
  organization, permission and API key tables

### Audit Log Contracts (`AuditLog/`)

- `IAuditLogRepository` -- queries of the audit and anomaly logs, with the `*LogQuery` types and `AuditLogPage`
- `IAuditLogWriteRepository` -- writes audit and anomaly entries
- `IAuditRuleRepository` -- reads the audit rules and signals that they changed

### Database Routing Contract

- `IRepositoryDatabaseRouter` -- resolves the physical databaseId a repository should use for a logical `DbScope` (`Common` / `Log` / `Company`) and the current session's access token

## Key Public APIs

| Interface / Class | Purpose |
|-------------------|---------|
| `IRepositoryFactory` | The single entry point for every repository, on both axes |
| `IDataFormRepository` | Contract for data form data access |
| `ISessionRepository` | Session seed persistence |
| `IDatabaseRepository` | Connection testing and schema migration |
| `IAuditLogRepository` / `IAuditLogWriteRepository` | Audit log queries and writes |
| `IRepositoryDatabaseRouter` | Resolves the physical databaseId for a logical `DbScope` and access token |

## Design Conventions

- **Repository Pattern** -- each domain concern has a dedicated repository interface.
- **One factory, two axes** -- `IRepositoryFactory` resolves progId-bound repositories through the registry and framework repositories by their interface. It replaced three factories, one of which grew a method per system table.
- **Passive contracts, injected via DI** -- this project defines contracts only; there is no static holder or service locator. Concrete implementations are registered in the DI container and injected where needed, rather than resolved from a static entry point or read from a static `BackendConfiguration`.
- **Nullable reference types** enabled (`<Nullable>enable</Nullable>`).

## Directory Structure

- `AuditLog/` -- `IAuditLogRepository`, `IAuditLogWriteRepository`, `IAuditRuleRepository` and the query types
- `Factories/` -- `IRepositoryFactory`
- `Form/` -- `IDataFormRepository`, `DataFormListResult`
- `System/` -- the system repository contracts
- project root -- `IRepositoryDatabaseRouter` (`DbScope` -> databaseId)
