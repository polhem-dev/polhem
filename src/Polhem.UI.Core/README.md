# Polhem.UI.Core

> Shared client-side foundation for the `Polhem.UI.*` native front-end family (Avalonia today): connection state, connector creation, endpoint and API key persistence.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: UI Layer (shared client foundation)
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/dependency-map.md). Not enumerated here — the csproj files are the authority, and a prose copy in every package README drifts with nothing to catch it. These did: `Polhem.Hosting` was missing as a dependent from four of them for months after it was extracted.
- The Blazor family does **not** consume `Polhem.UI.Core` — see [ADR-013](../../maintainers/adr/adr-013-frontend-api-connection-strategy.md).

## Target Framework

- `net10.0` -- access to modern runtime APIs and performance improvements

## Overview

`Polhem.UI.Core` is the framework-agnostic base every `Polhem.UI.*` front end builds on. It holds the
per-process client connection state (`ClientInfo`) and abstracts where the service endpoint and API key are
persisted (`IEndpointStorage`, `IApiKeyStorage`). It carries no UI-framework types.

`Polhem.Web.Blazor.Server` serves several users from one process, so it keeps its connection state per circuit
instead of in `ClientInfo`. What the two families do share lives one layer down in `Polhem.Api.Client`: the
connectors, `ClientDefineAccess`, `FormDefinitionLoader` and the permission capability resolver.

## Key Types

### Connection State

- `ClientInfo` -- static client-side connection state. Owns the `AccessToken` (per-process token
  model: resetting the token clears the cached `SystemApiConnector`, `DefineAccess` and
  capability snapshot), lazily creates the `SystemApiConnector` and `DefineAccess` (`ClientDefineAccess`),
  produces connectors via `CreateFormApiConnector(progId)` and `CreateAuditLogApiConnector()`, resolves the
  endpoint (local vs. remote) through `InitializeAsync` / `SetEndpointAsync`, and applies login / EnterCompany
  results (`ApplyLoginResult`, `ApplyEnterCompanyResult`, `ClearCompanyContext`). `ResetDefineCache` discards
  the cached definition data after a tenant switch.
- `ClientInfo.LocalServiceProvider` -- the backend service provider of a head that runs the backend in-process
  (built with `AddPolhemFramework`); the local connectors `ClientInfo` creates use it.
- `ClientInfo.UseDefinitionLoader` / `DefinitionLoader` -- localized form definitions through `FormDefinitionLoader`.

### Endpoint and API Key Persistence

- `IEndpointStorage` -- persistence contract for the configured service endpoint
  (`LoadEndpoint` / `SetEndpoint` / `SaveEndpoint`).
- `IApiKeyStorage` -- the same for the `X-Api-Key` value (`LoadApiKey` / `SetApiKey` / `SaveApiKey`).
- `FileEndpointStorage` -- the default for both `ClientInfo.EndpointStorage` and `ClientInfo.ApiKeyStorage`: one
  single-line text file per value in a per-application folder under the per-user local application data
  directory. Its XML documentation lists where that folder is on each platform. Browser WASM has no persistent
  file system, so a browser host assigns both properties an implementation backed by browser storage.
- `ClientInfo.ApplyApiKey(defaultApiKey)` -- applies the stored key, seeding empty storage with the
  value the application ships. That makes the shipped constant a first-run default instead of a
  hard-coded key: from then on the stored value wins and can be changed without recompiling.
  `ClientInfo.SetApiKey` persists a new key and applies it to subsequent calls.

> An API key held by a client is not a secret in the cryptographic sense -- it can be recovered from
> the shipped application. It identifies *which application* is calling; authenticating *the user*
> remains the access token's job.

### Host Services

- `IUIViewService` -- view services supplied by the host UI framework (e.g. `ShowApiConnectAsync`
  to prompt for connection setup when the endpoint is missing or unreachable).

### Permission Capabilities

- `ClientInfo.Capabilities` -- the per-model permission snapshot received at company entry. The resolver that
  turns it into element-level decisions, `ElementCapabilityResolver`, is in `Polhem.Api.Client` so both UI
  families use it.

> Client-side capability resolution is **UX degradation only**. The backend remains the
> authoritative security boundary.

## Design Conventions

- **Per-process token model** -- `ClientInfo` is static and holds one access token for the
  process; changing it invalidates the cached connectors, define accessor, and capability snapshot.
- **Framework-agnostic** -- no UI-framework types leak in, so the same connection logic serves every
  `Polhem.UI.*` front end.
- **Pluggable endpoint / API key storage** -- hosts replace `ClientInfo.EndpointStorage` and
  `ClientInfo.ApiKeyStorage` with platform-appropriate implementations.
- **Async-friendly initialization** -- `InitializeAsync` / `SetEndpointAsync` validate the endpoint
  and initialize the connector without blocking, so they are safe on single-threaded runtimes
  (browser WASM).
- **Nullable reference types** enabled (`<Nullable>enable</Nullable>`).

## Directory Structure

- `ClientInfo.cs` -- client-side connection state and connector creation
- `IEndpointStorage.cs` / `IApiKeyStorage.cs` -- persistence contracts
- `FileEndpointStorage.cs` -- the default file-backed implementation of both
- `IUIViewService.cs` -- host-supplied view services
