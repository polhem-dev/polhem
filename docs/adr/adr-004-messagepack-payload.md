# ADR-004: Use MessagePack as the API Payload serialization format

[繁體中文](adr-004-messagepack-payload.zh-TW.md)

## Status

Accepted, partially superseded: the integer-key strategy by [ADR-030](adr-030-messagepack-name-based-keys.md),
and MessagePack as the only payload format by [ADR-044](adr-044-payload-codec-negotiation.md) (see the notes below).

> **Note (2026-07-22, superseded-in-part)**: this ADR's decision "MessagePack as the API Payload format" stands.
> However, the **integer `[Key]` key strategy** described under "Rationale › Schema Evolution" below has been changed
> by [ADR-030](adr-030-messagepack-name-based-keys.md) (**Accepted / carried out**) to property-name keys
> (`keyAsPropertyName`): contracts, most DTOs and non-Union collection items now use property names as wire keys.
> **Exceptions**: `[Union]` polymorphic hierarchies (such as `FilterNode`) and the DataSet wire plumbing keep integer
> `[Key]`s. When adding an ordinary MessagePack contract type, use `keyAsPropertyName`; when adding a polymorphic
> hierarchy, use integer `[Key]` + `[Union]`.
>
> **Second note (2026-08-11)**: the **operational guidance in the last two sentences of the previous paragraph no
> longer applies and must not be followed**. Since [ADR-036](adr-036-wire-serialization-externalized.md) the whole
> repository has no `[MessagePackObject]` / `[Key]` / `[Union]` any more, and the definition layer carries no
> serialization attributes; [ADR-037](adr-037-wire-explicit-registration.md) then changed wire binding to "always
> register a formatter explicitly in `WireContracts.*.cs`". Polymorphism (`FilterNode`) is now carried by a named
> `Kind` discriminator field instead of `[Union]`.<br>The previous paragraph is kept as written to preserve the
> context of the decision; **current practice always follows ADR-036 / ADR-037**.
>
> **Third note (2026-09-04, superseded-in-part)**: this ADR's "MessagePack as the API Payload format"
> has been **partly superseded** by [ADR-044](adr-044-payload-codec-negotiation.md): the body codec is now
> **negotiated per request**, so MessagePack is no longer the only option, nor a "chosen default" but a compatibility
> constant (an undeclared codec means MessagePack, because every client that predates negotiation declares nothing
> and sends MessagePack). A JSON codec sits alongside it.

## Context

The framework uses three serialization formats, each with a clear purpose:

| Format | Purpose | Scenario |
|--------|---------|----------|
| **XML** | Storing and representing definition data | Persistence and transport representation of complex types such as FormSchema and SystemSettings (when definition data is transmitted, it is first serialized to an XML string, then carried by MessagePack or JSON) |
| **MessagePack** | Front end to back end transport within the system | API Payload |
| **JSON** | Integration with external systems | Third-party system integration, the JSON-RPC envelope (see [ADR-002](adr-002-newtonsoft-json.md)) |

For transporting the API Payload between the front end and back end within the system, an efficient serialization
format has to be chosen. The options:

1. **JSON** (Newtonsoft.Json): a text format, highly readable, but large and slow
2. **MessagePack**: a binary format, small and fast
3. **Protobuf**: a binary format that requires .proto definition files

## Decision

Use `MessagePack` as the serialization format of the API Payload between the front end and back end within the
system. The JSON-RPC envelope uses JSON (as the JSON-RPC 2.0 specification requires), and the Payload field is
encoded with MessagePack and embedded as Base64.

## Rationale

- **Performance**: MessagePack serializes and deserializes markedly faster than JSON, which suits high-frequency API
  calls.
- **Size**: a binary Payload is usually 50-70% of the size of the JSON, and smaller still with GZip compression.
- **Integration with the encryption pipeline**: the Payload goes through a three-stage Serialize → Compress → Encrypt
  pipeline, and a binary format is a natural fit for the compression and encryption that follow.
- **Schema Evolution**: MessagePack's `[Key]` attribute supports adding and removing fields without breaking backward
  compatibility.
- **No .proto files needed**: unlike Protobuf, MessagePack defines the schema with C# attributes, with no extra
  definition files or code generation step.

## Trade-offs

- **Poor readability**: a binary format cannot be read directly, and debugging needs a tool to decode it.
- **Learning cost**: developers need to understand attributes such as `[MessagePackObject]` and `[Key]`.
- **Type whitelist**: to prevent deserialization attacks, the framework implements `SafeTypelessFormatter` and
  `SafeMessagePackSerializerOptions`, and a new API type must be registered at the same time.
  > **Later (2026-08-10)**: `[MessagePackObject]` / `[Key]` were retired entirely in
  > [ADR-036](adr-036-wire-serialization-externalized.md), and the whitelist is now held by
  > `WireTypeWhitelist`; the registration requirement for wire types was widened by
  > [ADR-037](adr-037-wire-explicit-registration.md) to "always register a formatter explicitly".

## Consequences

- `Polhem.Api.Core/Transformers/MessagePackPayloadSerializer.cs`: the default Payload serializer
- `Polhem.Api.Core/MessagePack/`: custom formatters (for ADO.NET types such as DataSet and DataTable)
- `Polhem.Api.Core/MessagePack/SafeMessagePackSerializerOptions.cs`: the type whitelist mechanism
- `Polhem.Api.Core/Registry/ApiContractRegistry.cs`: API type registration
  — **this type and the `Registry/` folder have since been removed** (they were meant for the "BO returns plain
  POCOs" scenario, which never took shape); the reasons are in `src/Polhem.Api.Contracts/README.md`.
- The `Polhem.Definition` types that travel on the wire (the filter nodes `FilterCondition` / `FilterGroup`, and
  collections such as `FilterNodeCollection` and `ListItemCollection`) are also serialized with MessagePack
- The API Payload format has three levels: Plain (no encoding), Encoded (MessagePack + GZip), Encrypted
  (MessagePack + GZip + AES)

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **2026-09-03: the Consequences describe a single codec.** They call `MessagePackPayloadSerializer` "the default
  Payload serializer" and spell Encoded and Encrypted as MessagePack + GZip (+ AES). Since
  [ADR-044](adr-044-payload-codec-negotiation.md) the body of an Encoded or Encrypted payload is written with the codec
  the request declares (`JsonPayloadSerializer` is the other built-in one), and MessagePack is used when none is
  declared. The compression and encryption steps are unchanged.
