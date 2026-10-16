# ADR-055: The server side of the API moves from `Polhem.Api.Core` to `Polhem.Api.Server`

## Status

**Accepted (2026-10-16)**

A breaking change within 1.x under [ADR-052](adr-052-breaking-changes-in-1-x-minors.md). Resolves the item
[ADR-054](adr-054-front-end-decoupled-from-api-client.md) left out of scope, and extends the dependency lock of
[ADR-038](adr-038-definition-dependency-boundary.md) to `Polhem.Api.Core`.

## Context

`Polhem.Api.Core` held two kinds of code:

- What both ends share: the request and response messages (`Messages/`), the MessagePack formatters
  (`MessagePack/WireContracts.*.cs`), the payload options and codecs (`Transformers/`), the error contract, and the
  date and time wire rules.
- What only the server runs: the JSON-RPC dispatch (`Dispatch/`), request authorization (`Authorization/`) and
  method-level access control (`Validator/`). Only `Dispatch/` referenced the server packages, `Polhem.JsonRpc.Server`
  and `Polhem.JsonRpc.Payload.Server`, and only it used `Microsoft.Extensions.Logging.Abstractions`.

Two assemblies referenced `Polhem.Api.Core`: the connector `Polhem.Api.Client` and the composition root
`Polhem.Hosting`. So every API client, and every front end with a backend through `Polhem.UI.ApiBridge`, carried the
server packages. `Polhem.Api.Client` also referenced `Polhem.JsonRpc.Server` itself: its in-process mode
(`PolhemApiClient.CreateLocal`) created an `InProcessTransport` over the backend's `JsonRpcDispatcher`.

One constraint decides the direction of the split: **the wire names message types by assembly**. An encoded payload
names its type as `"<full name>, Polhem.Api.Core"` (`PolhemPayloadTypeResolver.GetTypeName`). The server screens
the name against an allow-list before resolving it, `wire-contracts/type-names.ts` is generated from it, and
polhem-connector-js sends those strings. A message type that changes assembly or namespace changes the wire.

## Decision

### 1. `Polhem.Api.Core` keeps what both ends share; the server side moves to `Polhem.Api.Server`

`Dispatch/` (except the payload type resolver), `Authorization/` and `Validator/` move to the new package
`Polhem.Api.Server`, in the namespaces `Polhem.Api.Server.Dispatch`, `Polhem.Api.Server.Authorization` and
`Polhem.Api.Server.Validator`. It references `Polhem.JsonRpc.Server`, `Polhem.JsonRpc.Payload.Server` and the logging
abstractions; `Polhem.Api.Core` references only `MessagePack` and `Polhem.JsonRpc.Payload` outside the framework.
`Polhem.Hosting` references `Polhem.Api.Server` instead of `Polhem.Api.Core`.

`PolhemPayloadTypeResolver` stays in `Polhem.Api.Core` (`Transformers/`): `PolhemPayload` uses it to name payload
types, on the client as much as on the server.

The message types, their namespaces and the assembly name do not change, so the wire does not change:
`wire-contracts/` and `wire-fixtures/` have no diff, and polhem-connector-js needs nothing.

### 2. An in-process client reaches the dispatcher through an injected factory

`Polhem.Api.Core` declares `IInProcessTransportFactory`, which creates the transport of one call from the backend's
service provider and the caller's access token. Its only dependency is `IJsonRpcTransport` from `Polhem.JsonRpc`,
which `Polhem.Api.Core` already has. `Polhem.Api.Server` implements it as `InProcessTransportFactory`, which does what
the client used to do: an `InProcessTransport` over the `JsonRpcDispatcher`, with the token in
`PolhemJsonRpc.AccessTokenItem`. `AddPolhemFramework` registers it.

`PolhemApiClient.CreateLocal(IServiceProvider, …)` keeps its signature. Its transport resolves the factory from that
provider on each call and throws an `InvalidOperationException` naming `AddPolhemFramework` when it is not there, as it
did before for a missing dispatcher. `Polhem.Api.Client` no longer references any JSON-RPC server package.

### 3. `Polhem.Api.Server` reads internals of `Polhem.Api.Core`, at the exact same version

The server needs some members of the shared half that no client should call: the request type an action's body is read
into (`ActionPayloadType`), the inbound converter (`ApiInputConverter`), the exception for invalid parameters, and the
code lookup of the error contract. They stay `internal`, and `Polhem.Api.Core` grants `InternalsVisibleTo` to
`Polhem.Api.Server`.

Internals have no compatibility promise, so the two packages only work from the same release. NuGet writes a project
reference as a minimum version, which would let an application upgrade `Polhem.Api.Core` alone. The
`PinPolhemApiCoreVersion` target in `src/Polhem.Api.Server/Polhem.Api.Server.csproj` rewrites that dependency to the
exact version (`[x.y.z]`). It edits an item of NuGet's pack targets, which is not a public extension point, so the
pack fails when that item no longer names `Polhem.Api.Core`, and a second target fails it when the nuspec is generated
without the pin having run (for example after NuGet renames the target it hooks).

### 4. `Polhem.Api.Core` is locked, without becoming a foundation assembly

`Polhem.Api.Core` is not one of the foundation assemblies of ADR-054: the front-end layer still may not reference it,
because it carries MessagePack. But every API client inherits it, so `POLHEM9001` now governs it with its own
allowlist (`src/Directory.Build.targets`), and `ApiClientDependencyGateTests` (`tests/Polhem.Api.Client.UnitTests`)
checks the transitive closure: `Polhem.Api.Core` stays within MessagePack, the payload envelope and the foundation
assemblies, and `Polhem.Api.Client` reaches none of the server side.

### 5. Tests follow the code

Tests of the dispatcher, the authorization and the access control, and every round trip that goes through the
dispatcher (`*JsonRpcRoundTripTests`), move to `tests/Polhem.Api.Server.UnitTests`. The wire gates
(`WireContractDriftTests`, `WireCodecParityTests`, `WireFixtureTests`, the contract generator) stay in
`tests/Polhem.Api.Core.UnitTests`. The mobile AOT gate in `build-ci.yml` runs both, because the dispatch round trips
encode and decode every message the way a mobile client does.

## Consequences

- An application that implements `IApiAuthorizationValidator`, or uses `ApiAuthorizationContext`,
  `ApiAuthorizationResult`, `ApiAccessValidator`, `ApiCallContext` or `PolhemJsonRpc`, changes its `using` from
  `Polhem.Api.Core.*` to `Polhem.Api.Server.*`. A host that builds the dispatcher without `Polhem.Hosting` adds a
  reference to `Polhem.Api.Server`. Applications built on `AddPolhemFramework` need no reference change.
- A host that builds a service provider for `CreateLocal` without `AddPolhemFramework` registers
  `IInProcessTransportFactory` itself (with `InProcessTransportFactory`), next to the dispatcher it already registers.
- `Polhem.Api.Client` and every front end with a backend lose `Polhem.JsonRpc.Server` and
  `Polhem.JsonRpc.Payload.Server` from their closure.
- `Polhem.Api.Server` and `Polhem.Api.Core` are released together and must be used at the same version. The exact
  dependency makes NuGet report a mismatch, but not always as an error: two packages that need different versions fail
  the restore (NU1107), while an application that references a newer `Polhem.Api.Core` directly only gets a warning
  (NU1608) and then fails at run time on a missing internal member.
- Log categories follow the namespaces: the masked-failure log and the API key lookup warnings now come from
  `Polhem.Api.Server.Dispatch.*`, so a logging filter keyed on `Polhem.Api.Core` no longer matches them.

## Alternatives considered

### Move the shared half out instead, and keep `Polhem.Api.Core` for the server

Every message type would change assembly, so every type name on the wire would change: the generated
`type-names.ts`, the fixtures and polhem-connector-js would all have to move in the same release, and clients of an
earlier release could no longer talk to the server. The name `Polhem.Api.Core` would also end up on the half that is
not shared. A wire break buys nothing here.

### Merge the server side into `Polhem.Hosting`

No new package, but `Polhem.Hosting` is the composition root: it wires the layers and holds the hosted-service shells.
The dispatcher filters and the authorization are runtime machinery an application replaces or extends, not wiring, and
a host that assembles the dispatcher itself would have to take the whole composition root to get them.

### Make the shared members public instead of `InternalsVisibleTo`

That avoids the version coupling, but puts the request type resolution and the inbound conversion, which are part of
what the server trusts a body with, into the public API that 1.x promises to keep.

### Keep `Polhem.JsonRpc.Server` in `Polhem.Api.Client`

Moving only Polhem's own server code would have left the in-process mode as it was. The package is small and depends
only on `Polhem.JsonRpc`, but every front end would still carry a server package, which is what ADR-054 set out to
remove, and the injected factory costs one interface.

## Out of scope

- The dependencies of `Polhem.JsonRpc.Payload.Client` itself, which belong to polhem-dev/polhem-jsonrpc.
- Changing the namespaces or the wire names of the message types; that is a wire change and would have to be planned
  together with polhem-connector-js.
