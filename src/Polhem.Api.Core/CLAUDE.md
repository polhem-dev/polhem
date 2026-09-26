# Polhem.Api.Core: wire serialization details

This file loads automatically when an agent touches any file under `src/Polhem.Api.Core/` (nested `CLAUDE.md`
files are lazily loaded). The cross-layer serialization conclusions (per-request negotiation of the body codec, no
wire format packages in the definition layer) are in `.claude/rules/serialization.md` (always loaded). For the
pitfall background see `../../docs/repo-ops/gotchas/serialization-and-expressions.md`.

## Every wire type is registered explicitly (adr-037)

**`ContractlessStandardResolver` is not the carrying mechanism; it is only a convenience fallback on the desktop.**
It generates formatters with `Reflection.Emit`, and .NET for iOS sets `DynamicCodeSupport=false` for every build.
There, an unregistered type is not slower; it is a `FormatterNotRegisteredException`.

**Required when adding a wire type** (`Polhem.Api.Core.Messages.*`, the definition-layer types reachable from them,
collections):

1. Add a `WireContract.For<T>().Member(...)...Build()` block in `MessagePack/WireContracts.*.cs`, listing every
   member.
2. If the type is a framework collection, register `CollectionBaseFormatter<,>` / `KeyCollectionBaseFormatter<,>`
   instead.
3. If a member introduces a new **closed generic instantiation** (`List<T>` / `Dictionary<K,V>` / `T?` / arrays /
   **enums**), add an entry in `WireContracts.Generics.cs`. These are also built through `MakeGenericType` and have
   no native code on AOT. Enums use `WireEnumFormatter<T>`.

A missing registration is caught by `WireContractDriftTests` (it walks the same type closure and compares it with
the registration list), so there is no `WireMemberCount` constant to maintain by hand.

**The JSON codec (adr-044) does not need any of this.** `JsonPayloadSerializer` goes through System.Text.Json, with
the same shape as a `Plain` body. But the two wires **share the same set of `WireValueCode` discriminators**
(`Wire/WireValueCode.cs`; `WireValueCodePinTests` pins both), and the raw JSON body is pinned by the golden samples
in `wire-fixtures/`. When you change the envelope of an `object` member or the shape of a `DataTable` / enum,
`WireFixtureTests` turns red, and **that diff is the description of the wire change**.

**The definition of a wire member is the definition of JSON**: a public read-write property not marked
`[JsonIgnore]`. Framework-managed members (`Tag` / `Key` / `SerializeState` / `Collection`) already carry that
attribute and are excluded automatically.

### Three easy misjudgments

1. **Both `Collection<T>` and `KeyedCollection<TKey,TItem>` must be registered.**
   On the desktop, contractless recognizes the first (serialized as an array) and wrongly binds the second as a
   dictionary, but on iOS neither works. Do not skip them because "it seems fine when tested on the desktop".
2. **A custom formatter must not use the non-generic `MessagePackSerializer.Serialize(Type, ref writer, ...)`.**
   `MessagePackWriter` is a `ref struct`, and that overload needs `Reflection.Emit`; mobile AOT throws right away.
   Name every member and use the generic overloads throughout. `WireContract.Member<TValue>` exists precisely to keep
   `TValue` at compile time.
3. **Registering a base type does not cover its subtypes.** `FilterNodeFormatter` is registered on `FilterNode`.
   If a caller holds the static type `FilterCondition`, what gets resolved is
   `IMessagePackFormatter<FilterCondition>`, which is why there are the two adapters `FilterConditionFormatter` /
   `FilterGroupFormatter`.

### `object` members use a discriminated envelope, not `TypelessFormatter`

`object` members such as `Parameter.Value` / `FilterCondition.Value` are handled by `WireValueFormatter`: the
framework's own closed set of types is read and written with an int discriminator + closed generic delegates; other
allow-listed types go through an escape hatch of "type name + non-generic overload", and **that branch only works on
runtimes that have dynamic code**. To send a new value type on mobile, add it to the closed set (`WireValueCode` +
a `WireValueFormatter` registration); do not count on the escape hatch.

## AOT: MessagePack's reflection fallback only covers attributed types (corrected 2026-08-10)

> This section used to say "MessagePack 3.x has a reflection-based fallback; the source generator is not a hard
> prerequisite". **That conclusion was over-generalized and was disproved by measurement on 2026-08-10**; the
> correct version follows.

A controlled experiment under NativeAOT (truly no dynamic code), using only MessagePack's own resolvers:

| Case | Result |
|------|--------|
| `[MessagePackObject(keyAsPropertyName: true)]` type + `StandardResolver` | ✅ round-trip works |
| Unattributed POCO + `ContractlessStandardResolver` | ❌ `FormatterNotRegisteredException` |

**Contractless has no reflection fallback.** The original measurement in adr-030 phase 0 (both integer keys and
`keyAsPropertyName` round-trip) was not wrong in itself: both of those are **attributed** types. The mistake was
generalizing it to "MessagePack works on AOT".

### History: this conclusion once broke the whole wire on iOS

After adr-036 removed all the attributes, wire types were carried by contractless, so on runtimes with
`IsDynamicCodeSupported=false` almost every payload type threw `FormatterNotRegisteredException` (measured the same
way: 37 → 185 failures). **It was not a simulation artifact** (reproduced on NativeAOT), **and that switch is exactly
the default the .NET for iOS SDK sets for every iOS / tvOS / MacCatalyst build** (see
`.claude/rules/apple-mobile-trim.md`). **Android is not affected** (it keeps the JIT).

Fixed by adr-037: every wire type is now registered explicitly, and `object` members use the discriminated envelope.
**This section stays because it shows a recurring mistake**: when a measurement disproves a guess, first ask "does my
sample cover the path the guess is about?" The guess named contractless, but the samples were all attributed types.

### Regression gate (one line, no csproj change)

```bash
dotnet test tests/Polhem.Api.Core.UnitTests/Polhem.Api.Core.UnitTests.csproj -c Release --settings .runsettings -p:DynamicCodeSupport=false
```

`DynamicCodeSupport` is a standard .NET SDK property and is mapped to the `RuntimeHostConfigurationOption` for
`RuntimeFeature.IsDynamicCodeSupported`, exactly as the iOS SDK does. **Zero failures are expected**; CI runs the
same gate.

A test that cannot pass here and genuinely depends on a "desktop-only capability" is marked `[DynamicCodeFact]` so
it is skipped automatically. **Do not use it to silence failures**: if a framework-owned wire type needs dynamic
code, that is a defect, not a test problem.

## Constructor parameter order of collection items (no longer a pitfall, 2026-08-09)

Historically, integer `[Key(n)]` keys matched by **position**. If the parameter order of a collection item's
parameterized constructor differed from the `[Key]` declaration order, a wire round-trip would **silently swap
fields of the same type**, and XML / JSON would not catch it.

**Since adr-036 there are no integer `[Key]`s anywhere in the repository.** Wire binding always goes by property name
or by a formatter that names each member, so **this pitfall no longer exists**, and `POLHEM4004`, which guarded it,
has been retired. A constructor parameter order that differs from the property declaration order (such as
`CurrencyItem`) is normal.
