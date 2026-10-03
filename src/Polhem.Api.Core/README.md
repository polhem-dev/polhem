# Polhem.Api.Core

> Core API framework handling JSON-RPC execution, the payload encryption pipeline, authorization validation, and type mapping.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: API Layer (core engine)
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/architecture/dependency-map.md). Not enumerated here — the csproj files are the authority, and a prose copy in every package README drifts with nothing to catch it. These did: `Polhem.Hosting` was missing as a dependent from four of them for months after it was extracted.

## Target Framework

- `net10.0` -- access to modern runtime APIs and performance improvements

## Key Features

### JSON-RPC Execution

- The protocol is [`Polhem.JsonRpc.Server`](https://github.com/polhem-dev/polhem-jsonrpc): its dispatcher parses
  `ProgId.Action` method identifiers and invokes the target method. Which methods an action name can reach is decided
  by `JsonRpcMethod.IsResolvableAction` there.
- `Dispatch/` plugs Polhem into it: `PolhemObjectFactory` checks the API key and the `Authorization` header of an HTTP
  call and obtains the business object from `IBusinessObjectFactory`; `PolhemAccessFilter` applies
  `[ApiAccessControl]`; `PolhemPayloadFilter` opens and writes the payload envelope through
  `Polhem.JsonRpc.Payload.Server`; `PolhemParameterBinder` binds the argument; `PolhemExceptionMapper` maps errors.
  `PolhemJsonRpc.CreateServerOptions` assembles them.
- Error responses -- a framework exception meant for the end user (`UserMessageException` and related types)
  reaches the caller with its message; other exceptions answer a fixed message per error code, and the real
  message is logged by `PolhemExceptionMapper`.

### Payload Security Pipeline

The envelope, the Serialize -> Compress -> Encrypt pipeline, the replay frame and the replay store are in
[`Polhem.JsonRpc.Payload`](https://github.com/polhem-dev/polhem-jsonrpc). This package supplies Polhem's side of it:

- `PolhemPayload` -- builds the package's `PayloadOptions` the framework's way: MessagePack as the default codec,
  the framework's JSON spellings and type names, and the compressor and encryptor named in the deployment's
  `ApiPayloadOptions` (the encryptor `none` only in debug mode). A server registers them with `AddPolhemPayload`
  (`Polhem.Hosting`); a client keeps them in `ApiClientInfo.PayloadOptions` (`Polhem.Api.Client`).
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
  `ApiClientInfo.PayloadOptions` on a .NET client; **client and server must be set to the same value**.
- `ApiReplayProtection` -- third dimension of `ApiAccessControlAttribute`, declaring per method whether sequences
  are checked. A host built with `AddPolhemFramework` logs a startup warning when methods declare it while the
  frame is off.

Sequence checks apply to `Encrypted` payloads, where the payload HMAC covers the frame. A `Plain` call carries no
frame and is not checked, and an `Encoded` frame is not authenticated, so declare replay-protected methods at the
`Encrypted` protection level. See [ADR-042](../../maintainers/adr/adr-042-api-replay-protection.md) for the rollout order
and the details.

### Authorization & Access Control

- `IApiAuthorizationValidator` / `ApiAuthorizationValidator` -- validates authorization context for incoming requests.
  `AddPolhemFramework` registers the default; a host replaces it by registering its own.
- `ApiAuthorizationContext` / `ApiAuthorizationResult` -- authorization input and outcome types.
- `ApiAccessValidator` -- enforces method-level protection via `ApiAccessControlAttribute`.
- `ApiCallContext` -- per-call metadata (token, protection level, caller identity).

### Type Mapping

- `ApiOutputConverter` -- converts a business object result into the wire response type (property copy by name;
  the inbound direction is internal).
- `ApiHeaders` -- standard header constants for API communication.
- `PayloadFormat` -- how a payload travels: `Plain`, `Encoded` (serialized and compressed) or `Encrypted`.
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
| `PolhemJsonRpc` | Creates the JSON-RPC server options the framework serves its API with |
| `PolhemPayload` | Builds the payload options the framework's way |
| `MessagePackPayloadCodec` | The `messagepack` body codec |
| `ApiAccessValidator` | Method-level protection via `ApiAccessControlAttribute` |
| `PayloadFormat` | Payload format enum (`Plain`, `Encoded`, `Encrypted`) |
| `ApiAuthorizationValidator` | Request authorization validation |
| `ApiCallContext` | Per-call metadata (token, protection, identity) |

## Design Conventions

- **Strategy Pattern** -- the codec, compressor and encryptor are interfaces of the payload package (`IPayloadCodec`, `IPayloadCompressor`, `IPayloadEncryptor`), each replaceable on `PayloadOptions`.
- **Strict pipeline ordering** -- the payload package runs Serialize -> Compress -> Encrypt on outbound and Decrypt -> Decompress -> Deserialize on inbound; the order must not be altered.
- **Type whitelist** -- MessagePack deserialization accepts only an explicit allow-list of types.
- **Reflection-based dispatch** -- the dispatcher resolves and invokes business object methods by name, decoupling the transport layer from concrete BO types.
- **Protection levels** -- `ApiAccessControlAttribute` declares a method's `ApiProtectionLevel` and `ApiAccessRequirement`; the members and their meaning are in the XML documentation of those enums (`Polhem.Definition.Security`).
- **Nullable reference types** enabled (`<Nullable>enable</Nullable>`).

## Directory Structure

- `Authorization/` -- `IApiAuthorizationValidator`, `ApiAuthorizationValidator`, `ApiAuthorizationContext`, `ApiAuthorizationResult`
- `Conversion/` -- .NET object-model conversion between API and BO types (`ApiOutputConverter`)
- `Json/` -- JSON converters for `object`-typed members
- `Dispatch/` -- the components that plug Polhem into the `Polhem.JsonRpc.Server` dispatcher
- `JsonRpc/` -- `ActionPayloadType`, `DateTimeWireGuard`, the error codes and the error contract
- `Messages/` -- `ApiRequest`, `ApiResponse`, `ApiHeaders`, `PayloadFormat`, `ExecFunc*`, and the `System/`, `Form/` and `AuditLog/` messages
- `MessagePack/` -- the internal MessagePack infrastructure and formatters
- `Transformers/` -- Polhem's side of the payload pipeline (`PolhemPayload`, `MessagePackPayloadCodec`, `PayloadCodecNames`)
- `Validator/` -- `ApiAccessValidator`, `ApiCallContext`
- `Wire/` -- `WireValueCode` (the discriminator both wires share)

The namespace layout follows the design principles in [ADR-008](../../maintainers/adr/adr-008-polhem-db-namespace-layout.md):
contracts grouped by responsibility (`Messages` for message types, `Conversion` for type
conversion, `Transformers` for the byte-level pipeline, etc.); the root is reserved for cross-cutting
infrastructure, and holds none at present.
