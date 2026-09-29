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

- `JsonRpcExecutor` -- parses `ProgId.Action` method identifiers, obtains the business object from
  `IBusinessObjectFactory`, and invokes the target method. Which methods an action name can reach is decided by
  `JsonRpcExecutor.IsResolvableAction`.
- `JsonRpcRequest` / `JsonRpcResponse` / `JsonRpcError` -- standard JSON-RPC 2.0 message types.
- `ApiPayload` / `ApiPayloadConverter` -- payload wrapping and conversion for JSON-RPC transport.
- Error responses -- a framework exception meant for the end user (`UserMessageException` and related types)
  reaches the caller with its message; other exceptions answer a fixed message per error code, and the real
  message goes to `JsonRpcExecutor.Logger`.

### Payload Security Pipeline

- `ApiPayloadTransformer` -- orchestrates the Serialize -> Compress -> Encrypt pipeline (and the reverse on inbound payloads).
- `IApiPayloadSerializer` -- the body codec. `MessagePackPayloadSerializer` and `JsonPayloadSerializer` ship with
  the framework (names in `PayloadCodecNames`); others are added with `ApiServiceOptions.RegisterPayloadCodec`.
- `IApiPayloadCompressor` / `GzipPayloadCompressor` -- pluggable Gzip compression.
- `IApiPayloadEncryptor` / `AesPayloadEncryptor` -- pluggable AES-CBC-HMAC encryption.
- `ApiPayloadOptionsFactory` -- creates the compressor and encryptor named in the deployment's `ApiPayloadOptions`.

### Body Codec Negotiation

The body codec of an `Encoded` or `Encrypted` payload is not a deployment setting: each request declares it in the
payload envelope and the server answers with the same codec. A request that declares none is read as MessagePack,
which is what every client that predates negotiation sends. On the client, `ApiConnector.PayloadCodec`
(`Polhem.Api.Client`) chooses it. See [ADR-044](../../maintainers/adr/adr-044-payload-codec-negotiation.md).

### Anti-Replay (optional, off by default)

- `ApiPayloadFrame` -- timestamp and sequence number carried inside the envelope, ahead of the payload body.
- `IReplayWindowStore` -- decides per session, in one atomic call, whether a sequence number may be accepted.
  The default `MemoryReplayWindowStore` keeps a sliding window in process memory; a multi-node deployment can
  implement the interface over a shared store and assign it to `ApiServiceOptions.ReplayWindowStore`.
- `ApiServiceOptions.RequireWireFrame` -- the master switch; **client and server must be set to the same value**.
- `ApiReplayProtection` -- third dimension of `ApiAccessControlAttribute`, declaring per method whether sequences
  are checked. A host built with `AddPolhemFramework` logs a startup warning when methods declare it while the
  frame is off.

Sequence checks apply to `Encrypted` payloads, where the payload HMAC covers the frame. A `Plain` call carries no
frame and is not checked, and an `Encoded` frame is not authenticated, so declare replay-protected methods at the
`Encrypted` protection level. See [ADR-042](../../maintainers/adr/adr-042-api-replay-protection.md) for the rollout order
and the details.

### Authorization & Access Control

- `IApiAuthorizationValidator` / `ApiAuthorizationValidator` -- validates authorization context for incoming requests.
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
> they are not part of the package's public surface — use `MessagePackPayloadSerializer` (public)
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
| `JsonRpcExecutor` | Parses `ProgId.Action`, creates BO, invokes method |
| `ApiServiceOptions` | Process-wide configuration of the pipeline components, codecs, authorization validator and replay store |
| `ApiPayloadTransformer` | Serialize -> Compress -> Encrypt pipeline |
| `ApiAccessValidator` | Method-level protection via `ApiAccessControlAttribute` |
| `PayloadFormat` | Payload format enum (`Plain`, `Encoded`, `Encrypted`) |
| `ApiAuthorizationValidator` | Request authorization validation |
| `ApiCallContext` | Per-call metadata (token, protection, identity) |
| `IReplayWindowStore` | Replaceable per-session sequence check |

## Design Conventions

- **Strategy Pattern** -- serializer, compressor, and encryptor are injected via interfaces (`IApiPayloadSerializer`, `IApiPayloadCompressor`, `IApiPayloadEncryptor`), allowing each stage to be replaced independently.
- **Strict pipeline ordering** -- the payload transformer runs Serialize -> Compress -> Encrypt on outbound and Decrypt -> Decompress -> Deserialize on inbound; the order must not be altered.
- **Type whitelist** -- MessagePack deserialization accepts only an explicit allow-list of types.
- **Reflection-based dispatch** -- `JsonRpcExecutor` resolves and invokes business object methods by name, decoupling the transport layer from concrete BO types.
- **Protection levels** -- `ApiAccessControlAttribute` declares a method's `ApiProtectionLevel` and `ApiAccessRequirement`; the members and their meaning are in the XML documentation of those enums (`Polhem.Definition.Security`).
- **Nullable reference types** enabled (`<Nullable>enable</Nullable>`).

## Directory Structure

- `Authorization/` -- `IApiAuthorizationValidator`, `ApiAuthorizationValidator`, `ApiAuthorizationContext`, `ApiAuthorizationResult`
- `Conversion/` -- .NET object-model conversion between API and BO types (`ApiOutputConverter`)
- `Json/` -- JSON converters for `object`-typed members
- `JsonRpc/` -- `JsonRpcExecutor`, the JSON-RPC message types, `ApiPayload`, `ApiPayloadFrame`, `IReplayWindowStore`, `DateTimeWireGuard`
- `Messages/` -- `ApiRequest`, `ApiResponse`, `ApiHeaders`, `PayloadFormat`, `ExecFunc*`, and the `System/`, `Form/` and `AuditLog/` messages
- `MessagePack/` -- the internal MessagePack infrastructure and formatters
- `Transformers/` -- the byte-level payload pipeline (serializers, compressor, encryptor, `ApiPayloadOptionsFactory`, `PayloadCodecNames`)
- `Validator/` -- `ApiAccessValidator`, `ApiCallContext`
- `Wire/` -- `WireValueCode` (the discriminator both wires share)
- project root -- `ApiServiceOptions` (startup configuration)

The namespace layout follows the design principles in [ADR-008](../../maintainers/adr/adr-008-polhem-db-namespace-layout.md):
contracts grouped by responsibility (`Messages` for message types, `Conversion` for type
conversion, `Transformers` for the byte-level pipeline, etc.); the root reserved for cross-cutting
infrastructure (here, only `ApiServiceOptions`).
