# ADR-039: `FormLayout` returns to design time; the runtime no longer derives it from `FormSchema`

[繁體中文](adr-039-formlayout-design-time-only.zh-TW.md)

## Status

**Accepted (2026-08-20)**: the decision has been carried out and shipped with 4.23.0.

[ADR-016](adr-016-multitenant-customization-overlay.md) established the **whole-file replacement** semantics of the
customization layer, and "`FormLayout` is the authoritative source of the screen" is a corollary of those semantics,
stated explicitly in the [Definition Files Overview](../en/definition-files-overview.md) (commit
[`53025c34`](https://github.com/jeff377/bee-library/commit/53025c34)). This ADR fills in how that corollary behaves
**when the file is missing**; the two-layer read-only overlay semantics of ADR-016 are unchanged.

## Context

`FormLayout` is the projection of `FormSchema` in the UI dimension and describes the visual arrangement of a form. The
framework had long established that **"`FormLayout` is the authoritative source of what is on the screen"**
([Definition Files Overview](../en/definition-files-overview.md)): the customization layer uses whole-file
replacement, so a field added to the base schema **does not** automatically appear on the screen of a tenant that has
customized it. What a tenant sees is decided by that tenant's layout file.

But the rule had a hole: **when the layout file is absent, the runtime derives one from `FormSchema` on the fly**. The
entry point was `FormSchema.GetFormLayout(string)`, a one-line call to `FormLayoutGenerator`, used by three runtime
paths (the missing-file branch of `FormDefinitionLoader`, and the no-loader branches of the two UI heads, Avalonia and
Blazor).

This made the "authoritative source" degrade, when the file was missing, into a live projection of the schema, and
**that projection had been reviewed by nobody and was stored nowhere**: exactly what the concept of an "authoritative
source" is meant to exclude. The same deployment could derive layout A today and, because a field was added to the
schema, derive layout B tomorrow, with no human decision in between.

**POLHEM2005 (a FormSchema should have a corresponding FormLayout)** in `src/Polhem.Analyzers` already anticipated
this positioning, except that what it warned about was silently filled in at runtime, so the warning had no
consequences at all.

## Decision

**`FormLayout` is always produced at design time and saved as a definition file; the runtime reads it as is, and a
missing file is a configuration error.**

### 1. The missing-file error sits in the runtime composition layer, not pushed down to storage

`IDefineStorage.GetFormLayout` stays `FormLayout?` (nullable); it does not change to "throw when the file is missing".

The reason is that the same interface member has two implementations with opposite semantics:
`CustomizeOnlyStorage.GetFormLayout` **must** be able to return `null`, since a tenant having no customization is the
normal case. One member cannot be "missing file is an error" for the base layer and "missing file is normal" for the
customization layer at the same time. The storage layer only answers "is the file there"; **how to interpret `null`
belongs to the caller**.

What changes is who treats `null` as an error: `FormDefinitionLoader.GetRuntimeLayoutAsync` changes from "generate
one" to throwing `InvalidOperationException`, with a message naming the missing `layoutId` and the relative path where
the file should be.

> **Correction during implementation**: when the base layer is missing the file, in practice it is usually the
> **server** that throws first. `ClientDefineAccess.GetFormLayoutAsync` goes through `GetDefine` /
> `DefineType.FormLayout`, which maps to `CacheDefineAccess.GetFormLayout(layoutId)` on the server, and that overload
> already threw on a missing file ("missing file is an error" **already held** at that layer). The loader's guard
> covers the remaining case: the server returns an empty payload. Therefore **tests should not assert the exception
> type**, only that "an exception is thrown and its message contains the layoutId".

### 2. The generator stays in `Polhem.Definition` and becomes `public`, and `FormSchema.GetFormLayout` is removed

`FormLayoutGenerator` changes from `internal` to `public`, and its `<remarks>` state explicitly that it is for design
time.

**Removing the instance method is the key step.** `Schema.GetFormLayout()` could be called in one line, which is
exactly "the shape the runtime calls in passing"; once it takes an explicit `using Polhem.Definition.Layouts;` followed
by `FormLayoutGenerator.Generate(schema, layoutId)`, the intent can no longer hide.

Three reasons not to move it to the `tools/` side:

1. `tools/DefineEditor` **has no test project** (only its own `Smoke.cs`), so the existing generator tests would have
   nowhere to go.
2. This repository's scaffolding workflow calls it in the form of "a public framework API".
3. If external framework users build their own definition tools, `DefineEditor` is not the only possible producer.

### 3. The no-loader branch of the UI heads becomes a three-stage resolution, and `FormView.Layout` is added

`FormView` (Avalonia) gains an overridable `ResolveLayoutAsync`, which resolves in order:

1. `FormView.Layout` set by the host
2. The runtime layout assembled by `DefinitionLoader`
3. The base definition obtained through `ClientInfo.DefineAccess`
4. None of these → throw `InvalidOperationException`

The public property `FormView.Layout` needed by step 1 is a required companion of this decision: `FormView.Schema` is a
public property, and a host **can** put a schema in directly with no backend behind it at all (the layout module of
`samples/Avalonia.DemoCenter` does exactly that). With derivation removed, that path would have no way out, so a
symmetric `Layout` is added.

Blazor's `FormPage` symmetrically switches to reading `GetDefineAsync<FormLayout>`.

> Step 3 **must `Clone()`**: `ClientDefineAccess` caches definitions per instance, and `LayoutCapabilityApplier.Apply`
> mutates in place, so handing it the cached instance directly would violate "definitions in the cache must not change
> after init". The loader path already clones.

### 4. `GetListLayout()` / `GetLookupLayout()` are out of scope

Both stay as they are. The list field set (`FormSchema.ListFields`) and the lookup field set (`LookupFields`) **are
declared on `FormSchema` to begin with**; `DefineType` has no corresponding type and they have no file form at all.
They are projections of the schema, not independent definitions. That is a different matter from "the single-record
form layout".

### 5. POLHEM2005's wording is strengthened; its severity stays Warning

The message changes from "should have" to "a missing file will fail at runtime". **It is not raised to Error**: that
would make existing app repositories fail to build immediately, a cost out of proportion to the benefit; the actual
consequence of a missing file is already carried by the runtime exception.

## Rationale

**Why not keep derivation as a "convenient default".** Because it is incompatible with "authoritative source", not
because it is inconvenient. A layout reviewed by nobody and stored nowhere changes silently as the schema drifts, so
the screen users see depends on "when someone last changed the schema", not on "when someone last decided what the
screen looks like". That is exactly the situation the rule "the layout is the authoritative source" is meant to
exclude; the missing-file path simply had not been closed off along with it.

**Why a breaking change rather than gradual deprecation.** An `[Obsolete]` attribute cannot stop derivation from
happening, and as long as derivation keeps happening the rule keeps its hole. POLHEM2005 already exists, so before
upgrading you can build to get the complete list of missing files, and the migration path is clear (generate one with
`DefineEditor`, review it, save it). The cost of removing it outright is therefore under control.

**Why the generator stays in the framework and not only in the tools.** "Layouts are produced at design time" is the
rule; "which tool produces them" is not. Keeping the generator as a public framework API lets external users build
their own producers, and the rule itself is not tied to `DefineEditor`.

## Consequences

**Positive**:

- "`FormLayout` is the authoritative source of the screen" no longer has exceptions; the content of a layout is always
  the result of some human decision.
- A missing file goes from being silently filled in to **a build-time warning (POLHEM2005) + an explicit runtime
  exception**, and both point to the file that needs to be added.
- The generator becomes a public API, so external definition tools can use the same implementation.

**Negative / costs**:

- **Binary breaking change**: the public `FormSchema.GetFormLayout` is removed, and consumers will get
  `MissingMethodException`. It has been declared with `*REMOVED*` in `PublicAPI.Unshipped.txt`.
- **Any deployment that relies on runtime derivation will fail when opening a form after upgrading**: this is the
  intent of the decision, but the way to add the missing files must be listed in the CHANGELOG.
- The definition work for adding a form goes from 4 places to 5 (one more FormLayout file).

**Companion changes** (landed together with this decision):

- The FormSchema node of `tools/DefineEditor` gains a "Generate FormLayout" command that writes
  `{DefinePath}/FormLayout/{ProgId}.FormLayout.xml`. **It asks for confirmation before overwriting an existing file**:
  regenerating throws away a manually adjusted layout, and it is the only destructive action of the feature.
- `samples/Define/` gains the three layout files that previously relied entirely on derivation.

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **`tools/DefineEditor` has a test project now**: `tests/Polhem.DefineEditor.UnitTests/`. The first of the three
  reasons for keeping the generator in `Polhem.Definition` no longer applies; the other two still do, and the generator
  has not moved.
- **The DemoCenter layout module no longer puts a schema into `FormView.Schema`.** It generates a layout with
  `FormLayoutGenerator.Generate` and renders it with `FormLayoutRenderer.Render`
  (`samples/Avalonia.DemoCenter/Modules/Layouts/AutoFormLayoutModule.cs`). `FormView.Layout` and the overridable
  `ResolveLayoutAsync` are unchanged, and a host that sets `FormView.Schema` without a backend still needs them.
