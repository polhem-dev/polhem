---
name: polhem-serialization
description: "Design guidance for \"triple serialization\" (XML / JSON / MessagePack) of polhem objects. Two core axes of use: XML for persistence (saved files / definition files / snapshots / written to the DB); JSON + MessagePack for API transport (the JSON-RPC envelope is always JSON; the body of Encoded/Encrypted payloads uses the codec each request declares, and MessagePack when none is declared). Covers the object recipe (parameterless ctor + XML/JSON attributes; the definition layer carries no serialization-package attributes), collections (inherit a Polhem.Base.Collections base + must register a formatter explicitly in WireContracts), wire transport patterns (the object itself vs an XML string), mobile AOT type-shape requirements, pitfalls, and a triple round-trip test template. Use when the user wants \"an object that supports XML/JSON/MessagePack\", \"serialization\", \"send to the front end and also save to disk\", \"send an object across the wire\", \"KeyCollectionBase collection serialization\", \"add a wire type\", \"serializable object design\", or similar."
---

# polhem triple serialization (XML / JSON / MessagePack)

> **Fully rewritten on 2026-08-11.** This file used to teach the `[MessagePackObject]` / `[Key(n)]` /
> `MessagePackCollectionBase<T>` approach. **Those types and attributes were all removed by ADR-036** (0 declarations in
> `src/`); code written the old way will not compile, or will throw on mobile.

## Core: two axes of use (remember this first; everything else follows from it)

| Serialization | Axis | Carrier | When |
|--------|--------|------|--------|
| **XML** | **Persistence** | `XmlCodec` | Saved files, definition files (FormSchema / TableSchema…), snapshots, any object written to disk / DB |
| **JSON** | **API transport** | `System.Text.Json` | The JSON-RPC envelope is always JSON; with `PayloadFormat.Plain` the payload value is also embedded as JSON |
| **MessagePack** | **API transport** | `MessagePackCodec` | The payload body of `PayloadFormat.Encoded` / `Encrypted`; the default when no codec is declared (base64 MessagePack bytes) |

- **XML = the persistence axis**; **JSON + MessagePack = the transport axis**.
- **`PayloadFormat` is the "encryption / compression" dimension, not a "JSON vs MessagePack" selector**:
  `Plain` embeds JSON; the body of `Encoded`/`Encrypted` **is declared per request in the envelope's `codec` field**
  (adr-044), and is MessagePack when none is declared. For the criteria and names see `rules/serialization.md` and
  `PayloadCodecNames`.
  ⚠️ "The framework has no JSON body serializer" is a conclusion from **before 4.26.0**; do not reason from it any more.
- An object that must "go to the front end and also be saved as a snapshot" → **needs all three** (e.g.
  `DepartmentTree`).
- An API DTO that only crosses the wire → needs JSON + MessagePack, not XML.

> **Do not use the persistence format as the wire format**: `GetDefine` returning the XML string from
> `XmlCodec.Serialize` is the **historical usage** for define objects. API transport of new objects sends the object
> itself (see "Wire transport patterns" below).
> Side effect: FormSchema travels on the wire as an XML string, so **it is not in the wire type closure**.

## Object recipe (plain object, no collections)

```csharp
public class Foo : IKeyObject          // IKeyObject only if it goes into a KeyObjectCache
{
    public Foo() { }                    // Parameterless ctor: required by all three serializers

    // XML needs attributes; JSON is automatic (included unless marked); MessagePack is not marked on the type, see below
    [XmlAttribute] public Guid RowId { get; set; }
    [XmlAttribute] public string Name { get; set; } = string.Empty;

    public string GetKey() => RowId.ToString();

    // Derived / index / owner: skip on both axes
    [XmlIgnore, JsonIgnore] private Dictionary<string, Foo>? _index;
    [XmlIgnore, JsonIgnore] public SomethingDerived Derived => ...;
}
```

- **The definition layer carries no serialization-package attributes** (ADR-036). `[XmlIgnore]` / `[JsonIgnore]` are
  BCL vocabulary and allowed; MessagePack's `[MessagePackObject]` / `[Key]` / `[IgnoreMember]` **are not allowed**, and
  `src/Polhem.Definition` must not have a `PackageReference` to `MessagePack`.
- **`[JsonIgnore]` is also the wire exclusion mechanism**: a wire member is defined as a **public readable and
  writable** property **not marked `[JsonIgnore]`**. To keep a member off MessagePack, mark it `[JsonIgnore]`.
- **A parameterless ctor is mandatory**: `XmlSerializer` / `System.Text.Json` both require it; `POLHEM4006` enforces it
  at build time.

## Wire types must be registered explicitly (ADR-037, the easiest to miss)

**New wire types** (`Polhem.Api.Core.Messages.*`, definition-layer types transitively reachable from them, collections)
**must be registered in `src/Polhem.Api.Core/MessagePack/WireContracts.*.cs`**, otherwise mobile throws
`FormatterNotRegisteredException` (it does not merely get slower). A missed registration is caught by
`WireContractDriftTests`.

> **The full registration procedure, the three easy misjudgments, and the `object` envelope mechanism →
> [`src/Polhem.Api.Core/CLAUDE.md`](../../../src/Polhem.Api.Core/CLAUDE.md)** (loaded automatically when that project
> is touched). That is the only authoritative source; this file does not copy it.

## Collections

Elements and collections each have a base, and MessagePack **requires an explicitly registered formatter**:

```csharp
// Element: KeyCollectionItem (with key) or CollectionItem (without key)
public sealed class FooNode : CollectionItem
{
    [XmlAttribute] public Guid RowId { get; set; }
}

// Collection: a base from Polhem.Base.Collections
public class FooNodeCollection : CollectionBase<FooNode> { }
```

```csharp
// MessagePackCodec.BuildFormatters(): without this line, mobile cannot read it back
new CollectionBaseFormatter<FooNodeCollection, FooNode>(),
```

### Choosing a collection base

| Base | Use |
|------|------|
| `Polhem.Base.Collections.CollectionBase<T>` | Collections whose items have no key |
| `Polhem.Base.Collections.KeyCollectionBase<T>` | Items have a key and need keyed indexing (e.g. `ParameterCollection`) |

- **Public collection properties in the definition layer must not use bare `List<T>` / `Collection<T>` / `IList<T>`**
  (`rules/definition.md`); `POLHEM3002` enforces this at build time.
- `KeyCollectionBase<T>` is constructed with `StringComparer.OrdinalIgnoreCase` and is **truly O(1)**
  (`dictionaryCreationThreshold = 0`: the dictionary is built on the first `Add`).

### Mobile AOT type-shape requirements (completely invisible on desktop)

Reflection-only `XmlSerializer` (the iOS path) is stricter about type shape than desktop: **only one public instance
`Add`**, **a parameterless constructor is required**, and **a collection property mapped to repeated `[XmlElement]`
must have a public setter**. The first two are enforced by `POLHEM4005` / `POLHEM4006`; the third is only caught by the
AOT gate in CI.

> **The full rules for all three, the exception-message mapping, and the correct way to write the setter (clear, then
> `Add` one by one; do not swap the field) →
> [`rules/apple-mobile-trim.md`](../../rules/apple-mobile-trim.md) § Mobile compatibility requirements for serialized
> types, and [`src/Polhem.Definition/CLAUDE.md`](../../../src/Polhem.Definition/CLAUDE.md).**

## `object` members use the discriminated envelope

`object` members such as `Parameter.Value` / `FilterCondition.Value` are handled by `WireValueFormatter`,
**not `TypelessFormatter`**. To send a new value type on mobile → add it to the closed `WireValueCode` set;
do not count on the whitelist escape hatch (that branch only works on runtimes with dynamic code).

**The numeric values of `WireValueCode` are part of the wire format and must not be renumbered**: that breaks
cross-version compatibility, and the drift test cannot catch it. Mechanism details are in
`src/Polhem.Api.Core/CLAUDE.md`.

## Wire transport patterns (API side)

| Pattern | How | When |
|------|------|------|
| **The object itself** (recommended) | `Response.Tree = DepartmentTree`; register in `WireContracts` | **New API transport**; sample `GetDepartmentTreeResponse` |
| **XML string** (historical) | `Response.Xml = XmlCodec.Serialize(obj)`; `XmlCodec.Deserialize<T>` on the client | **Define objects only** (`GetDefine` / `GetFormSchema`): the persistence format borrowed as wire format; new objects do not use it |

## Pitfalls

1. **The four registration pitfalls** (forgetting to register; `Collection<T>` and `KeyedCollection<,>` both need
   registering; registering a base type does not cover subtypes; custom formatters must not use the non-generic
   `Serialize(Type, ref writer, …)`)
   → all in `src/Polhem.Api.Core/CLAUDE.md` § Three easy misjudgments. **Common symptom: nothing at all on
   desktop, it only blows up on mobile**, so "it tested fine on desktop" is no evidence of anything.
2. **Derived / index / owner fields missing one axis → leakage or cycles**: `[XmlIgnore, JsonIgnore]` always go as a
   pair.
3. **The type whitelist is only for the `object` escape hatch and `ApiPayload.TypeName`**, and what it lists are
   **namespaces** (`SysInfo.AllowedTypeNamespaces`). **Always validate an assembly-qualified name with
   `WireTypeWhitelist.IsAssemblyQualifiedNameAllowed`**; do not split the string yourself. The commas of generic
   arguments come before the assembly separator, so splitting on the first comma leaves the arguments completely
   unchecked (an unauthenticated-reachable bypass was fixed on 2026-08-11).
4. **Serializing a process-wide cached instance pollutes the source**: `XmlCodec.Serialize(obj)` flips flags **on the
   source object** through `IObjectSerialize.SetSerializeState` and recurses into child collections (so that empty
   collection getters return `null` during serialization, and definition files on disk do not carry redundant elements
   such as `<Tables />`). Therefore **it cannot be used as a free deep clone**; to mutate an object taken from the
   cache, always `Clone()` first (see `rules/definition.md`).
5. **A lazy index must be rebuildable after deserialization**: serialization only carries the flat state; the lookup
   index is built lazily (thread-safe) on the first lookup after restore; the index itself is not serialized.
6. **An Oracle Guid reads back as `byte[]` (RAW 16)**: `ValueUtilities.CGuid` already supports coercing `byte[]`;
   when reading a Guid column from a hand-written raw DataTable, do not use a conversion that would miss it.
7. **A custom JSON converter is only needed for polymorphism**: `System.Text.Json` enumerates single-type collections
   directly; only polymorphic ones (e.g. `FilterNode`) need a `JsonConverter` (see
   `FilterNodeCollectionJsonConverter`).

## Triple round-trip test template

```csharp
// XML (persistence axis): put it in the *.UnitTests of the project that owns the definition object
var xml = XmlCodec.Serialize(obj);
var fromXml = XmlCodec.Deserialize<Foo>(xml)!;

// JSON (transport axis): same place
var json = JsonSerializer.Serialize(obj);
var fromJson = JsonSerializer.Deserialize<Foo>(json)!;

// MessagePack (transport axis): put it in Polhem.Api.Core.UnitTests (the codec lives there)
var bytes = MessagePackCodec.Serialize(obj);
var fromMp = MessagePackCodec.Deserialize<Foo>(bytes)!;
```

- **Compare the restored values; do not just `Assert.NotNull`.** Round-tripping an object with no data and asserting
  non-null is a false green that does not do what its name says: there is nothing to lose. Correct example:
  `tests/Polhem.Api.Core.UnitTests/TestFunc.cs` (including `comparedCount > 0` to guard against the helper silently
  degrading).
- **Test collection members with values.** The breadth test (`ApiContractSerializationTests`) fills every
  `IEnumerable` with `null`, so for collection members it only guarantees that "an empty instance can round-trip".
- Test the empty-collection / single-node boundaries once each.
- **Mobile gate**: `dotnet test <project> -c Release --settings .runsettings -p:DynamicCodeSupport=false`
  (zero failures expected). CI runs the same gate on the `Polhem.Api.Core` / `Polhem.Definition` / `Polhem.Base`
  projects.

## Complete checklist

- [ ] Identify the use: persistence (XML)? API transport (JSON/MessagePack)? Or all three?
- [ ] Object: parameterless ctor + `[XmlAttribute]`/`[XmlElement]` + derived fields `[XmlIgnore, JsonIgnore]`
- [ ] **The definition layer carries no MessagePack attributes and adds no `PackageReference` to `MessagePack`**
- [ ] Collections: elements `: CollectionItem` / `KeyCollectionItem`, collections `: CollectionBase<T>` /
      `KeyCollectionBase<T>`
- [ ] The collection has only one public `Add` and a parameterless ctor; collection properties mapped by
      `[XmlElement]` have a public setter
- [ ] **Register the contract in `WireContracts.<Axis>.cs`**; register `CollectionBaseFormatter<,>` for collections;
      add new closed generic instantiations to `WireContracts.Generics.cs`
- [ ] New value types for `object` members are added to the closed `WireValueCode` set, without relying on the escape
      hatch
- [ ] No mutation or `XmlCodec.Serialize` on cached instances obtained from `IDefineAccess.GetX(...)` (`Clone()` first
      if you need to change one)
- [ ] Round-trip tests **compare values** (not just `Assert.NotNull`), collection members carry values, empty-collection
      boundary included
- [ ] `dotnet build Polhem.slnx -c Release --no-incremental` 0w/0e
- [ ] The `-p:DynamicCodeSupport=false` gate has zero failures

## Reference files (read them alongside the code)

| Purpose | File |
|------|------|
| Triple-serializable object + collection sample | `src/Polhem.Definition/Organization/DepartmentTree.cs` / `DepartmentNode.cs` / `DepartmentNodeCollection.cs` |
| Polymorphic collection (with JsonConverter) | `src/Polhem.Definition/Filters/FilterNodeCollection.cs` / `FilterGroup.cs` |
| Collection bases | `src/Polhem.Base/Collections/CollectionBase.cs` / `KeyCollectionBase.cs` / `CollectionItem.cs` / `KeyCollectionItem.cs` |
| **Everything on the wire side** (registration list, formatters, resolver chain, `object` envelope, whitelist, drift gate) | `src/Polhem.Api.Core/MessagePack/` (**for the file list see `src/Polhem.Api.Core/CLAUDE.md`**; not listed here) |
| XML persistence codec | `src/Polhem.Base/Serialization/XmlCodec.cs` |
| Serialization lifecycle (SerializeState propagation) | `src/Polhem.Base/Serialization/IObjectSerialize.cs` |
| Round-trip test samples | `tests/Polhem.Api.Core.UnitTests/TestFunc.cs`, `tests/Polhem.Api.Core.UnitTests/WireFormatterTests.cs` |

## Related rules

This skill is the operational side of "designing a triple-serializable object". Three authoritative sources each own
one part; **always go back and read them for details; they are not copied here**:

| Source | Owns | How it is loaded |
|------|--------|---------|
| `rules/serialization.md` | Hard rules for wire binding (per-request body codec negotiation, the definition layer must not bring in transport packages) | Always loaded |
| `src/Polhem.Api.Core/CLAUDE.md` | **Wire registration procedure, misjudgment points, `object` envelope, AOT measurements and regression gates** | When that project is touched |
| `rules/apple-mobile-trim.md` | Full context for mobile trim / AOT and the type-shape requirements | Always loaded |
| `rules/definition.md` + `src/Polhem.Definition/CLAUDE.md` | Definition-layer collection bases, cache immutability, how to write setters | Always loaded / when touched |
