# ADR-036: Wire serialization moves out to the API layer; the definition layer no longer carries MessagePack

[繁體中文](adr-036-wire-serialization-externalized.zh-TW.md)

## Status

**Accepted (2026-08-09), partially superseded**: the decision has been carried out. Its contractless fallback was
replaced by [ADR-037](adr-037-wire-explicit-registration.md) (see "Implementation evolution"), and its single-format
premise was lifted by [ADR-044](adr-044-payload-codec-negotiation.md) (see the note below).

This ADR revises two conclusions of [ADR-030](adr-030-messagepack-name-based-keys.md) (see "Revisions to ADR-030"
below), but does not change the decision of [ADR-004](adr-004-messagepack-payload.md) itself, "MessagePack as the API
payload format".

> **Note (2026-09-04, superseded in part)**: the premise of this ADR, "the wire has only one format, MessagePack",
> was lifted by [ADR-044](adr-044-payload-codec-negotiation.md): the body codec is now negotiated per request, and a
> JSON codec sits alongside MessagePack. **The core decision of this ADR is unaffected**: the definition layer still
> must not carry any transport format package, and wire binding still stays in `Polhem.Api.Core`. The only thing that
> changed is "how many codecs that layer has".

## Context

`Polhem.Definition` is the definition layer: the host of structures such as `FormSchema`, `TableSchema` and
`FormLayout`. Its consumers include `Polhem.Db` (including the `FormCommandBuilder` of 5 providers),
`Polhem.Repository`, `Polhem.Business`, `Polhem.UI.Avalonia`, `tools/Polhem.Cli` and `tools/DefineEditor`.

Before this decision, the package had a `PackageReference` to `MessagePack`, and 37 source files carried
`[MessagePackObject]` / `[Key]` / `[Union]` / `[IgnoreMember]` attributes. Not one of the consumers above needs
MessagePack, yet all of them were forced to pull it in through the dependency chain.

The more fundamental problem is that **the definition layer was tied to a technology choice of transport format**:
switching to another transport format later would mean going back and changing the definition types themselves.

## Decision

**All knowledge of wire serialization moves out to `Polhem.Api.Core`; the definition layer knows nothing about the
transport format.**

### The dividing line: what the BCL provides stays, what needs an external package moves out

The criterion is not "is it a transport format" but **"would it give the definition layer an external package
dependency"**:

| Format | Role | Needs an external package | Handling |
|------|------|-----------|------|
| **XML** | The definition layer's own persistence: definition files, saved files, snapshots | ❌ Built into the BCL | ✅ Stays in the definition layer |
| **JSON** | General web API transport, supported by .NET out of the box | ❌ Built into the BCL | ✅ Stays in the definition layer |
| **MessagePack** | Efficient transport | ✅ `PackageReference` | ❌ Moves out to `Polhem.Api.Core` |

`[XmlIgnore]` / `[JsonIgnore]` are **platform vocabulary**: using them brings in no dependency and does not bind to
any particular third-party format. MessagePack is different: it is an explicit technology choice, and it spreads along
the dependency chain.

> **The boundary of this criterion was made explicit by [ADR-038](adr-038-definition-dependency-boundary.md)
> (2026-08-11)**: "external package" means third-party packages and first-party **implementation** packages;
> Microsoft first-party **pure abstraction** packages do not count. That ADR also dealt with a dependency chain this
> criterion originally missed (`Polhem.Definition → Polhem.Expressions → DynamicExpresso.Core`). At the time, the
> criterion was applied by a human grepping for "transport format" keywords, which could not find it, so the
> criterion is now enforced by two gates: a build-time lock and a transitive closure test.

### Mechanism: a hand-written formatter per type

Wire binding is now handled by hand-written formatters in `src/Polhem.Api.Core/MessagePack/`; the definition types
themselves carry no transport attributes:

| Formatter | Target |
|-----------|------|
| `SortFieldFormatter`, `DepartmentNodeFormatter`, `NumberFormatItemFormatter`, `CashRoundingItemFormatter`, `AllowedCurrencyItemFormatter`, `ParameterFormatter` | Contract types that need to exclude framework-managed members |
| `FilterNodeFormatter` | The `FilterNode` polymorphic hierarchy (with `Kind` as the discriminator) |
| `KeyCollectionBaseFormatter` | `KeyedCollection` subtypes |
| `CollectionBaseFormatter`, `DataSetFormatter`, `DataTableFormatter` | Existing |

Types not listed are handled by `ContractlessStandardResolver`, keyed by property name: the same wire format as the
earlier `keyAsPropertyName`.

## Rationale

### Why hand-written rather than reflection-driven

The original design was one generic reflection formatter that read custom attributes to decide which members to
include. **That design is not workable under mobile AOT**: recursing into arbitrary property types can only use the
non-generic overload `MessagePackSerializer.Serialize(Type, ref MessagePackWriter, object, options)`, and
`MessagePackWriter` is a `ref struct`. That path needs `Reflection.Emit` to generate a delegate that can pass a ref
struct, and throws `NotSupportedException` when `IsDynamicCodeSupported=false`.

> This does not contradict the existing conclusion that "MessagePack 3.x has a reflection-based fallback": that
> conclusion is about the formatters MessagePack **generates itself**, and does not cover the path "a custom formatter
> calls the non-generic API".
>
> **Correction (2026-08-10)**: the existing conclusion itself also needs narrowing. MessagePack's fallback only covers
> contract types **carrying `[MessagePackObject]`**; `ContractlessStandardResolver` has no fallback. See the
> correction under "Open issues" below.

In a hand-written formatter the property types are known at compile time, so the generic overloads can be used
throughout with zero reflection, and desktop and device take the same path.

### The cost of hand-writing and the safeguard

Adding a property without updating the formatter **silently drops the field**. Each formatter therefore exposes a
`WireMemberCount` constant, and the wire tests assert that the map header matches it: as soon as a type and its
formatter drift apart, the test goes red.

### A side benefit: four pairs of twin types disappear

`MessagePackCollectionBase` / `MessagePackCollectionItem` / `MessagePackKeyCollectionBase` /
`MessagePackKeyCollectionItem` in `Polhem.Definition.Collections` were **deliberate copies** of the corresponding
types in `Polhem.Base.Collections`. Their only reason to exist was that "`Polhem.Base` takes no external packages and
cannot carry MessagePack attributes".

Once the attributes are removed, that reason is gone, and the four pairs merge back into single implementations. The
maintenance tax of a comment demanding "Keep the two in step" goes away with them, and that demand **had already been
violated**: `Polhem.Base.KeyCollectionBase.GetOrDefault` did not exist in the MessagePack version.

## Revisions to ADR-030

Two conclusions of ADR-030 no longer hold:

| ADR-030's conclusion | Current state |
|---------------|------|
| "`[Union]` types **must not** switch to `keyAsPropertyName`; new polymorphic hierarchies keep using integer `[Key]` + `[Union]`" | **No longer applies**. Polymorphism is now handled by `FilterNodeFormatter` with the `Kind` discriminator; `[Union]` has been removed, and `POLHEM4003`, which guarded it, is retired |
| "The bare `[MessagePackObject]` on collection types is what `ApiContractRegistry.ConvertForSerialization` decides on, and **must not be removed**" | **The judgement was wrong**. That class has no production callers, its mapping table is always empty and the conversion path is inert; the attribute check is only a short circuit, and the behavior is exactly the same after removing it |

The core decision of ADR-030 (wire keys follow property names) **stays unchanged**; only the implementation changes,
from `[MessagePackObject(keyAsPropertyName: true)]` to contractless plus explicit formatters, and the two produce the
same wire format.

## Consequences

### Positive

- The definition layer is decoupled from the technology choice of transport format; switching formats needs zero
  changes in `src/Polhem.Definition/`
- Six downstream packages that do not need MessagePack are no longer forced to depend on it
- The four pairs of twin types are merged and the maintenance tax disappears
- The wire contract becomes something **visible and reviewable** in the code, instead of "whatever contractless
  decides"

### Costs

- **The MessagePack source generator fallback is given up**: source generation requires the `[MessagePackObject]`
  marker. ADR-030's reason for keeping the marker was exactly this "free insurance", and this ADR gives it up
  deliberately. The basis is that MessagePack 3.x's reflection fallback was measured to work on mobile.
  > **Correction (2026-08-10): this basis does not hold.** That fallback only covers attributed types, so removing the
  > attributes loses both the fallback and source generation at once. The actual consequences are under "Open issues"
  > below.
- **Adding a wire type requires a hand-written formatter** (if the type has framework-managed members to exclude).
  Types without that need are handled automatically by contractless, with no action required.
- **Breaking change**: `Polhem.Definition` removes `SafeTypelessFormatter`, the four `Collections.MessagePack*` types
  and their public API entries. Downstream code switches to the corresponding types in `Polhem.Base.Collections`.

### Retired analyzer rules

`POLHEM4001` (collections must register a formatter), `POLHEM4002` (JSON rename inconsistent with the MessagePack
key), `POLHEM4003` (union hierarchies must use integer `[Key]`), `POLHEM4004` (ctor parameter order vs `[Key]` order):
all four guarded the attribute mechanism that has been removed.

`POLHEM4005` / `POLHEM4006` (a single public `Add`, a parameterless constructor) **are kept**: they guard the type
shape required by the mobile AOT `XmlSerializer`, which has nothing to do with the transport format. `POLHEM4006` now
decides based on the base types of framework collections and collection items.

## Open issues

> This section was originally recorded as "worth investigating separately". The investigation was completed on
> **2026-08-10** with the conclusions below: of the two reservations in the original text, one holds and one does not.

### Conclusion: this decision made the wire unusable on iOS

After all `[MessagePackObject]` attributes were removed, wire types are carried by `ContractlessStandardResolver`.
And **contractless has no reflection fallback**: on a runtime with `IsDynamicCodeSupported=false` it cannot generate
formatters, and almost every payload type throws `FormatterNotRegisteredException`.

A controlled experiment under NativeAOT (truly no dynamic code), using only MessagePack's own resolvers:

| Case | Result |
|------|------|
| `[MessagePackObject(keyAsPropertyName: true)]` type + `StandardResolver` | ✅ Round-trips correctly |
| Unattributed POCO + `ContractlessStandardResolver` | ❌ `FormatterNotRegisteredException` |

The original measurement in ADR-030 phase 0 (both integer keys and `keyAsPropertyName` round-trip) **was not wrong**:
both of those are attributed types. The mistake was generalizing it into "MessagePack works under AOT", and this ADR
was built on exactly that generalization.

### Settling the two reservations

1. **"A simulation on a JIT runtime; a real device may differ": does not hold.**
   That switch (`RuntimeFeature.IsDynamicCodeSupported`) is exactly the value the .NET for iOS SDK sets by default for
   **every configuration** of iOS / tvOS / MacCatalyst (Debug and Release, device and simulator), unless the
   interpreter is explicitly enabled. The simulation used the default value of an iOS build. Android has no such
   setting and keeps the JIT, so it is **not affected**.
2. **"`InvalidProgramException` is an artifact of the simulation": the symptom is right, the conclusion is wrong.**
   That exception does appear only in the desktop reproduction, where a JIT exists but is reported as unavailable; a
   runtime with truly no dynamic code throws `InvalidOperationException` / `NotSupportedException` /
   `MissingMethodException` instead. But **the same cases still fail on NativeAOT**: what is distorted is the
   exception type, not the failure itself.

### Quantification

Same test project, same switch, counting "failure message contains `MessagePack`":

| Version | MessagePack-related failures |
|------|--------------------|
| Before this decision (v4.18.0) | 37 |
| After this decision (v4.19.0) | 185 |

The original text's "the hand-written formatters of this decision bring it down further" is the opposite of what was
measured: **it grew about fivefold**. The previously recorded 51 / 694 could not be reproduced, and how they were
counted is unknown.

The remaining 37 are pre-existing defects older than this decision, concentrated in the typeless channel (`object`
members such as `Parameter.Value` / `FilterCondition.Value`) not working for `Decimal` / `Guid` / `DateTime` /
`DateOnly` / `Byte[]`, and in `DataTable` / `DataSet`.

### The fix

Handled in [ADR-037](adr-037-wire-explicit-registration.md): every wire type registers a formatter explicitly, and
`object` members use a discriminated envelope.

### The decision of this ADR is not withdrawn because of this

The judgement to decouple the definition layer from the transport format ("do not let the definition layer grow
external package dependencies") is unaffected. That contractless has no fallback changes **the implementation cost of
the decision**, not the decision itself: the coverage of hand-written formatters must widen from "types with members
to exclude" to "every wire type". The fix is handled separately.

How to reproduce (no csproj changes needed):

```bash
dotnet test tests/Polhem.Api.Core.UnitTests/Polhem.Api.Core.UnitTests.csproj -c Release --settings .runsettings -p:DynamicCodeSupport=false
```

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

**The analyzer rule numbers above are Bee.NET's.** This decision was taken while the framework was Bee.NET, and the
rules it names shipped as `BEE4001`–`BEE4004`; the `POLHEM` spelling here comes from the rename. Polhem never shipped
rules under those numbers: its analyzer release history starts at 1.0.0, and `POLHEM4001`–`POLHEM4004` are reserved and
never reused, so a suppression carried over from Bee.NET cannot silence a new rule. The reserved numbers are listed in
the [analyzer rule reference](../en/analyzer-rules.md).

- **2026-08-10: contractless is no longer part of the mechanism.** The Decision's "types not listed are handled by
  `ContractlessStandardResolver`" and the Costs' "handled automatically by contractless, with no action required" were
  replaced by [ADR-037](adr-037-wire-explicit-registration.md): every wire type registers a formatter explicitly (see
  "Open issues" above for why). The formatter table in the Decision is therefore not the full list; the registered
  formatters are the files in `src/Polhem.Api.Core/MessagePack/` and the registrations in its `WireContracts.*.cs`.
- **`WireMemberCount` no longer exists, and it never guarded anything.** The safeguard described under "The cost of
  hand-writing and the safeguard" compared the map header a formatter wrote with a constant the same formatter
  declared, so it could not fail. The drift check today is `WireContractDriftTests`
  (`tests/Polhem.Api.Core.UnitTests/`): each hand-written formatter implements `IWireContract` and exposes
  `WireMemberNames`, and the test compares that list with the type's current shape and the registrations with the wire
  type closure. Only `[JsonIgnore]` with its default `Always` condition takes a member off the wire, and the test fails
  on member shapes it does not recognize. `WireCodecParityTests` round-trips every registered contract through both body
  codecs and compares it member by member, which catches a member the formatter writes but does not read back.
