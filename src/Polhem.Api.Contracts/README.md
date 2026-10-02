# Polhem.Api.Contracts

> Contract interface library shared by the API layer and the business logic layer, defining the Request/Response interfaces.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: Shared Contracts. Despite the name it is not part of the API layer: `Polhem.Business` and `Polhem.Api.Core` both depend on it, so it sits below them
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/architecture/dependency-map.md). Not enumerated here — the csproj files are the authority, and a prose copy in every package README drifts with nothing to catch it. These did: `Polhem.Hosting` was missing as a dependent from four of them for months after it was extracted.

## Target Framework

- `net10.0` -- access to modern runtime APIs and performance improvements

## Key Features

### Authentication Contracts

- `ILoginRequest` / `ILoginResponse` -- RSA key-exchange login flow (client sends `ClientPublicKey`, server returns `ApiEncryptionKey`)
- `ICreateSessionRequest` / `ICreateSessionResponse` -- session creation for a user id, accepted from in-process (local) calls only
- `ILogoutRequest` / `ILogoutResponse` -- ends the caller's session
- `IEnterCompanyRequest` / `ILeaveCompanyRequest` (and their responses) -- company scope of the session

### Health Check

- `IPingRequest` / `IPingResponse` -- lightweight API health / connectivity check

### Definition CRUD

- `IGetDefineRequest` / `IGetDefineResponse` -- retrieve FormSchema-driven definition data
- `ISaveDefineRequest` / `ISaveDefineResponse` -- persist definition data changes

### Custom Function Execution

- `IExecFuncRequest` / `IExecFuncResponse` -- invoke server-side custom functions (AnyCode pattern)

### Configuration

- `IGetCommonConfigurationRequest` / `IGetCommonConfigurationResponse` -- retrieve shared application configuration

### Definitions and Organization

- `IGetFormSchemaRequest`, `IGetFormLayoutRequest`, `IGetLanguageRequest` (and their responses) -- typed definition reads
- `IGetCustomizePluginSettingsRequest` / `ISaveCustomizePluginSettingsRequest` (and their responses) -- a tenant's plugin settings
- `IGetDepartmentTreeRequest` / `IGetDepartmentTreeResponse` -- the company's department tree

### API Keys and Deployment Administration

- `ICreateApiKeyRequest`, `IListApiKeysRequest`, `ISetApiKeyEnabledRequest`, `ISetApiKeyExpiryRequest` (and their responses) -- API key management
- `ISetDeploymentAdminRequest` / `ISetDeploymentAdminResponse` -- grants or revokes the deployment administrator flag

### Forms and Audit Logs

- `Form/` -- `IGetListRequest`, `IGetDataRequest`, `IGetNewDataRequest`, `ISaveRequest`, `IDeleteRequest`, `IGetLookupRequest` and their responses
- `AuditLog/` -- the change, access, login and anomaly log queries, with `IAuditLogListResponse`, `IAuditLogAggregateResponse` and `RecordFieldChange`

## Key Public APIs

| Interface / Class | Purpose |
|-------------------|---------|
| `ILoginRequest` / `ILoginResponse` | RSA key-exchange login contract |
| `ICreateSessionRequest` / `ICreateSessionResponse` | Session creation contract |
| `IPingRequest` / `IPingResponse` | Health check contract |
| `IGetDefineRequest` / `IGetDefineResponse` | Definition retrieval contract |
| `ISaveDefineRequest` / `ISaveDefineResponse` | Definition persistence contract |
| `IExecFuncRequest` / `IExecFuncResponse` | Custom function execution contract |
| `IGetCommonConfigurationRequest` / `IGetCommonConfigurationResponse` | Configuration retrieval contract |

## Design Conventions

- **Axis-based namespaces** -- interfaces are grouped into `System` / `Form` / `AuditLog` sub-namespaces that mirror the `Polhem.Business.*` and `Polhem.Api.Core.Messages.*` layers, so a contract, its message implementation, and its business object share the same axis. The generic cross-BO `IExecFunc*` dispatch contract stays at the root `Polhem.Api.Contracts` (mirroring the root-level `ExecFunc*` implementation in `Polhem.Api.Core.Messages`).
- **Pure interface definitions** -- each API operation is defined as an `IXxxRequest` / `IXxxResponse` pair; no implementation logic in this project.
- **No serialization attributes** -- data classes such as `RecordFieldChange` are plain types with public read/write properties. Their binding to the wire lives in `Polhem.Api.Core` as hand-written formatters, so this package takes no dependency on a transport format ([ADR-036](../../maintainers/adr/adr-036-wire-serialization-externalized.md)).
- **RSA-based security** -- the login contract includes `ClientPublicKey` (client-generated) and `ApiEncryptionKey` (server-generated) for secure key exchange.
- **Nullable reference types** enabled (`<Nullable>enable</Nullable>`).

## Why These Interfaces Exist

They are not decorative markers. Two mechanisms depend on them at runtime and at compile time.

**1. They make a silent reflective copy total.** Every API call converts in both directions through
the internal `ApiInputConverter.Convert`, which copies public properties **by matching name** — inbound from the
wire message to the BO argument (called by the server's parameter binder), outbound from the BO result to the
wire response (called by `ApiOutputConverter`). A name that does not match is skipped silently: no
exception, no warning, and the call appears to succeed with that field left empty. Because both
`LoginRequest` and `LoginArgs` implement `ILoginRequest`, the compiler forces the two sides to carry
the same members, so the copy cannot be partial.

**2. They are the discriminator for wire invariants.** `DateTimeWireGuard` pattern-matches on the
response contracts (`IGetListResponse`, `ISaveResponse`, `IAuditLogListResponse`, and others) to find the
payloads that carry a `DataSet` or a loose `DateTime`, and enforces the ADR-032 wire invariants on
them.

Both sides of the pairing are gated by tests rather than by review: `ApiContractPairingTests`
(in `Polhem.Api.Core.UnitTests`) asserts every `ApiRequest` / `ApiResponse` subtype implements its
matching contract, and `BusinessContractPairingTests` (in `Polhem.Business.UnitTests`) asserts the same
for every `BusinessArgs` / `BusinessResult`. Each gate exists because that side drifted once and
nobody noticed.

> There is no runtime registry mapping contracts to implementations. One existed
> (`ApiContractRegistry`) for a "BO returns a plain POCO" scenario that never materialised; it was
> removed in favour of the conversion `ApiOutputConverter` already performs.

## Directory Structure

Interfaces are organized into axis sub-folders (folder = sub-namespace); the cross-BO
`IExecFunc*` pair stays at the root.

```
Polhem.Api.Contracts/
  IExecFuncRequest.cs / IExecFuncResponse.cs          # root — cross-BO generic dispatch
  System/                                             # namespace Polhem.Api.Contracts.System
  Form/                                               # namespace Polhem.Api.Contracts.Form
  AuditLog/                                           # namespace Polhem.Api.Contracts.AuditLog
```
