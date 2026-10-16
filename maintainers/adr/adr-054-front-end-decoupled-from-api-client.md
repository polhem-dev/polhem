# ADR-054: Front ends reach the connector through injected contracts

## Status

**Accepted (2026-10-16)**

A breaking change within 1.x under [ADR-052](adr-052-breaking-changes-in-1-x-minors.md). Amends
[ADR-013](adr-013-frontend-api-connection-strategy.md) (where the native heads' connection state lives),
[ADR-038](adr-038-definition-dependency-boundary.md) (which assemblies the dependency lock governs) and
[ADR-053](adr-053-api-client-composition-root.md) (`ClientInfo` moves to `Polhem.UI.ApiBridge`). Tracked in
[polhem-dev/polhem#107](https://github.com/polhem-dev/polhem/issues/107).

## Context

The framework's layers are UI > connector > business object > repository. Business objects already reach the
repository through `Polhem.Repository.Abstractions`, and the connector reaches business objects over the wire. The UI
did not follow the pattern: it was wired straight into the connector implementation.

- `Polhem.UI.Core`, the layer every native front end shares, referenced `Polhem.Api.Client`. Through
  `Polhem.Api.Core` that put MessagePack and the JSON-RPC **server** packages into every front end's dependency
  closure, and `Polhem.UI.Avalonia` referenced `Polhem.Api.Client` a second time directly.
- The views read a static, process-wide `ClientInfo` for the connector, the definitions, the user's capabilities,
  time zone and company. A form could not be put on screen without a Polhem backend, so the controls were not usable as
  a plain control library.
- Because `ClientInfo` holds one signed-in user per process, `Polhem.Web.Blazor.Server` could not reference
  `Polhem.UI.Core`, and the value rules both heads need lived in `Polhem.Api.Client` instead, where they had no
  business being.

## Decision

### 1. Three foundation assemblies are shared by front end and back end

`Polhem.Core`, `Polhem.Definition` and `Polhem.Api.Contracts` are the assemblies both sides reference. They may hold
nothing that belongs to one side only: no database, MessagePack, JSON-RPC or ASP.NET for the back end, no UI framework,
local file storage or user state for the front end. `Polhem.Api.Contracts` is not at the bottom (it depends on
`Polhem.Definition`), but it is inherited the same way, which is why `POLHEM9001` already governed it.

### 2. The connector contract lives in `Polhem.Api.Contracts`

`IFormApiConnector` (list, lookup, new, load, save, delete) and `IFormApiConnectorFactory` mirror the client's
`FormApiConnector` and return the response interfaces the contracts already had (`IGetListResponse` and the rest).
`FormApiConnector` implements the contract directly; its public methods keep returning the concrete messages, and the
contract members forward to them.

### 3. `Polhem.UI.Core` holds what every front end shares, and no transport

It references `Polhem.Api.Contracts` only. It holds the UI's service contracts (`IFormDefinitionSource`,
`IUserContext`), the form value rules every head shares (`FormValueBinding`, `FormDataGuard`, the element capability
resolver, moved from `Polhem.Api.Client`), a definition source over `IDefineStorage` for applications without a
backend, simple response types for an application that implements the connector itself, and `UIServices`.

### 4. Controls created from markup resolve services through `UIServices`

A XAML loader cannot pass constructor arguments, so a single-user head sets `UIServices.Provider` once. A view resolves
in this order: its own property (`FormConnector`, `DefinitionSource`), then its `Resolve*` hook, then the provider. With
nothing registered the view runs without a backend, on the schema, layout and connector the host set.

### 5. `Polhem.UI.ApiBridge` connects a single-user head to a Polhem backend

A new, UI-framework-agnostic package that references `Polhem.UI.Core` and `Polhem.Api.Client`. It holds `ClientInfo`
and the endpoint storage, moved unchanged, and the API-backed implementations of the UI.Core services, registered with
`AddPolhemUIApiBridge()`. The connector does not reference the UI: an adapter package does the joining, so the
dependency points from the bridge to both sides and never from `Polhem.Api.Client` up to the UI.

### 6. The Blazor head shares `Polhem.UI.Core` but not its process-wide parts

It references `Polhem.UI.Core` for the value rules. It must not use `UIServices` or reference `Polhem.UI.ApiBridge`,
both of which hold one user per process; `UIServicesUsageGateTests` reads the compiled assembly's references and fails
when it does.

### 7. Removed

`IUIViewService` and `ClientInfo.UIViewService`: no head implemented the service, and the connection flow called back
into the UI only to show a setup screen. `ClientInfo.InitializeAsync(SupportedConnectTypes)` returns `false` and the
head shows its own. The `AccessToken` property of `FormView` and `ListView` and its `ResolveAccessToken` hook: since
ADR-053 the client's session holds the token, and nothing read the property. The views' `DefinitionLoader` property is
replaced by `DefinitionSource`.

### 8. Enforcement

`POLHEM9001` (`src/Directory.Build.targets`) now also governs `Polhem.UI.Core` and `Polhem.UI.Avalonia`: a reference to
`Polhem.Api.Client`, `Polhem.Api.Core` or `Polhem.UI.ApiBridge` fails the build. `FrontEndDependencyGateTests`
(`tests/Polhem.UI.Avalonia.UnitTests`) walks the transitive closure, which the build-time lock cannot see.

## Consequences

- **A front end's closure is the foundation assemblies, its UI framework and the expression engine.** MessagePack and
  the JSON-RPC packages reach only a head that references the bridge.
- **The controls work without a Polhem backend.** An application implements `IFormApiConnector` over its own data;
  `samples/Avalonia.DemoCenter` shows `ListView` and `FormView` over an in-memory connector.
- **A future front end (another UI framework) reuses the bridge unchanged.** It references `Polhem.UI.Core` and
  resolves the same services.
- **A head with a backend wires one more line**: `UIServices.Provider = services.AddPolhemUIApiBridge()...`.
- **`UIServices.Provider` is process-wide state.** Tests that set it restore it, and multi-user hosts are kept away from
  it by the gate in decision 6.
- **Breaking.** `ClientInfo` and the endpoint storage change namespace and package, the moved helpers change namespace,
  `FormConnector` and the lookup APIs take `IFormApiConnector`, and the removed members are gone. The migration steps
  are in the release notes.

## Alternatives considered

### Put the UI's contracts in `Polhem.Definition` and keep `Polhem.UI.Core` as the bridge

Rejected: `Polhem.UI.Core` would still carry the whole API chain into every front end, the UI would still be wired to
the connector implementation, and the definition layer would gain UI contracts and a process-wide setting it has no
use for.

### Let `Polhem.Api.Client` implement contracts declared in `Polhem.UI.Core`

Rejected: the connector would reference the UI layer, against the direction of the layers, and every non-UI consumer
of the client (the Blazor head's server side, the load tests) would inherit `Polhem.UI.Core`.

### Keep the front end free of every `Polhem.Api.*` assembly, including the contracts

Considered first, and dropped. What the front end must not carry is the transport (`Polhem.Api.Core`,
`Polhem.Api.Client`); `Polhem.Api.Contracts` has no external dependency and already holds the response shapes. Declaring
the connector contract there lets `FormApiConnector` implement it without an adapter.

## Out of scope

`Polhem.Api.Client` still carries the JSON-RPC server packages through `Polhem.Api.Core`, which mixes the shared
message types with the server-side dispatch. It does not cross into the business-object layer and no longer reaches a
front end; splitting `Polhem.Api.Core` is separate work.
