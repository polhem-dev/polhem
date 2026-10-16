# Polhem.Api.Core

> The wire layer the Polhem API client and server share: request and response messages, the payload codecs, compression and encryption, the error contract, and type mapping.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: API Layer (shared by both ends). The server side (dispatch, authorization, access control) is
  [`Polhem.Api.Server`](../Polhem.Api.Server/README.md); see [ADR-055](../../maintainers/adr/adr-055-api-core-split-from-server.md).
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/architecture/dependency-map.md). Not enumerated here — the csproj files are the authority, and a prose copy in every package README drifts with nothing to catch it. These did: `Polhem.Hosting` was missing as a dependent from four of them for months after it was extracted.

## Target Framework

- `net10.0` -- access to modern runtime APIs and performance improvements

## Key Features

### Error Contract

- `JsonRpcErrorCode` and `JsonRpcErrorContract` -- the error codes and the table that pairs each with an exception
  type. The server maps an exception to a code with it, and `ApiConnector` (`Polhem.Api.Client`) rebuilds the
  exception from the code, so both ends read the same table.

### In-Process Calls

- `IInProcessTransportFactory` -- creates the transport an in-process client (`PolhemApiClient.CreateLocal`) sends
  its calls through. `AddPolhemFramework` registers the implementation from `Polhem.Api.Server`, so a client does
  not reference the server side; `ApiClientDependencyGateTests` (`tests/Polhem.Api.Client.UnitTests`) fails when it does.

### Payload Security Pipeline

The envelope, the Serialize -> Compress -> Encrypt pipeline, the replay frame and the replay store are in
[`Polhem.JsonRpc.Payload`](https://github.com/polhem-dev/polhem-jsonrpc). This package supplies Polhem's side of it:

- `PolhemPayload` -- builds the package's `PayloadOptions` the framework's way: MessagePack as the default codec,
  the framework's JSON spellings and type names, and the compressor and encryptor named in the deployment's
  `ApiPayloadOptions` (the encryptor `none` only in debug mode). A server registers them with `AddPolhemPayload`
  (`Polhem.Hosting`); a client keeps them in `PolhemApiClient.PayloadOptions` (`Polhem.Api.Client`).
- `MessagePackPayloadCodec` -- the `messagepack` body codec over the framework's formatters. The `json` codec is the
  package's, with the framework's options; others are registered with `PayloadOptions.RegisterCodec`.
- `PayloadCodecNames` -- the codec names a payload declares.

### Body Codec Negotiation

The body codec of an `Encoded` or `Encrypted` payload is not a deployment setting: each request declares it in the
payload envelope and the server answers with the same codec. A request that declares none is read as MessagePack,
which is what every client that predates negotiation sends. On the client, `ApiConnector.PayloadCodec`
(`Polhem.Api.Client`) chooses it. See [ADR-044](../../maintainers/adr/adr-044-payload-codec-negotiation.md).

### Anti-Replay (optional, off by default)

- The frame (timestamp and sequence number, inside the envelope ahead of the body) and the replay store are the
  payload package's: `PayloadFrame`, `IPayloadReplayStore` and the in-memory default `MemoryPayloadReplayStore`. A
  multi-node deployment registers an `IPayloadReplayStore` over a shared store; Polhem uses the access token as the
  scope a sequence number is unique in.
- `PayloadOptions.RequireFrame` -- the master switch, set through `AddPolhemPayload` on the server and
  `PolhemApiClient.PayloadOptions` on a .NET client; **client and server must be set to the same value**.
- `ApiReplayProtection` -- third dimension of `ApiAccessControlAttribute`, declaring per method whether sequences
  are checked. A host built with `AddPolhemFramework` logs a startup warning when methods declare it while the
  frame is off.

Sequence checks apply to `Encrypted` payloads, where the payload HMAC covers the frame. A `Plain` call carries no
frame and is not checked, and an `Encoded` frame is not authenticated, so declare replay-protected methods at the
`Encrypted` protection level. See [ADR-042](../../maintainers/adr/adr-042-api-replay-protection.md) for the rollout order
and the details.

### Type Mapping

- `ApiOutputConverter` -- converts a business object result into the wire response type (property copy by name;
  the inbound direction is internal).
- `ApiHeaders` -- standard header constants for API communication.
- `DateTimeWireGuard` -- enforces the date and time wire invariants of
  [ADR-032](../../maintainers/adr/adr-032-datetime-timezone.md) on the responses that carry them.

### MessagePack Infrastructure

> These types are `internal`. They are documented here because they define the wire behaviour, but
> they are not part of the package's public surface — use `MessagePackPayloadCodec` (public)
> to reach the same pipeline.

- `SafeMessagePackSerializerOptions` / `WireTypeWhitelist` -- restrict deserialization to an allow-list of types.
- `MessagePackCodec` -- encoder/decoder for MessagePack serialization.
- `WireContracts` -- the explicit formatter registrations for every wire type. The contractless
  resolver is a desktop-only convenience, not the carrying mechanism: .NET for iOS turns dynamic
  code off, and an unregistered type fails there outright (see
  [ADR-037](../../maintainers/adr/adr-037-wire-explicit-registration.md)).
- `WireValueFormatter` -- discriminated envelope for `object`-typed members (filter values,
  parameter values, table cells).

### Built-in Messages

- Request/response types for the built-in operations live in `Messages/System/` (login, sessions, companies,
  definitions, API keys), `Messages/Form/` (list, data, save, delete, lookup) and `Messages/AuditLog/`;
  `ExecFuncRequest` / `ExecFuncResponse` sit in `Messages/`.

## Key Public APIs

| Class / Interface | Purpose |
|-------------------|---------|
| `PolhemPayload` | Builds the payload options the framework's way |
| `MessagePackPayloadCodec` | The `messagepack` body codec |
| `JsonRpcErrorContract` | The error codes both ends map exceptions with |
| `IInProcessTransportFactory` | The transport of an in-process client |

## Design Conventions

- **Strategy Pattern** -- the codec, compressor and encryptor are interfaces of the payload package (`IPayloadCodec`, `IPayloadCompressor`, `IPayloadEncryptor`), each replaceable on `PayloadOptions`.
- **Strict pipeline ordering** -- the payload package runs Serialize -> Compress -> Encrypt on outbound and Decrypt -> Decompress -> Deserialize on inbound; the order must not be altered.
- **Type whitelist** -- MessagePack deserialization accepts only an explicit allow-list of types.
- **No server side** -- this package references no JSON-RPC server package; `POLHEM9001` (`src/Directory.Build.targets`) and `ApiClientDependencyGateTests` (`tests/Polhem.Api.Client.UnitTests`) fail when one is added.
- **Protection levels** -- `ApiAccessControlAttribute` declares a method's `ApiProtectionLevel` and `ApiAccessRequirement`; the members and their meaning are in the XML documentation of those enums (`Polhem.Definition.Security`).
- **Nullable reference types** enabled (`<Nullable>enable</Nullable>`).

## Directory Structure

- `Conversion/` -- .NET object-model conversion between API and BO types (`ApiOutputConverter`)
- `Json/` -- JSON converters for `object`-typed members
- `JsonRpc/` -- `ActionPayloadType`, `DateTimeWireGuard`, the error codes and the error contract, `IInProcessTransportFactory`
- `Messages/` -- `ApiRequest`, `ApiResponse`, `ApiHeaders`, `ExecFunc*`, and the `System/`, `Form/` and `AuditLog/` messages
- `MessagePack/` -- the internal MessagePack infrastructure and formatters
- `Transformers/` -- Polhem's side of the payload pipeline (`PolhemPayload`, `MessagePackPayloadCodec`, `PayloadCodecNames`, and the internal type resolver that names payload types on the wire)
- `Wire/` -- `WireValueCode` (the discriminator both wires share)

The namespace layout follows the design principles in [ADR-008](../../maintainers/adr/adr-008-polhem-db-namespace-layout.md):
contracts grouped by responsibility (`Messages` for message types, `Conversion` for type
conversion, `Transformers` for the byte-level pipeline, etc.); the root is reserved for cross-cutting
infrastructure, and holds none at present.
