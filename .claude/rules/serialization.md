# Serialization and expression engine rules (core)

> The wire formatter registration procedure, the three easy misjudgements, the `object` envelope, AOT measurements
> and the regression gates → `src/Polhem.Api.Core/CLAUDE.md` (loaded automatically when you touch that project).
> Type shape requirements for mobile trim / AOT → `rules/apple-mobile-trim.md`.
> Pitfall background → `docs/repo-ops/gotchas/serialization-and-expressions.md`.

## The wire body codec is negotiated per request (adr-044)

The body codec **is not a deployment setting**. Each request declares it in the `codec` field of the payload
envelope, and the server **responds with the same codec**. **Undeclared means MessagePack**: that is a compatibility
constant, not a chosen default (every client that predates negotiation declares nothing and sends MessagePack).
The available names and their implementations are in `PayloadCodecNames` and
`ApiPayloadOptionsFactory.CreateSerializer`; **this file does not copy that list**.

`PayloadFormat` (Plain/Encoded/Encrypted) is **the encryption/compression dimension**, orthogonal to the codec: a
`Plain` body is always the envelope's own System.Text.Json; only `Encoded` / `Encrypted` bodies are spelled by the
codec.

Therefore, **when a .NET client (including the iOS / Android / WASM heads) declares nothing, both ends still run
MessagePack**, and `ApiConnector.PayloadCodec` is the only way to change it on that end. The assumption "mobile uses
JSON, MessagePack is only between desktop/server" still does not hold.

> ⚠️ **`ApiPayloadOptions.Serializer` was removed in 4.27.0** (breaking change); a `<Serializer>` element left over
> in an existing `SystemSettings.xml` is **ignored**, not honoured.
> "The framework has no JSON body serializer" and "`CreateSerializer` has only one case" are conclusions from
> **4.26.0 and earlier**. **Do not reason from them any more.**

## Wire shape changes have a downstream in another repository

`wire-contracts/messages.d.ts` and `wire-fixtures/` are **the authoritative source of the cross-language contract**.
The TypeScript client [`polhem-connector-js`](https://github.com/polhem-dev/polhem-connector-js) syncs them (it deliberately
does not check in a copy: a copy would be a second authority), and its CI compares what it fetches.

So **changing these two places turns that repository's CI red, and that is expected, not an accident**: the red light
is the notification mechanism. Landing the change here without following up there leaves the two halves of the same
contract contradicting each other, while the TS side still parses the old shape.

> This rule is deliberately recorded in polhem and not in polhem-connector-js: **the person who needs it is the one
> changing the wire here**, and that person will not open the other repository. Recording it there would guarantee
> it is ignored.

Test: **will this change produce a diff in `wire-contracts/` or `wire-fixtures/`?** If yes, arrange the other side
at the same time. The regeneration commands are in the README of each of those two folders; this file does not
copy them.

## The definition layer must not bring in a transport format package (adr-036)

`src/Polhem.Definition` **must not** have a `PackageReference` to `MessagePack` (or any transport format package).
The criterion is "would it give the definition layer an external package dependency": `[XmlIgnore]` /
`[JsonIgnore]` are BCL vocabulary and are allowed; MessagePack attributes are not. The only MessagePack dependency
in the whole repository is in **`Polhem.Api.Core`**.

Wire binding is handled by the **hand-written formatters** in `src/Polhem.Api.Core/MessagePack/`; definition types
carry no attributes.

## New wire types must have an explicitly registered formatter

`ContractlessStandardResolver` **has no reflection fallback**; it is only a convenience on desktop. It relies on
`Reflection.Emit`, and .NET for iOS sets `DynamicCodeSupport=false` for every build, so an unregistered type there
throws `FormatterNotRegisteredException` (it does not just get slower).

When you add `Polhem.Api.Core.Messages.*`, a definition-layer type or collection transitively reachable from it, you
**must** register it in `src/Polhem.Api.Core/MessagePack/WireContracts.*.cs`. The full procedure and the three easy
misjudgements are in `src/Polhem.Api.Core/CLAUDE.md`; a missed registration is caught by `WireContractDriftTests`.

> "MessagePack works under AOT" is an old conclusion disproved by measurement on 2026-08-10. **Do not reason from
> it.** How it was disproved, and the record of the time it broke the entire iOS wire, are kept in
> `src/Polhem.Api.Core/CLAUDE.md`.

## AOT: DynamicExpresso needs no special handling (this rule is unchanged)

DynamicExpresso's `Expression.Compile()` automatically falls back to the **interpreter** when
`IsDynamicCodeSupported=false`.

**Mobile does not need to disable live computation for AOT.** The degrade mechanism of
`FormLiveComputation.IsDegraded` protects against "syntax/identifier errors in customer-written expressions" and is
**unrelated to AOT**.

## Two hard requirements for the expression variable table

This spans two places, `Polhem.Base` (`ExpressionPolicy`) and each UI head (`FormLiveComputation`), so it stays
always loaded.

1. **Variable keys always use `FormField.FieldName` (the casing declared in the schema)**, not
   `DataColumn.ColumnName`. **DynamicExpresso identifiers are case-sensitive**, and expressions are written with the
   declared field names; using `DataColumn.ColumnName` as the key ties the expression to "whichever casing the
   in-memory DataSet currently stores column names in". `DataRow` indexing and `Fields.Contains` are
   case-insensitive anyway, so writing back is unaffected.

   > **`AddColumn` now stores lowercase, not uppercase** (`fieldName.ToLowerInvariant()`,
   > `src/Polhem.Base/Data/DataTableExtensions.cs`). Historically it stored uppercase, and using uppercase as the
   > key threw `UnknownIdentifierException` outright; ADR-029 migrated the stored casing to lowercase, which
   > **happens to match the declared field names**. This **does not make this rule obsolete**: the conclusion was
   > always "decouple from the stored casing", not "avoid uppercase"; the coincidental match only hides the symptom
   > of code that violates this rule for now.

   **Regression tests must build the DataTable with column names whose casing differs from the declared field
   names** (under the current implementation, uppercase). Testing with exactly the same lowercase as the declared
   names passes with both approaches, so it does not test the decoupling at all.
2. **`ExpressionPolicy.CoerceValue` cannot rely on `Convert.ChangeType` alone.** `Guid` / `byte[]` are not
   `IConvertible`. A GUID column read back from SQLite on the client is a **String**, and may be an **empty string**.
   Rule: `Guid` → empty/whitespace returns `Guid.Empty`, otherwise `Guid.Parse`; `byte[]` → empty string returns an
   empty array, otherwise `FromBase64String`. This aligns with the "null/DBNull → the type's default value" policy.
