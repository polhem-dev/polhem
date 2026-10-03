# Serialization and expression engine rules (core)

> The wire formatter registration procedure, the three easy misjudgements, the `object` envelope, AOT measurements
> and the regression gates → `src/Polhem.Api.Core/CLAUDE.md` (loaded automatically when you touch that project).
> Type shape requirements for mobile trim / AOT → `rules/apple-mobile-trim.md`.
> Pitfall background → `maintainers/gotchas/serialization-and-expressions.md`.

## The wire body codec is negotiated per request (adr-044)

The body codec **is not a deployment setting**. Each request declares it in the `codec` field of the payload
envelope, and the server **responds with the same codec**. **Undeclared means MessagePack**: that is a compatibility
constant, not a chosen default (every client that predates negotiation declares nothing and sends MessagePack).
The built-in names are in `PayloadCodecNames`; the registry that maps a name to its implementation is the
`PayloadOptions` of Polhem.JsonRpc.Payload (`ResolveCodec`, `CodecNames`, and `RegisterCodec` for a codec the framework
does not ship), which `PolhemPayload.CreateOptions` fills the framework's way. **This file does not copy that list.**
The settings name only the compressor and the encryptor, never the codec.

`PayloadFormat` (Plain/Encoded/Encrypted) is **the encryption/compression dimension**, orthogonal to the codec: a
`Plain` body is always the envelope's own System.Text.Json; only `Encoded` / `Encrypted` bodies are spelled by the
codec.

Therefore, **when a .NET client (including the iOS / Android / WASM heads) declares nothing, both ends still run
MessagePack**, and `ApiConnector.PayloadCodec` is the only way to change it on that end. The assumption "mobile uses
JSON, MessagePack is only between desktop/server" still does not hold.

> ⚠️ **`ApiPayloadOptions.Serializer` was removed in Bee.NET 4.27.0** (breaking change); a `<Serializer>` element
> left over in an existing `SystemSettings.xml` is **ignored**, not honoured.
> "The framework has no JSON body serializer" and "`CreateSerializer` has only one case" are conclusions from
> **Bee.NET 4.26.0 and earlier**. **Do not reason from them any more.**

## Wire shape changes have a downstream in another repository

`wire-contracts/messages.d.ts` and `wire-fixtures/` are **the authoritative source of the cross-language contract**.
The TypeScript client [`polhem-connector-js`](https://github.com/polhem-dev/polhem-connector-js) syncs them from a
**framework release tag** (its `scripts/framework-ref.mjs`; it deliberately does not check in a copy of the fixtures:
a copy would be a second authority), and its CI compares what it fetches.

So **a change to these two places does not reach that repository until it is released and connector-js moves its tag;
its CI turns red then, and that is expected, not an accident**. Nothing goes red when the change lands here, so the
follow-up has to be arranged on purpose: plan the connector-js change for the same release. Releasing the change
without it leaves the two halves of the same contract contradicting each other, while the TS side still parses the
old shape.

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
misjudgements are in `src/Polhem.Api.Core/CLAUDE.md`; a missed registration is caught by `WireContractDriftTests`,
and a member the formatter writes but does not read back, or that the two body codecs carry differently, by
`WireCodecParityTests`.

> "MessagePack works under AOT" is an old conclusion disproved by measurement on 2026-08-10. **Do not reason from
> it.** How it was disproved, and the record of the time it broke the entire iOS wire, are kept in
> `src/Polhem.Api.Core/CLAUDE.md`.

## DynamicExpresso: AOT needs the fixed invoker shape, trimming needs the descriptor

Both halves need something, and each has its own gate.

- **Dynamic code (AOT): every expression compiles to `Func<object?[], object?>`.** Without dynamic code
  `LambdaExpression.Compile()` does fall back to the interpreter, but it still has to hand back a delegate of the
  lambda's exact signature. On iOS and Mac Catalyst (Mono, AOT-only) a signature with more than two parameters needs a
  `DynamicMethod` thunk and throws `ExecutionEngineException`; under NativeAOT a signature with any value type has no
  code at all. `DynamicExpressoEvaluator.BuildInvoker` wraps DynamicExpresso's parsed body in the fixed signature, which
  needs neither. **Never call DynamicExpresso's `Lambda.Invoke` or `Lambda.Compile`**: both compile the typed
  delegate. `InterpretedInvokerGateTests` (tests/Polhem.Expressions.UnitTests) forces interpretation on the desktop and
  fails when a compiled expression sits behind a runtime-generated thunk.
  > **Measured correction (2026-09-28).** This bullet used to say "nothing to do: `Compile()` falls back to the
  > interpreter". Editing an order line on the iOS simulator (`quantity * unit_price * (1 - discount)`) terminated the
  > Northwind app. `-p:DynamicCodeSupport=false` could not see it, because CoreCLR still JITs the emitted thunk; the
  > 2026-07-09 measurement that produced the old conclusion ran only there. The account is in
  > `maintainers/gotchas/serialization-and-expressions.md`.
- **Trimming: `src/Polhem.Expressions/ILLink.Descriptors.xml` is required.** DynamicExpresso finds `Math.*`,
  `string.*`, `DateOnly.*` and every other member an expression names by reflection, so the default mobile trim
  (`TrimMode=partial`) removes the ones nothing else references. Measured on 2026-09-26 without the descriptor:
  `Math.Round`, `Math.Abs`, `ToUpper()`, `Today().AddDays(1)`, `Math.PI` and others fail with "No applicable method" /
  "No property or field". The descriptor ships inside the package and roots the interpreter's exposed types;
  `TrimmerDescriptorGateTests` (tests/Polhem.Expressions.UnitTests) fails when the exposed types and the descriptor
  disagree. **When you reference a new type in the interpreter or add a helper function with a new return type,
  add it to the descriptor.**

The degrade mechanism of `FormLiveComputation.IsDegraded` protects against syntax and identifier errors in
customer-written expressions. A trimmed-away member looks exactly like one of those errors, which is why the trim
failure was silent: the form just stopped computing live.

## Two hard requirements for the expression variable table

This spans two places, `Polhem.Core` (`ExpressionPolicy`) and each UI head (`FormLiveComputation`), so it stays
always loaded.

1. **Variable keys always use `FormField.FieldName` (the casing declared in the schema)**, not
   `DataColumn.ColumnName`. **DynamicExpresso identifiers are case-sensitive**, and expressions are written with the
   declared field names; using `DataColumn.ColumnName` as the key ties the expression to "whichever casing the
   in-memory DataSet currently stores column names in". `DataRow` indexing and `Fields.Contains` are
   case-insensitive anyway, so writing back is unaffected.

   > **`AddColumn` now stores lowercase, not uppercase** (`fieldName.ToLowerInvariant()`,
   > `src/Polhem.Core/Data/DataTableExtensions.cs`). Historically it stored uppercase, and using uppercase as the
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
