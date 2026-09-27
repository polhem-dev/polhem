# ADR-025: Definition types compatible with the AOT reflection XmlSerializer (a single Add + parameterless constructors)

[繁體中文](adr-025-define-types-aot-xmlserializer-compat.zh-TW.md)

## Status

Accepted (2026-06-26)

## Context

Polhem's definition types (`FormSchema`, `TableSchema`, `ProgramSettings`, `FormLayout`, `LanguageResource`,
`DbCategorySettings`, `PermissionModels` and so on) are **persisted as XML**, and the client deserializes them through
`ClientDefineAccess.GetDefineAsync<T>` → `XmlCodec.Deserialize` (`System.Xml.Serialization.XmlSerializer`). The
desktop (`net10.0`) and browser (Avalonia WASM, not AOT) runtimes **have `Reflection.Emit`**, so XmlSerializer takes
the **code-generated** path, which has worked fine for years.

When `Polhem.Northwind.iOS` (an Avalonia iOS head) was added, connecting and logging in succeeded, but **loading any
XML definition crashed**. The root cause: **iOS forbids dynamic code generation (no `Reflection.Emit`)**, so
XmlSerializer falls back to the **reflection-only** path (`ReflectionXmlSerializationReader`), which exposed two
places where Polhem's definition types are incompatible with that path:

1. **Overloaded collection `Add` → `AmbiguousMatchException`**: the reflection reader looks up a collection's add
   method with `Type.GetMethod("Add")` (**without parameter types**). Polhem collections (subclasses of
   `KeyCollectionBase<T>` / `CollectionBase<T>`) usually have several public `Add`s: the inherited `Add(T)`, the
   base interface's `Add(I…CollectionItem)`, and each collection's convenience `Add(string, …)`. More than one throws
   `AmbiguousMatchException`.
2. **Collections without a parameterless constructor → `MissingMethodException`**: the reflection reader creates a
   collection with `Activator.CreateInstance(type)`. Many definition collections are **owner-coupled** (the
   constructor only takes the owner, such as `ProgramItemCollection(ProgramSettings)`) and have no public
   parameterless constructor. The code-gen path uses the getter's existing instance (lazy-init with owner) and so
   does not need one; the reflection path does.

Both are **triggered only on the AOT/reflection path**; the code-gen path on desktop / WASM was fine all along. This
is a structural constraint between the framework's external API surface (the definition types) and their
serialization mechanism, hence this ADR.

## Options considered

1. **Implement `IXmlSerializable` in the collection bases**: custom Read/Write that bypasses reflection's probing of
   `Add` / constructors. **Rejected**: once a member is `IXmlSerializable`, XmlSerializer **ignores the member-level
   `[XmlArray]` / `[XmlArrayItem]` and polymorphism handling** (such as the `[XmlArray("Items")]` of
   `LanguageResource.Items`, or the `[XmlInclude]` element-name polymorphism of `FilterNodeCollection`). That would
   mean redoing XmlSerializer's array + polymorphism logic inside ReadXml/WriteXml, which is large and fragile, and
   **would change the existing XML format**.
2. **Switch the definition wire to JSON / MessagePack**: the client would deserialize definitions with
   `System.Text.Json` / MessagePack instead (`ICollection<T>.Add(T)` does not collide with overloads and needs no
   reflection-only XmlSerializer). Technically feasible, and it fits the split of "XML = persistence, JSON/MsgPack =
   wire", but **rejected**: the user explicitly chose to keep the XML deserialization path; and it would require
   changing the definition transport on both server and client, a large blast radius.
3. **Pregenerate XmlSerializers in the iOS head (`Microsoft.XmlSerializer.Generator` / sgen)**: expand the serializers
   of the definition types into static code at build time, bypassing runtime reflection. **Rejected**: every type that
   needs serializing has to be fed to the generator, the build setup is tedious, and it is only an iOS-side band-aid
   that does not remove the incompatibility of the framework types themselves.
4. **Make the definition types themselves compatible with the reflection XmlSerializer (adopted)**: see the next
   section. The smallest change, the format is unchanged, it covers every AOT target at once, and needs no per-type
   maintenance.

## Decision

Adopt **option 4**, with two fixes; **the XML format is unchanged, character for character** (XmlSerializer still
handles collections as usual, and `[XmlArray]` / polymorphism are preserved):

- **D1: every definition collection keeps only one public instance `Add(T)`**:
  - The interface methods `Add(IKeyCollectionItem)` / `Add(ICollectionItem)` of the bases `KeyCollectionBase<T>` /
    `CollectionBase<T>` / `MessagePackKeyCollectionBase<T>` / `MessagePackCollectionBase<T>` become **explicit
    interface implementations** (`void IKeyCollectionBase.Add(…)`): they disappear from the public probing of
    `Type.GetMethod("Add")`, and interface calls work as before.
  - The convenience `Add(...)` of each definition collection (such as `FormFieldCollection.Add(string, string,
    FieldDbType)`) becomes an **extension method** (`public static … Add(this XxxCollection? collection, …)` +
    `ArgumentNullException.ThrowIfNull`, in the same file and the same namespace). The caller's `.Add(...)` syntax is
    unchanged (when C# finds no instance overload it resolves to the extension method), and `Type.GetMethod("Add")`
    does not see extension methods.
- **D2: owner-coupled collections get a public parameterless constructor**: the 12 definition collections that lacked
  a parameterless constructor get `public Xxx() : base()`. For a read-only collection property the reflection reader
  builds a temp (which needs this constructor) and then puts the items into the getter's owner-coupled collection, so
  **the owner is unaffected** (the temp has a null owner, and the items go into the real owner's collection). The
  item types already had parameterless constructors.

## Consequences

- iOS (and any AOT target: a future MAUI iOS, NativeAOT and so on) can deserialize every XML definition correctly.
  Northwind iOS passes end to end (connect → log in → menu → list with real data → open a record).
- **The format is unchanged**: verified by the round-trip serialization tests, Definition 723 + Base 464, all green;
  existing persisted definition files need no migration.
- **Zero behavior impact on desktop / browser**: the code-gen path already uses the getter + the strongly typed Add,
  so this fix is transparent to it.
- **A small API surface adjustment (breaking, see the CHANGELOG)**: the convenience `Add(...)` changes from an
  instance method to an extension method. It is **source compatible** (the `.Add(...)` syntax is unchanged, and the
  extension class is in the same namespace so no new using is needed), but it is binary breaking for **already
  compiled** external users. The fallout in a few internal callers was fixed at the same time (adding
  `using Polhem.Definition.Collections`, `!` null assertions, casting to the interface in tests of the interface Add).
- **Follow-up rules (constraints on writing definition types)**: when adding a definition collection:
  1. **Expose only one public instance `Add`** (the inherited strongly typed `Add(T)`); convenience overloads are
     always provided as **extension methods**.
  2. **A public parameterless constructor is required** (an owner-coupled collection adds a `: base()` one besides the
     owner constructor).
  3. Serialized **item types must also have a public parameterless constructor**.
  Compatibility with the reflection XmlSerializer is a hard requirement, so that future new types do not hit the same
  pitfall on AOT targets. The analyzers and the test that enforce these rules today are listed under
  "Implementation evolution" below.

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **2026-08-09: the `MessagePack*CollectionBase<T>` bases named in D1 no longer exist.** They were merged back
  into the `Polhem.Base.Collections` bases by [ADR-036](adr-036-wire-serialization-externalized.md);
  D1 applies to those.
- **2026-09-27: what enforces the follow-up rules.** The analyzer POLHEM4005 (warning) reports a framework collection
  that declares an additional public `Add` overload, and POLHEM4006 (error) reports a framework collection or
  collection item without a public parameterless constructor (`src/Polhem.Analyzers/DiagnosticIds.cs`). Abstract
  types are not constructed by a deserializer and are exempt; `KeyCollectionBase<T>` and `CollectionBase<T>` are now
  both abstract with protected constructors, and the rules apply to their concrete subclasses.
  `XmlSerializerShapeGateTests` (`tests/Polhem.Definition.UnitTests`) walks every type the definition files reach and
  checks the same shape rules, plus one more that the reflection-only serializer imposes: a collection property mapped
  to repeated `[XmlElement]` must have a public setter.
- **2026-09-27: empty collections are omitted by `XSpecified` properties.** The per-object serialize state that used to
  decide whether an empty collection is written was removed, because serializing a cached definition mutated it.
  Empty collections are now omitted by get-only `{Property}Specified` properties. `ShouldSerialize{Property}()` was
  not used for this, because the reflection-only XmlSerializer on iOS throws when that method is declared on a base
  class.
