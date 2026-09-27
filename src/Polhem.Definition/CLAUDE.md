# Polhem.Definition: design rules for definition types

This file loads automatically when an agent touches any file under `src/Polhem.Definition/` (nested `CLAUDE.md`
files are lazily loaded).
**The cross-layer rule (definition data in the cache must not be mutated after init) stays in
`.claude/rules/definition.md` (always loaded)**: the people who violate it are every `IDefineAccess` consumer, not
only this project.

For the dependency boundary (no more `PackageReference`s in this project) see `.claude/rules/dependency-boundary.md`;
for the type shape requirements on mobile see `.claude/rules/apple-mobile-trim.md`.

## Collection properties always inherit a base class

Child collections in definition files (FormSchema / FormLayout / TableSchema / LanguageResource, etc.) always inherit
`Polhem.Base.Collections.KeyCollectionBase<T>` (items have a key) or `CollectionBase<T>` (items have no key).
**Do not use** bare BCL collections such as `List<T>`, `Collection<T>`, `IList<T>` as public property types.

**Why:** it centralizes serialization, the `IObjectSerialize` lifecycle, `ITagProperty`, Owner back-navigation and
key uniqueness checks in a single base. Supporting a new serialization format, adding change notification or doing
cache invalidation in the future then means changing only the base.
**A bare `List<T>` bypasses the base mechanism; when the base gains new behavior, this collection will not follow and
becomes an exception.**

- When adding a collection property, first write an `<Item>Collection` that inherits the matching base, then declare
  the property as that collection type.
- Items inherit `KeyCollectionItem` / `CollectionItem`; a domain-meaningful key uses the proxy pattern
  (`FormField.FieldName { get => Key; set => Key = value; }` + `[XmlAttribute]`).
- The MessagePack wire needs no special base class (the MessagePack-specific variants were removed by ADR-036).
  A collection that travels on the wire gets an explicitly registered formatter instead; see
  `rules/serialization.md` § New wire types must have an explicitly registered formatter.
- The only case where this may be bypassed: the item is a pure value-type DTO and **never appears within
  Polhem.Definition**.

Reference implementations: `FormFieldCollection`, `LayoutColumnCollection`, `DbFieldCollection`,
`LanguageItemCollection`.

## Field reference properties have no `Name` suffix

A property that "refers to other fields by string name" is `XxxField` when singular and `XxxFields` for a
comma-separated list, **without `Name`**.

Existing family: `FormSchema.ListFields` / `LookupFields`, `FieldMapping.SourceField` / `DestinationField`,
`FormField.DisplayFields`. `FormField.FieldName` has `Name` because it is the field's own identity, not a reference.

## Mobile compatibility requirements for collection types (reflection-only `XmlSerializer`)

The iOS AOT path uses the reflection-only `XmlSerializer`, which is stricter about type shape than the desktop.
**These three points never show up on the desktop and only blow up on mobile**, and the violators are always
definition types in this project:

- A collection type may expose **only one** public instance `Add`; several overloads throw
  `AmbiguousMatchException`. Convenience overloads must be displaced into extension methods (see the one-type-per-file
  exception in `code-style.md`).
- A collection type **must have a parameterless constructor**, otherwise it throws `MissingMethodException`.
- **A collection property mapped as repeated `[XmlElement]`s must have a public setter** (added 2026-08-10).
  The reflection-only path **assigns** such a member instead of calling `Add`, so a get-only property throws
  `ArgumentException: Property set method not found`, which shows up as the misleading
  "There is an error in XML document (line, column)". **A get-only collection under `[XmlArray]` is not affected**:
  the difference is only in the mapping, not in the collection itself.
  Write the setter as "clear, then `Add` each item into the existing instance" rather than replacing the field;
  that keeps the owner link intact (example: `Entries` in `Language/LanguageEnum.cs`).

For the reflection scan that inventories the whole definition layer for the same kind of problem, see
`../../docs/repo-ops/gotchas/mobile-trim-aot.md`.

## `Defaults/` is only a scaffold source; the runtime does not use it

`Defaults/` is loaded from embedded resources and is **the scaffold source for "start a new project"** (it lays out
the FormSchema/TableSchema of framework system tables such as `st_employee` / `st_department` as initial definition
files).

**Runtime definition loading reads only `PathOptions.DefinePath`** (the backend uses `CacheDefineAccess` +
`FileDefineStorage`; the frontend uses `ClientDefineAccess`, which fetches through the API and never touches the
file system).
**There is no load-priority mechanism of "fall back to Defaults when DefinePath is missing something".**

To use a framework system table in a project, **copy its definition from `Defaults/` into the project's
`DefinePath`** as a starting point, then extend as needed (keep the framework's standard fields; features such as
permissions and organization depend on them).
