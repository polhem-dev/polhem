# Polhem.Business

> Business logic layer: the system, form and audit-log business objects, authentication and session handling, and the custom function execution framework.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: Business Logic Layer
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/dependency-map.md). Not enumerated here — the csproj files are the authority, and a prose copy in every package README drifts with nothing to catch it. These did: `Polhem.Hosting` was missing as a dependent from four of them for months after it was extracted.

## Target Framework

- `net10.0` -- access to modern runtime APIs and performance improvements

## Key Features

### Custom Function Execution

- `IBusinessObject` -- base interface exposing `ExecFunc` (authenticated) and `ExecFuncAnonymous` (anonymous) entry points
- `ExecFuncArgs` / `ExecFuncResult` -- input/output contracts for custom function dispatch
- `IExecFuncHandler` -- a class whose public methods are the functions; `ExecFuncHandlerExtensions` invokes the
  method named by the function id through reflection
- `ExecFuncAccessControlAttribute` -- method-level attribute declaring the access requirement of each function

### System Operations

- `ISystemBusinessObject` -- the cross-BO contract: `Login`, `CreateSession`, `EnterCompany`, `LeaveCompany`, `Logout`. API-only methods (`Ping`, `GetFormSchema`, `GetFormLayout`, `GetLanguage`, …) are public on the concrete `SystemBusinessObject` with `[ApiAccessControl]` and deliberately stay off this interface
- Each operation has an argument/result pair in `System/` (`LoginArgs` / `LoginResult`, `GetDefineArgs` / `GetDefineResult`, …)

### Form Operations

- `IFormBusinessObject` / `FormBusinessObject` -- FormSchema-driven CRUD (`GetList`, `GetData`, `GetNewData`, `Save`, `Delete`, `GetLookup`) with record scope, permissions, audit and plugins
- `FormBusinessPlugin` -- base class of a plugin that runs at fixed points of a program's save and delete pipelines, bound through `PluginSettings`

### Audit Logs

- `AuditLogBusinessObject` -- queries of the change, access, login and anomaly logs (reserved progId `AuditLog`)
- `AuditRuleBusinessObject` -- maintenance of the audit rules (reserved progId `AuditRule`)

### Authentication & Security

- `LoginAttemptTracker` -- in-memory account lockout; the defaults are the constants
  `DefaultMaxFailedAttempts` and `DefaultLockoutMinutes`
- `AccessTokenValidator` -- validates access tokens for authenticated API calls
- `DerivedApiEncryptionKeyProvider` (the default), `DynamicApiEncryptionKeyProvider`, `StaticApiEncryptionKeyProvider` -- encryption key strategies for API payload protection
- `DeploymentAuthorizationService` -- answers whether the session's user is a deployment administrator

### Data, Sessions & Caching

- `CacheDataSourceProvider` -- loads the database-dependent cache objects
- `SessionCompanyBinder` / `EmployeeContextResolver` -- company entry and the employee context of a session
- `BusinessArgs` / `BusinessResult` -- base input/output types shared across business operations

## Key Public APIs

| Class / Interface | Purpose |
|-------------------|---------|
| `IBusinessObject` | Base BO interface (`ExecFunc`, `ExecFuncAnonymous`) |
| `ISystemBusinessObject` | Cross-BO system operations (API-only methods stay on the concrete class) |
| `IFormBusinessObject` | Form-level business logic interface |
| `BusinessObjectFactory` | Creates BO instances for a progId |
| `IBoTypeResolver` / `ProgramSettingsBoTypeResolver` | Resolves a progId to its BO type through `ProgramSettings` |
| `LoginAttemptTracker` | Account lockout after consecutive failures |
| `AccessTokenValidator` | Access token validation |
| `DerivedApiEncryptionKeyProvider` | Default per-session key, derived from a root key and the access token |
| `ExecFuncArgs` / `ExecFuncResult` | Custom function dispatch contracts |
| `ExecFuncAccessControlAttribute` | Method-level access requirement declaration |
| `BusinessArgs` / `BusinessResult` | Base input/output types for operations |

## Design Conventions

- **Command Pattern** -- `ExecFunc` invokes handler methods by name via reflection, dispatching custom business logic dynamically.
- **Factory Pattern** -- `BusinessObjectFactory` creates the business object bound to a progId, with the access token and context.
- **Template Method** -- `BusinessObject` defines the execution skeleton; subclasses override `DoExecFunc(ExecFuncArgs, ExecFuncResult)` and `DoExecFuncAnonymous(ExecFuncArgs, ExecFuncResult)` for specific logic.
- **Strategy Pattern** -- the encryption key providers are interchangeable implementations of `IApiEncryptionKeyProvider`, selected through `BackendComponents.ApiEncryptionKeyProvider`.
- **Attribute-driven access control** -- `ExecFuncAccessControlAttribute` declares per-method access requirements, checked at dispatch time.
- **Nullable reference types** enabled (`<Nullable>enable</Nullable>`).

## Directory Structure

- project root -- `BusinessObject`, `BusinessObjectFactory`, `IBusinessObject`, `IExecFuncHandler`, `ExecFuncArgs`, `ExecFuncResult`, `BusinessArgs`, `BusinessResult`, the progId resolution types
- `Attributes/` -- `ExecFuncAccessControlAttribute`
- `AuditLog/` -- `AuditLogBusinessObject`, `AuditRuleBusinessObject` and their arguments and results
- `Form/` -- `IFormBusinessObject`, `FormBusinessObject` (split by concern), the form plugins, and the form arguments and results
- `Permission/` -- `ScopeResolver` (record scope)
- `Providers/` -- the encryption key providers, `CacheDataSourceProvider`
- `Security/` -- `LoginAttemptTracker`, `DeploymentAuthorizationService`
- `Session/` -- `SessionCompanyBinder`, `EmployeeContextResolver`
- `System/` -- `ISystemBusinessObject`, `SystemBusinessObject` (split by concern) and the system arguments and results
- `Validator/` -- `AccessTokenValidator`
