# ADR-030: MessagePack contracts switch to property-name keys (keyAsPropertyName)

[繁體中文](adr-030-messagepack-name-based-keys.zh-TW.md)

## Status

> **Partly amended by [ADR-036](adr-036-wire-serialization-externalized.md) (2026-08-09).**
> The core decision (wire keys are property names) stands, but the implementation has changed to contractless plus
> explicit formatters; the wire format of the two is the same. The following two conclusions **no longer hold**:
> 1. "`[Union]` types must not switch to `keyAsPropertyName`; new polymorphic hierarchies keep integer `[Key]` +
>    `[Union]`": polymorphism is now handled by `FilterNodeFormatter` with a `Kind` discriminator, and `POLHEM4003`
>    has been retired.
> 2. "The bare `[MessagePackObject]` on collection types is what `ApiContractRegistry` decides by and must not be
>    removed": that judgement was wrong. The mapping table is always empty and the conversion path is inert, so
>    behavior is exactly the same after removal.
>
> See "Revisions to ADR-030" in ADR-036 for details.


**Accepted (2026-07-22; scope widened on 2026-07-27)**: the decision has been carried out. Contracts and most DTO /
collection item types have switched to name-based keys (`keyAsPropertyName`), and `SerializableData*` was brought in
line on 2026-07-27; `[Union]` polymorphic hierarchies and the like are recorded exceptions (see "Outcome and final
scope").

> **Go/no-go resolution (2026-07-22, final)**: **carry it out now**. The key fact: **there are currently no real
> external consumers**, so a breaking wire change has no compatibility cost; the earlier reason for deferring it
> ("tie it to the next major", because of the compatibility impact) no longer applies. For a very low price this
> gains "eliminate the ctor-order footgun + eliminate key-number coordination across inheritance + unify the JSON and
> MessagePack mental models".

This ADR re-evaluates the **integer key** strategy implied by the "Schema Evolution: `[Key]` supports adding/removing
fields" section of [ADR-004](adr-004-messagepack-payload.md). It does not change the decision "MessagePack as the API
Payload format" itself.

## Outcome and final scope (2026-07-22)

`[Union]` polymorphic hierarchies **keep integer keys by decision** (a Union is serialized as an integer-keyed array
plus a type discriminator, so the whole hierarchy shares a single keying strategy). The "convert all 90 types" plan
was therefore not carried out; the final scope is **category-aware**:

| Category | Treatment | Types |
|----------|-----------|-------|
| Contract Request/Response | ✅ Switched to keyAsPropertyName | 57 `Polhem.Api.Core.Messages.*` (+ `[Key(0)]` removed from `ApiMessageBase.Parameters`) |
| Plain DTOs | ✅ Switched | PackageUpdateInfo/Query, RecordFieldChange, CompanyInfo, DepartmentTree, Paging* |
| Non-Union collection items | ✅ Switched (where the footgun is eliminated) | *Item, SortField, DepartmentNode, Parameter |
| **`[Union]` polymorphic hierarchies** | ❌ **Exception** (integer keys, by decision) | FilterNode / FilterCondition / FilterGroup |
| Collection containers | ➖ Not affected (go through a custom formatter / proxy) | MessagePackCollectionBase/KeyCollectionBase subtypes |
| DataSet/DataTable wire plumbing | ✅ Switched (done later on 2026-07-27, see below) | SerializableData* |

**Recorded constraint**: `[Union]` types **must not** switch to `keyAsPropertyName`. New polymorphic MessagePack
hierarchies keep integer `[Key]` + `[Union]`. This constraint is enforced at compile time by **POLHEM4003** (covering
a base with `[Union]` and all of its subclasses, including multi-level inheritance). Relaxing it requires changing
this ADR's decision, not new compatibility evidence: `keyAsPropertyName` has been measured to round-trip correctly on
a union hierarchy, and integer keys are kept so that the hierarchy shares a single keying strategy.

### Follow-up: bringing SerializableData\* in line (2026-07-27)

The original table listed `SerializableData*` as "keeps integer keys". That was an **unexamined leftover**, not an
exception with a technical reason. These five types (`SerializableDataSet` / `DataTable` / `DataColumn` / `DataRow` /
`DataRelation`) are plain DTOs, with no `[Union]`, no read-only members and no ctor positional mapping; none of them
has any property that would block `keyAsPropertyName`. Keeping integer keys only added one more exception to remember
to the rule "MessagePack is always name-based".

→ All five types switch to `keyAsPropertyName: true`, and 22 `[Key(n)]`s are removed.

**The only real cost**: `SerializableDataRow` is serialized **row by row**, and its three member keys change from
integers to `CurrentValues` / `OriginalValues` / `RowState`, adding about 35 bytes per row on the wire. But these keys
repeat exactly from row to row, and the GZip in the payload pipeline compresses that kind of repetition extremely
well, so the actual net cost is negligible.

**Confirmed as true exceptions that keep integer keys** (only these two places after a scan of the whole repository):

| Location | Reason |
|----------|--------|
| `FilterNode` / `FilterCondition` / `FilterGroup` | `[Union]` polymorphism; integer keys are kept by decision so the hierarchy shares a single keying strategy (enforced by POLHEM4003) |
| `MessagePackKeyCollectionBase<T>.ItemsForSerialization` (a `[Key(0)]` proxy; the only subtype is `ParameterCollection`) | Opt-out membership would pull `KeyedCollection`'s `Count` / `Comparer` / indexer onto the wire as well. The integer key on the proxy property is a deliberately minimal serialization surface |

The eight subtypes of `MessagePackCollectionBase<T>` (`CurrencySettings` / `UnitSettings` /
`FilterNodeCollection` / `SortFieldCollection` / `DepartmentNodeCollection` /
`CompanyNumberFormats` / `CompanyCashRounding` / `CompanyAllowedCurrencies`) are serialized as arrays through
`CollectionBaseFormatter`, so key style does not apply; their bare `[MessagePackObject]` marker is still what
`ApiContractRegistry.ConvertForSerialization` decides by, and **must not be removed**.

**Verification**: Phase 0 AOT smoke test (keyAsPropertyName OK under reflection-only); Definition serialization 201 +
Api.Core serialization/contract 237 all pass; full-solution Release build 0 errors / 0 warnings. (The DB-dependent
end-to-end tests were not run because Docker was not running locally; they are unrelated to the serialization
change.)

## Context

Current state (scanned on 2026-07-22):

- The resolver chain of `MessagePackCodec` has `ContractlessStandardResolver.Instance` as primary, which makes it a
  **hybrid**: **90 `[MessagePackObject]` types** use integer `[Key(n)]`, and only unmarked types go through
  contractless (property names as keys).
- Integer keys carry a **coordination burden across inheritance**: `ApiMessageBase` uses `[Key(0)]` (`Parameters`),
  and derived types such as `LoginRequest` use `[Key(100+)]` to avoid colliding with the base.
- Collections match consistently by string keys (the scenarios of comparing identifier-like strings: key case, field
  names, ProgId and so on), which is a mental mismatch with the positional semantics of integer keys.

Three problems triggered the re-evaluation:

1. **The positional-mapping footgun of integer keys**: if the parameter order of the parameterized ctor of a
   `MessagePackCollectionItem` subtype ≠ the `[Key]` order, a wire round-trip **silently swaps fields**, and XML/JSON
   cannot catch it. This is a real, recorded pitfall.
2. **Two sets of compatibility rules for JSON and MessagePack**: the JSON wire uses property names as the contract,
   while MessagePack uses integer key positions. The same rename breaks the two in different ways, which is a heavy
   mental burden.
3. **Mobile AOT**: MessagePack is the mandatory path of the authenticated wire for mobile (native iOS/Android
   clients), yet `MessagePackCodec` uses an Emit-based resolver; a real-device AOT round-trip has not been verified.
   If we are forced onto the MessagePack source generator, source-gen **requires the `[MessagePackObject]` marker**.

## Decision

**Goal**: contract wire keys become **name-based (property names as keys)**, eliminating the positional-mapping
fragility of integer keys and the key-number coordination across inheritance.

**Implementation**: use **`[MessagePackObject(keyAsPropertyName: true)]`** (keeping the marker), **not** the approach
of "remove the markers entirely and rely on `ContractlessStandardResolver`".

**Condition for carrying it out (gated)**: this is a breaking wire change, so there is no standalone breaking release;
if it is done, it is tied to the next planned major version, and must first pass Phase 0 (AOT smoke test + scope
decision) and a go/no-go.

## Rationale

- **Eliminates the positional-mapping footgun**: name-based keys map by property name, so the ctor parameter order no
  longer affects the wire.
- **Eliminates key-number coordination across inheritance**: no more planning of base `[Key(0)]` / derived
  `[Key(100+)]` to avoid collisions.
- **Unified mental model**: both JSON and MessagePack use "the property name" as the wire contract; one set of rules.
- **Keeps the source generator as a fallback**: keyAsPropertyName still needs the `[MessagePackObject]` marker, so if
  mobile AOT ever forces us onto source-gen, the markers are already in place and do not have to be added back
  everywhere. Contractless without markers would **close this door** (source-gen needs the marker), so it is not
  adopted.
  - **Note (Phase 0, 2026-07-22)**: the AOT smoke test measured that MessagePack 3.x has a **reflection-based
    fallback** under `IsDynamicCodeSupported=false` (no Emit), and both integer keys and keyAsPropertyName
    **round-trip correctly**. So source-gen is **not currently required**, and the urgency of this "fallback" point
    drops. Keeping the markers is still **cheap insurance** (it keeps the source-gen option for free), and B's other
    three reasons (eliminating the footgun, eliminating number coordination, unifying the mental model) are
    unaffected, so the decision stays B.
  - **Correction (2026-08-10)**: the measurement in the note above is correct, but **its scope must be narrowed**:
    integer keys and keyAsPropertyName are **both annotated types**, and that fallback only covers contract types
    annotated with `[MessagePackObject]`; `ContractlessStandardResolver` has **no** fallback (confirmed by a NativeAOT
    control experiment). So this "fallback" point is more than insurance: **the marker holds up both the fallback and
    the source-gen path**, and removing the marker loses both. The full accounting of this point is in "Open issues"
    of [ADR-036](adr-036-wire-serialization-externalized.md).

## Trade-offs

- **Breaking wire change** (the biggest cost): integer-keyed **array** format → string-keyed **map** format; the wire
  is incompatible. The framework is published on NuGet, so external consumers break if client and server are not
  upgraded to the same version: this requires a version bump, a changelog entry clearly marked breaking, and a
  coordinated upgrade.
- **Opt-in → opt-out membership** (a permanent cost): integer `[Key]` serializes only members that have a key
  (opt-in); keyAsPropertyName serializes every public member (opt-out). From now on every new public property must
  remember `[IgnoreMember]`, or it leaks onto the wire.
- **Larger wire**: string keys are larger than integer keys, but the payload pipeline includes GZip, so the net cost
  after compression is not high.
- **Cross-type byte reinterpretation is potential, not current, for polhem**: polhem currently goes wire ↔ BO args
  through **explicit property copy** (`ApiInputConverter`/`ApiOutputConverter`) and does not use byte
  reinterpretation, so this benefit is not a current need for polhem.

## Alternatives not adopted

- **Keep integer `[Key]` (the current state)**: the positional-mapping footgun and the key-number coordination across
  inheritance remain, and it diverges from JSON's name-as-contract rule.
- **Remove the markers entirely and rely on `ContractlessStandardResolver`**: the least boilerplate, but it closes the
  MessagePack source generator fallback (source-gen needs the marker); an unacceptable risk for mobile AOT.

## Consequences

**This ADR (at the proposal stage) only adds this document and one cross-reference line in
[ADR-004](adr-004-messagepack-payload.md).**

When adopted and carried out:

- The "Schema Evolution: `[Key]` supports adding/removing fields" section of `[ADR-004]` changes to point to this
  ADR's name-based strategy.
- 90 `[MessagePackObject]` types switch to `keyAsPropertyName: true` and have their integer `[Key(n)]`s removed; an
  opt-out membership audit adds `[IgnoreMember]` where needed.
- Collection container types (the `ItemsForSerialization` proxy of `MessagePackKeyCollectionBase<T>`, and the types
  registered as arrays with `CollectionBaseFormatter<T>`) are handled individually; only their item types switch.
- The public document `docs/en/api-bo-contract-design.md` (bilingual) updates its description of wire keys.
- (Conditional) introduce the MessagePack source generator and `[GeneratedMessagePackResolver]`.

**Regression guard**: `tests/Polhem.Api.Core.UnitTests/Contracts/ApiContractSerializationTests.cs` (scans every
contract by reflection and checks round-trip fidelity in both MessagePack and JSON) is the main regression guard.
Note that it only verifies "round-trip fidelity within the same format" and **does not verify wire compatibility
across versions** (the old and new wires are incompatible by design, an expected breaking change).

## Related

- [ADR-004: Use MessagePack as the API Payload serialization format](adr-004-messagepack-payload.md): this ADR
  revisits its schema-evolution reasoning for integer keys.
- [ADR-025: AOT XmlSerializer compatibility for definition types](adr-025-define-types-aot-xmlserializer-compat.md):
  the neighboring context of mobile AOT serialization.

## Implementation evolution

An ADR records the design at the time of the decision. The following is a later change, for readers comparing with
the current code:

**The analyzer rule numbers above are Bee.NET's.** This decision was taken while the framework was Bee.NET, and the
rules it names shipped as `BEE4001`–`BEE4004`; the `POLHEM` spelling here comes from the rename. Polhem never shipped
rules under those numbers: its analyzer release history starts at 1.0.0, and `POLHEM4001`–`POLHEM4004` are reserved and
never reused, so a suppression carried over from Bee.NET cannot silence a new rule. The reserved numbers are listed in
the [analyzer rule reference](../en/analyzer-rules.md).
