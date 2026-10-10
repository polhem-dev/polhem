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

- The connectors send through [`Polhem.JsonRpc.Client`](https://github.com/polhem-dev/polhem-jsonrpc), sealing each call with `PayloadConnector` from `Polhem.JsonRpc.Payload.Client`, over one of two transports that implement its `IJsonRpcTransport`: an in-process one that hands the call to the backend's dispatcher, or an HTTP one that sends POST requests to a remote endpoint.
- The strategy is chosen once, when the `PolhemApiClient` is created: `PolhemApiClient.CreateLocal` takes an `IServiceProvider` (the backend service provider built with `AddPolhemFramework`) and runs in-process, `PolhemApiClient.CreateRemote` takes an endpoint URL and the API key and goes over HTTP. Every connector the client hands out follows that choice.

```csharp
var client = PolhemApiClient.CreateRemote("https://host/api", apiKey);
await client.System.LoginAsync(userId, password);
await client.System.EnterCompanyAsync(companyId);
var data = await client.Form("Employee").GetDataAsync(rowId);
```

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

- `ApiConnectValidator.ValidateAsync` determines `ConnectType` (Local or Remote) from the endpoint string, checks it against the `SupportedConnectTypes` the caller passes, validates the target, and optionally generates missing settings files for local connections.
- Remote validation performs a `Ping` to verify connectivity before returning.

### Cached Definition Access

- `ClientDefineAccess` reads and saves definitions through a `SystemApiConnector` (`GetFormSchemaAsync`,
  `GetFormLayoutAsync`, `GetSystemSettingsAsync`, `SaveFormSchemaAsync`, …) and caches what it has read.
  It is an asynchronous API of its own and does not implement the server-side `IDefineAccess`.
- `FormDefinitionLoader` builds the localized `FormSchema` and runtime `FormLayout` a UI needs from those definitions.

### Client State

- `PolhemApiClient` is the composition root: one connection to a backend (`IsLocal`, `Endpoint`, `ApiKey`,
  `PayloadOptions`, `DefaultLanguage`), the identity signed in over it (`Session`), and the connectors that call it
  (`System`, `AuditLog`, `Form(progId)`). The members are the class itself.
- `ApiSessionContext` (`PolhemApiClient.Session`) holds the signed-in state as one immutable
  `ApiSessionCredentials` (access token, transmission key, user's time zone). `SystemApiConnector.LoginAsync`
  replaces it as a unit, `LogoutAsync` and `PolhemApiClient.SignOut` clear it.
- One client holds one identity. A host serving several users from one process (Blazor Server) creates a client
  per user.

### UI Helpers Shared by Every Head

- `ElementCapabilityResolver` -- resolves whether a field is visible or read-only from the user's permissions.
- `FormDataGuard` / `FormValueBinding` -- CRUD preconditions and value conversion between the UI and a `DataRow`.

## Key Public APIs

| Class / Interface | Purpose |
|-------------------|---------|
| `PolhemApiClient` | Entry point: one connection, its signed-in identity and its connectors |
| `ApiSessionContext` / `ApiSessionCredentials` | The client's signed-in state (access token, transmission key, time zone) |
| `ApiConnector` | Abstract base connector with the payload pipeline |
| `SystemApiConnector` | System-level operations |
| `FormApiConnector` | Form-level business object calls bound to a specific ProgId |
| `AuditLogApiConnector` | Audit and anomaly log queries |
| `ClientDefineAccess` | Cached asynchronous definition access over the API |
| `FormDefinitionLoader` | Localized form schema and runtime layout for a UI |
| `ApiConnectValidator` | Validates endpoints and determines connection type |
| `ConnectType` | Enum: `Local`, `Remote` |
| `SupportedConnectTypes` | Flags enum: `Local`, `Remote`, `Both` |

## Design Conventions

- **Strategy Pattern** -- an in-process and an HTTP transport implement `IJsonRpcTransport`; the client's factory method selects one, and each call gets a transport of that kind.
- **Template Method** -- `ApiConnector.ExecuteAsync<T>` has fixed steps (transform payload, send through the transport, restore response); subclasses supply domain-specific methods.
- **Connectors belong to a client** -- each connector has one constructor taking the `PolhemApiClient` (`FormApiConnector` takes the bound `progId` as well), and reads its connection and credentials from `ApiConnector.Client`. `PolhemApiClient.System` and `AuditLog` are single instances; `Form(progId)` creates a new connector each call.
- **Payload format** -- each action chooses its `PayloadFormat`; an `Encrypted` request is sent `Encoded` when the session has no transmission key yet, and a local client sends `Plain` unless `SysInfo.IsDebugMode` is on. `ApiConnector.PayloadCodec` selects the body codec of `Encoded` / `Encrypted` requests (MessagePack when blank).

## Directory Structure

- project root -- `PolhemApiClient`, `ApiSessionContext`, `ApiSessionCredentials`, `ApiConnectValidator`, `ClientDefineAccess`, `ConnectType`,
  `SupportedConnectTypes`, `FormDataGuard`, `FormValueBinding`
- `Connectors/` -- `ApiConnector`, `SystemApiConnector`, `FormApiConnector`, `AuditLogApiConnector`
- `Providers/` -- the internal in-process and HTTP transports
- `Definitions/` -- `FormDefinitionLoader`, `LanguageLayers`, `SnapshotLanguageService`
- `Permissions/` -- `IElementCapabilityResolver`, `ElementCapabilityResolver`, `FieldCapability`
