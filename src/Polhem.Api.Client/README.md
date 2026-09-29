# Polhem.Api.Client

> API client connectors providing one interface for local (in-process) and remote (network) business logic invocation.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: Frontend / Client
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/architecture/dependency-map.md). Not enumerated here — the csproj files are the authority, and a prose copy in every package README drifts with nothing to catch it. These did: `Polhem.Hosting` was missing as a dependent from four of them for months after it was extracted.
- Consumed by application code (Avalonia, Blazor, console and other heads).

## Target Framework

- `net10.0` -- access to modern runtime APIs and performance improvements

## Key Features

### Local / Remote Strategy

- `IJsonRpcProvider` abstracts the transport layer; `LocalApiProvider` invokes business logic in-process via `JsonRpcExecutor`, while `RemoteApiProvider` sends HTTP POST requests to a remote endpoint.
- The strategy is selected by the connector constructor: the constructors that take an `IServiceProvider` (the backend service provider built with `AddPolhemFramework`) run in-process, the ones that take an endpoint URL go over HTTP.

### Connectors

- `SystemApiConnector` -- system operations: login and logout, session creation, company entry, `InitializeAsync`
  (environment bootstrap), definition reads and saves (`GetDefineAsync` / `SaveDefineAsync` and typed variants
  such as `GetFormSchemaAsync`), API key management and `ExecFuncAsync`. The full list is the class itself.
- `FormApiConnector` -- bound to one `ProgId`: `GetListAsync`, `GetDataAsync`, `GetNewDataAsync`, `SaveAsync`,
  `DeleteAsync`, `GetLookupAsync`, `ExecFuncAsync` and `ExecFuncAnonymousAsync`.
- `AuditLogApiConnector` -- reads the audit and anomaly logs.
- Every action method is asynchronous, carries the `Async` suffix and takes a trailing `CancellationToken`.
  All connectors share the payload pipeline (encoding, compression, encryption) of `ApiConnector`.

### Connection Validation

- `ApiConnectValidator.ValidateAsync` determines `ConnectType` (Local or Remote) from the endpoint string, validates the target, and optionally generates missing settings files for local connections.
- Remote validation performs a `Ping` to verify connectivity before returning.

### Cached Definition Access

- `ClientDefineAccess` reads and saves definitions through a `SystemApiConnector` (`GetFormSchemaAsync`,
  `GetFormLayoutAsync`, `GetSystemSettingsAsync`, `SaveFormSchemaAsync`, …) and caches what it has read.
  It is an asynchronous API of its own and does not implement the server-side `IDefineAccess`.
- `FormDefinitionLoader` builds the localized `FormSchema` and runtime `FormLayout` a UI needs from those definitions.

### Client State

- `ApiClientInfo` holds the process-wide settings: `ConnectType`, `Endpoint`, `ApiKey`, `DefaultLanguage` and
  `SupportedConnectTypes`.
- `ApiSessionContext` holds the state of one signed-in user (the transmission key set at login and the user's
  time zone). A single-user head uses the shared `ApiSessionContext.Ambient`; a host serving several users from
  one process (Blazor Server) passes one per session to the connector constructors.

### UI Helpers Shared by Every Head

- `ElementCapabilityResolver` -- resolves whether a field is visible or read-only from the user's permissions.
- `FormDataGuard` / `FormValueBinding` -- CRUD preconditions and value conversion between the UI and a `DataRow`.

## Key Public APIs

| Class / Interface | Purpose |
|-------------------|---------|
| `ApiClientInfo` | Process-wide client settings (connection type, endpoint, API key, default language) |
| `ApiSessionContext` | Per-user client state (transmission key, time zone) |
| `ApiConnector` | Abstract base connector with the payload pipeline |
| `SystemApiConnector` | System-level operations |
| `FormApiConnector` | Form-level business object calls bound to a specific ProgId |
| `AuditLogApiConnector` | Audit and anomaly log queries |
| `IJsonRpcProvider` | Strategy interface for JSON-RPC transport |
| `LocalApiProvider` | In-process provider via `JsonRpcExecutor` |
| `RemoteApiProvider` | HTTP-based provider with API key and Bearer token headers |
| `ClientDefineAccess` | Cached asynchronous definition access over the API |
| `FormDefinitionLoader` | Localized form schema and runtime layout for a UI |
| `ApiConnectValidator` | Validates endpoints and determines connection type |
| `ConnectType` | Enum: `Local`, `Remote` |
| `SupportedConnectTypes` | Flags enum: `Local`, `Remote`, `Both` |

## Design Conventions

- **Strategy Pattern** -- `IJsonRpcProvider` with `LocalApiProvider` and `RemoteApiProvider` implementations; the connector selects the strategy at construction time.
- **Template Method** -- `ApiConnector.ExecuteAsync<T>` has fixed steps (create request, transform payload, invoke provider, restore response); subclasses supply domain-specific methods.
- **Dual constructor pattern** -- each connector offers a local and a remote constructor, mirroring the two provider types: `SystemApiConnector(IServiceProvider services, Guid accessToken)` / `(string endpoint, Guid accessToken)`. `FormApiConnector` takes the bound `progId` as well. Each has an overload that also takes an `ApiSessionContext`.
- **Payload format** -- each action chooses its `PayloadFormat`; an `Encrypted` request is sent `Encoded` when the session has no transmission key yet, and a local provider sends `Plain` unless `SysInfo.IsDebugMode` is on. `ApiConnector.PayloadCodec` selects the body codec of `Encoded` / `Encrypted` requests (MessagePack when blank).

## Directory Structure

- project root -- `ApiClientInfo`, `ApiSessionContext`, `ApiConnectValidator`, `ClientDefineAccess`, `ConnectType`,
  `SupportedConnectTypes`, `FormDataGuard`, `FormValueBinding`
- `Connectors/` -- `ApiConnector`, `SystemApiConnector`, `FormApiConnector`, `AuditLogApiConnector`
- `Providers/` -- `IJsonRpcProvider`, `LocalApiProvider`, `RemoteApiProvider`
- `Definitions/` -- `FormDefinitionLoader`, `LanguageLayers`, `SnapshotLanguageService`
- `Permissions/` -- `IElementCapabilityResolver`, `ElementCapabilityResolver`, `FieldCapability`
