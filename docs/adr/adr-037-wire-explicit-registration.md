# ADR-037: Every wire type registers a formatter explicitly; `object` values use a discriminated envelope

[繁體中文](adr-037-wire-explicit-registration.zh-TW.md)

## Status

**Accepted (2026-08-10)**: the decision has been carried out.

Related: [ADR-030](adr-030-messagepack-name-based-keys.md), [ADR-036](adr-036-wire-serialization-externalized.md)

## Context

[ADR-036](adr-036-wire-serialization-externalized.md) removed every MessagePack attribute, and wire types came to be
carried by `ContractlessStandardResolver`. That decision rested on "MessagePack 3.x has a reflection fallback, so it
works under mobile AOT".

**That basis does not hold.** The measurement on 2026-08-10 (a controlled experiment under NativeAOT) showed:

| Case | Result |
|------|------|
| `[MessagePackObject(keyAsPropertyName: true)]` type + `StandardResolver` | ✅ Round-trips correctly |
| Unattributed POCO + `ContractlessStandardResolver` | ❌ `FormatterNotRegisteredException` |

**Contractless has no reflection fallback**; MessagePack's fallback only covers attributed contract types. And the
.NET for iOS SDK sets `DynamicCodeSupport=false` (mapped to `RuntimeFeature.IsDynamicCodeSupported`) by default for
**every configuration** of iOS / tvOS / MacCatalyst, so after ADR-036 almost no payload type could be serialized on
iOS.

There was also a separate defect older than ADR-036: `object` members such as `Parameter.Value` /
`FilterCondition.Value` go through `TypelessFormatter`, whose path for non-primitive types passes a `ref struct`
writer through `MessagePackSerializer.NonGeneric`, which needs `Reflection.Emit`. `String` / `Int32` / `Boolean` /
`Int64` / `Double` pass because they have a primitive fast path; `Decimal` / `Guid` / `DateTime` / `DateOnly` /
`Byte[]` do not.

## Decision

### 1. Every type in the wire type closure registers a formatter explicitly

Contractless is no longer the default carrying mechanism. The closure covers:

- Message contract types (`Polhem.Api.Core.Messages.*`, `Polhem.Api.Contracts.*`)
- The definition-layer types and framework collections transitively reachable from them
- Closed generic instantiations: `List<T>` / `Dictionary<K,V>` / `T?` / arrays / **enums** (these are also created
  through `MakeGenericType` and have no native code under AOT)

Most types declare their members with `WireContract.For<T>().Member(...)`; `WireObjectFormatter<T>` reads and writes
them by name, one by one, according to that table. The key is that the `TValue` of `Member<TValue>` is a
**compile-time** generic parameter, so the serialization calls are closed generics throughout and never touch
reflection or dynamic code.

Contractless still sits at the end of the resolver chain, but its role becomes **a convenience fallback on desktop**
(for example for types a host puts into `Parameter.Value` itself); it is no longer the carrying mechanism for
framework types.

### 2. `object` values use a discriminated envelope

`TypelessFormatter` is replaced by `WireValueFormatter`. The envelope is a two-element array:

```
[ <discriminator:int> | <type name:string> , <value> ]
```

- **Discriminator**: the framework's own closed set of types (`Boolean`...`DataTable`, `DBNull`, `object[]`); each
  type builds closed generic read and write delegates when the class is initialized.
- **Type name**: the escape hatch for the configurable extension point `SysInfo.AllowedTypeNamespaces`. It still goes
  through the non-generic overload, **so it still only works on a runtime with dynamic code**.

The whitelist semantics are unchanged (`WireTypeWhitelist`), but the check **moves forward to the writing side**, and
the reading side filters the type name **before** `Type.GetType`: a type outside the whitelist is never loaded at all.

### 3. Drift is guarded by tests, not by manual constants

ADR-036 used a `WireMemberCount` constant on each formatter as the guard. It is replaced by two checks in
`WireContractDriftTests`:

1. Walk the wire type closure once and assert that every type has an explicitly registered formatter;
2. Compare the member list of each `WireContract` with the current shape of its type, one by one.

A wire member is defined the same way as for JSON: a public readable and writable property not marked `[JsonIgnore]`
(framework-managed members such as `Tag` / `Key` / `SerializeState` already carry that attribute).

### 4. Regression gate

`dotnet test … -p:DynamicCodeSupport=false` is added to CI. `DynamicCodeSupport` is a standard .NET SDK property, and
it is exactly what the iOS SDK uses: this gate does not run a simulated scenario, it runs the actual setting of a
mobile build.

## Consequences

### Positive

- The wire on iOS goes from "almost entirely unusable" to usable. Verified in five environments: the
  `DynamicCodeSupport=false` gate (0 failures / 718), NativeAOT, **Mac Catalyst Release**, **iOS simulator Release**
  (the last two are real Mono and both report `IsDynamicCodeSupported = False`), and the full-AOT compilation of the
  iOS device target. In addition, the four heads of `apps/Polhem.Northwind` (Desktop / Browser / iOS / Android) have
  been tested end to end against the same server.
- The `object` channel no longer describes each value with a full assembly-qualified name, so payloads get smaller
  and the wire no longer names CLR assemblies.
- The deserialization attack surface shrinks: the framework's own values go through a closed discriminated set, with
  no type name resolution.
- The drift guard changes from a manually maintained constant to an automatic comparison; forgetting to register a
  new property is caught by the tests.

### Costs

- **Breaking wire change**: the envelope format of `object` values changes, so client and server must be upgraded to
  the same version.
- Adding a wire type requires adding its registration. This is not an extra burden but an existing implicit
  requirement made explicit: a missed registration is caught on the spot by `WireContractDriftTests`, instead of
  blowing up later on mobile.
- The registration list is not small. It is generated mechanically from the type closure, and it is maintained by
  re-running the closure, not by adding and removing entries by hand.

### Correction to ADR-036

The core decision of ADR-036 (the definition layer must not depend on a transport format package) **stays
unchanged**: this ADR does not put MessagePack attributes back into `Polhem.Definition`. What changes is **the
implementation cost** of that decision: the coverage of hand-written formatters widens from "types with members to
exclude" to "every wire type".

The basis for ADR-036's cost "the source generator fallback is given up" (that the reflection fallback works) has been
overturned, but the conclusion still holds: explicit registration does not need attributes either, and is more
controllable than a source generator.

## Not covered

- **Runtime behavior on a physical iOS device has not been tested yet** (it needs Apple Developer signing and a
  device). Five environments have been verified: CoreCLR with the switch turned off, NativeAOT, Mac Catalyst Release,
  iOS simulator Release, and the full-AOT compilation of the iOS device target. The last two are real Mono and both
  report `IsDynamicCodeSupported = False`. The only difference between a device and the simulator is that "Mono has no
  JIT at all", and that aspect is already covered by NativeAOT, so this is listed as a low-risk formal gap.
- **The named-type escape hatch is still unusable on mobile**. Letting host-defined types onto the mobile wire as well
  needs a separate mechanism of "the host registers its own formatters", which this ADR does not address.

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **The registration list is maintained by hand.** The Costs say it is generated mechanically from the type closure
  and maintained by re-running the closure. It was bootstrapped that way, but there is no generator to re-run: the
  `WireContracts.*.cs` files in `src/Polhem.Api.Core/MessagePack/` are edited by hand, and `WireContractDriftTests`
  (`tests/Polhem.Api.Core.UnitTests/`) fails when a type or a member of the closure is missing from them.
- **2026-09-27: the named-type branch no longer needs dynamic code for enums and `ParameterCollection`.** Behind the
  string discriminator, `WireValueFormatter` writes an allowed enum as its underlying integer and reads it back with
  `Enum.ToObject`, and the types in its named table (`ParameterCollection`) have closed generic delegates like the
  known set, so both work on iOS and Mac Catalyst. The bytes are the ones the non-generic overload writes, so the wire
  is unchanged (`WireValueFormatterNamedTypeTests` pins them). Any other named type still takes the non-generic
  overload; without dynamic code it fails with a `NotSupportedException` that names the type and suggests the JSON
  body codec. The "Not covered" item about the escape hatch on mobile therefore still holds for host-defined types.
- **2026-09-27: arrays of allowed element types pass the escape hatch** on both ends and on both body codecs, and the
  writer runs the same screen as the reader in advance (`WireTypeWhitelist.IsNamedValueTypeAllowed`).
  `WireTypeWhitelist` also screens the assembly-name part of an assembly-qualified type name.
