# Polhem.UI.ApiBridge

> Connects a single-user Polhem front end to a Polhem backend: the client connection state, endpoint and API key persistence, and the API-backed implementations of the `Polhem.UI.Core` services.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: UI Layer (adapter between the shared front-end layer and the API client)
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/architecture/dependency-map.md). Not enumerated here — the csproj files are the authority, and a prose copy in every package README drifts with nothing to catch it.
- It references both [`Polhem.UI.Core`](../Polhem.UI.Core/README.md) and [`Polhem.Api.Client`](../Polhem.Api.Client/README.md), so the dependency points from the bridge to both sides: the API client does not reference the UI, and the UI does not reference the API client. The reasons are in [ADR-054](../../maintainers/adr/adr-054-front-end-decoupled-from-api-client.md).
- **Single-user heads only** (desktop, mobile, browser WebAssembly). A host that serves several users from one process, such as Blazor Server, must not use it: `ClientInfo` holds one signed-in user per process.

## Target Framework

- `net10.0` -- access to modern runtime APIs and performance improvements

## Overview

A front end built on `Polhem.UI.Core` (for example the `Polhem.UI.Avalonia` controls) resolves its connector, its
definitions and its user context through contracts. This package supplies the implementations that read them from a
Polhem backend, and holds the per-process connection state those implementations read from (`ClientInfo`). It
carries no UI-framework types, so any single-user front end reuses it unchanged.

## Registration

A head registers the bridge once at startup and hands the provider to the controls:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Polhem.UI.ApiBridge.DependencyInjection;
using Polhem.UI.Core;

UIServices.Provider = new ServiceCollection().AddPolhemUIApiBridge().BuildServiceProvider();
```

`AddPolhemUIApiBridge()` registers:

| Service (contract) | Implementation | Reads from |
|---|---|---|
| `IFormApiConnectorFactory` (`Polhem.Api.Contracts.Form`) | `Connectors/FormApiConnectorFactory` | `ClientInfo.ApiClient` |
| `IFormDefinitionSource` (`Polhem.UI.Core.Definitions`) | `Definitions/ApiFormDefinitionSource` | `ClientInfo.DefineAccess`, `ClientInfo.DefinitionLoader` |
| `IUserContext` (`Polhem.UI.Core`) | `ClientUserContext` | `ClientInfo.UserInfo`, `Capabilities`, `Company` |

Each implementation reads `ClientInfo` on every call, so the registration holds across the connection, sign-in and
company steps; nothing has to be re-registered when the endpoint or the user changes.
[`Polhem.UI.Avalonia.Shell`](../Polhem.UI.Avalonia.Shell/README.md) wires it this way in `ShellApplication`.

## Key Types

### Connection State

- `ClientInfo` -- static client-side connection state. Owns the head's one `ApiClient` (`PolhemApiClient`;
  per-process identity model: a new sign-in clears the cached `DefineAccess` and capability snapshot, and a new
  endpoint replaces the client and signs out), exposes its `AccessToken`, `ConnectType` and `SystemApiConnector`,
  lazily creates `DefineAccess` (`ClientDefineAccess`), produces connectors via `CreateFormApiConnector(progId)`
  and `CreateAuditLogApiConnector()`, and applies login / EnterCompany results (`ApplyLoginResult`,
  `ApplyEnterCompanyResult`, `ClearCompanyContext`). `ResetDefineCache` discards the cached definition data after a
  tenant switch.
- `ClientInfo.InitializeAsync(SupportedConnectTypes)` -- reads the stored endpoint and the command line and connects.
  It returns `false` when the endpoint is missing, malformed or unreachable; the host then shows its own connection
  setup and calls `InitializeAsync(endpoint)` (or `SetEndpointAsync`) with what the user entered.
- `ClientInfo.LocalServiceProvider` -- the backend service provider of a head that runs the backend in-process
  (built with `AddPolhemFramework`); the in-process `ApiClient` dispatches to it.
- `ClientInfo.UseDefinitionLoader` / `DefinitionLoader` -- localized form definitions through `FormDefinitionLoader`;
  `ApiFormDefinitionSource` uses the loader when there is one and the stored definitions otherwise.

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

### API-Backed Services

- `Connectors/FormApiConnectorFactory` -- creates the `FormApiConnector` of a program from the current client. It takes
  the client as a delegate because a new endpoint replaces the client.
- `Definitions/ApiFormDefinitionSource` -- reads the form schema, layout and currency and unit masters from the backend;
  with a definition loader the schema is localized to the current UI culture and the layout carries the tenant's
  customization.
- `ClientUserContext` -- the `IUserContext` of the signed-in user, read from `ClientInfo`.
- `DependencyInjection/PolhemUIApiBridgeServiceCollectionExtensions.AddPolhemUIApiBridge()` -- registers the
  services above.

## Design Conventions

- **Per-process identity model** -- `ClientInfo` is static and holds one signed-in user for the process. Correct for a
  desktop, mobile or browser head; wrong for a multi-user host, where every session would write the same fields.
- **Framework-agnostic** -- no UI-framework types leak in, so the same bridge serves every single-user front end.
- **Pluggable endpoint / API key storage** -- hosts replace `ClientInfo.EndpointStorage` and
  `ClientInfo.ApiKeyStorage` with platform-appropriate implementations.
- **Async-friendly initialization** -- `InitializeAsync` / `SetEndpointAsync` validate the endpoint
  and initialize the connector without blocking, so they are safe on single-threaded runtimes
  (browser WASM).
- **Nullable reference types** enabled (`<Nullable>enable</Nullable>`).

## Directory Structure

- project root -- `ClientInfo`, `ClientUserContext`, `IEndpointStorage`, `IApiKeyStorage`, `FileEndpointStorage`
- `Connectors/` -- `FormApiConnectorFactory`
- `Definitions/` -- `ApiFormDefinitionSource`
- `DependencyInjection/` -- `PolhemUIApiBridgeServiceCollectionExtensions`
