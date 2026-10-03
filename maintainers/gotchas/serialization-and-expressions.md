# Pitfall log: serialization and the expression engine

The matching hard rules are in `.claude/rules/serialization.md`; for mobile trim/AOT see
`.claude/rules/apple-mobile-trim.md`.

## Setting the facts straight: the serialization dimension ≠ the encryption dimension (once confused)

What the investigation found (2026-07-22):

- **`PayloadFormat` (Plain / Encoded / Encrypted) = the encryption / compression dimension**, not JSON-vs-MessagePack.
- At the time, the body serializer was decided by `ApiPayloadOptions.Serializer`, and a factory switch had only one
  case, `messagepack`. ⚠️ **This half has been out of date since Bee.NET 4.27.0**: that setting was removed, the codec
  is now declared per request, and a JSON body codec exists (adr-044). The settings name no codec on purpose; the
  codec names live in `PayloadCodecNames`, and a declared name is resolved by `PayloadOptions.ResolveCodec` of
  Polhem.JsonRpc.Payload (since 1.2.0, ADR-049). **The conclusion above that "the dimensions are orthogonal" holds
  even more strongly now**: `PayloadFormat` governs encryption / compression and `codec` governs how the body is
  spelled, each on its own.
- `FormApiConnector` defaults to `Encrypted` and `Login` uses `Encoded` → the body of such a call goes through
  MessagePack **whenever the request declares no codec**, on both the client and the server (client =
  `Polhem.Api.Client`, which runs on the iOS/Android/WASM heads). A .NET client declares nothing unless
  `ApiConnector.PayloadCodec` is set.

**Conclusion: MessagePack really is on the mobile wire path.** The assumption "mobile uses JSON, MessagePack is only
between desktop/server" does not hold; this misunderstanding once led to hanging the AOT risk assessment on the wrong
engine.

## MessagePack item ctor parameter order ≠ `[Key]` order → fields silently swapped (resolved 2026-08-09, adr-036)

> **Historical record.** Since adr-036 there are no integer `[Key]`s anywhere in the repository: wire binding goes by
> property name or through a formatter that names each member, so this pitfall no longer exists. The current state is
> in `src/Polhem.Api.Core/CLAUDE.md` § "Constructor parameter order of collection items". The account below is kept
> because the symptom (only a MessagePack round-trip gives it away) is the reason wire round-trip tests exist.

**Symptom**: only a MessagePack wire round-trip gave it away; XML / JSON round-trips were correct (they go by property
name). So the definition file tests alone were all green, while the data that reached the client was wrong.

**Root cause at the time**: `CollectionBaseFormatter` serialized each item under the `[MessagePackObject]`+`[Key]`
contract. Deserialization picked "the constructor with the most parameters" and fed values to the ctor parameters
**by position in Key order** (position-based, not by name).

**Instance**: the Key order of `UnitItem` was Code(100)/Decimals(101)/Dimension(102)/Name(103), and the ctor was
originally `(code, decimals, name, dimension)` → the round-trip swapped Dimension / Name
(commit [`eb10bc0c`](https://github.com/jeff377/bee-library/commit/eb10bc0c) corrected it to `(code, decimals, dimension, name)`).

**What still applies**: add a MessagePack wire round-trip test for wire-carried items
(template: `UnitSettingsMessagePackTests`).

## `[Union]` polymorphism is incompatible with `keyAsPropertyName` (superseded 2026-08-09, adr-036)

> **Historical record.** The "permanent constraint" below no longer holds: there is no `[Union]` or `[Key]` anywhere
> in `src/`, and `FilterNode` polymorphism is handled by the hand-written `FilterNodeFormatter` with a named `Kind`
> discriminator. The status block of adr-030 records the same amendment. A new polymorphic wire hierarchy gets its
> own formatter registered in `WireContracts.*.cs` (see `.claude/rules/serialization.md`).

On 2026-07-22 the name-based migration was carried out: 72 types were converted to
`[MessagePackObject(keyAsPropertyName:true)]` (57 contracts + 15 DTOs/items), and the Definition serialization tests
(201) + Api.Core tests (237) all passed. The decision is in `maintainers/adr/adr-030-messagepack-name-based-keys.md`.

The go/no-go finally chose "do it now" because **there are no external consumers → a breaking change costs nothing**
(overturning the earlier decision to postpone). `keyAsPropertyName` was chosen over "simply removing the attributes"
because **keeping the attributes = keeping the door open for source generation** (adr-036 later removed the attributes
anyway).

The constraint at the time: `[Union]` uses an integer-keyed array + a discriminator, which is incompatible with
`keyAsPropertyName`, so `FilterNode` (+`FilterCondition` / `FilterGroup`) kept integer `[Key]`.

## AOT risk assessment: two guesses, both "overturned" by measurement, and both right in the end

These two are worth keeping because **each guess was dismissed by a measurement whose sample missed the path the guess
was about, and each came back as a production failure**: MessagePack broke the whole iOS wire, DynamicExpresso crashed
the iOS app. When a similar doubt comes up, measure first, and then ask what the measurement did not cover.

### MessagePack

**The guess at the time**: `MessagePackCodec` uses `ContractlessStandardResolver.Instance` +
`CompositeResolver.Create`, **both Reflection.Emit-based** with no reflection-only fallback; MessagePack's correct AOT
approach is the source generator (which needs `[MessagePackObject]` attributes) → an Encrypted form call on a physical
iOS device in Release AOT might throw `PlatformNotSupportedException` when producing a formatter.

**The measured result (Phase 0)**: reproducing the no-Emit path with the runtimeconfig `IsDynamicCodeSupported=false`,
three types (integer keys, integer keys + a collection, `keyAsPropertyName`) **all round-tripped normally with no
exception**.
→ The conclusion at the time: **MessagePack 3.x has a reflection-based fallback, and source generation is not a hard
prerequisite.**

**The real lesson of this one is "sample coverage" (correction of 2026-08-10)**: the three types above **all carried
the `[MessagePackObject]` attribute**. The measurement was not wrong; the mistake was generalizing the conclusion into
"MessagePack works under AOT". A NativeAOT control experiment showed:

| Case | Result |
|------|------|
| `[MessagePackObject(keyAsPropertyName: true)]` + `StandardResolver` | ✅ round-trip works |
| Unattributed POCO + `ContractlessStandardResolver` | ❌ `FormatterNotRegisteredException` |

**Contractless has no fallback.** The original guess (contractless is Emit-based, with no reflection-only fallback) was
actually right; that measurement just never tested contractless, so the guess was wrongly believed to be overturned.
After adr-036 the whole repository moved to contractless, and the iOS wire stopped working as a result; the accounting
is in "Open issues" in [adr-036](../adr/adr-036-wire-serialization-externalized.md), and the fix is in
[adr-037](../adr/adr-037-wire-explicit-registration.md).

**The takeaway**: when a measurement overturns a guess, first ask "does my sample cover the path the guess was about?"
Here the guess named contractless, but the samples were all attributed types.

### DynamicExpresso

**The guess at the time**: `Polhem.Expressions` uses `Expression.Compile()`, and iOS/WASM AOT forbid
`Reflection.Emit` → mobile needs a graceful degrade that disables live computation.

**The measured result (2026-07-09)**: `Expression.Compile()` automatically falls back to the **interpreter** when
`IsDynamicCodeSupported=false` (CoreCLR's built-in interpreter; Mono has one too). `Evaluate` (the computed field
`price*qty`), `Evaluate<bool>` (condition rules) and `GetReferencedVariables` were all correct. **No need to disable
it.**

The degrade mechanism of `FormLiveComputation.IsDegraded` protects against "syntax/identifier errors in customer-written
expressions" (`ExpressionEvaluationException`), so that they do not spread into the `FieldValueChanged` handler and
break the form. **It is unrelated to AOT**; do not treat it as an AOT remedy.

**Correction (2026-09-28): the conclusion "no need to disable it" was only true on CoreCLR.** Editing an order line in
the row-edit overlay of the Northwind iOS head (quantity 40 → 405, OK) terminated the app:

```
System.ExecutionEngineException: Attempting to JIT compile method '(wrapper dynamic-method)
object object:Thunk1ret_Object_Int32_Decimal_Decimal (System.Func`2<object[], object>,int,System.Decimal,System.Decimal)'
while running in aot-only mode.
   at System.Dynamic.Utils.DelegateHelpers.CreateObjectArrayDelegateRefEmit(Type , Func`2 )
   at System.Linq.Expressions.Interpreter.LightLambda.MakeDelegate(Type )
   at System.Linq.Expressions.LambdaExpression.Compile()
```

What actually happens: the interpreter fallback is real, but `LightLambda.MakeDelegate` must still return a delegate of
the lambda's **exact signature**, here `(int, decimal, decimal) → object` for `quantity * unit_price * (1 - discount)`.
`DelegateHelpers.CreateObjectArrayDelegateRefEmit` (the same code in the CoreCLR and Mono builds of
`System.Linq.Expressions`) uses a precompiled C# thunk (`FuncThunk1` / `FuncThunk2`) only for **at most two
parameters**, and emits a `DynamicMethod` otherwise. Each runtime then fails differently:

| Runtime | Result for DynamicExpresso's typed lambda (before the fix) |
|---------|------|
| CoreCLR, `-p:DynamicCodeSupport=false` | **Passes.** The emitted thunk runs under `ForceAllowDynamicCode`, and a JIT exists |
| NativeAOT (`osx-arm64` console) | Fails **every signature with a value type**, including one or zero parameters: `Expression<Func<decimal, decimal>>` "is missing native code or metadata". String-only signatures pass |
| Mono, iOS simulator and Mac Catalyst Release | Fails **every signature with more than two parameters**, value types or not (`int, decimal, string → bool` too): `ExecutionEngineException`. Up to two parameters passes, value types included |

The 2026-07-09 measurement ran only the first row, and its samples (`price*qty`, a condition) had two parameters or
fewer, so even Mono would have passed them.

**Fix**: `DynamicExpressoEvaluator` no longer calls `Lambda.Invoke` (which compiles the typed delegate lazily). It
wraps DynamicExpresso's parsed body in `Expression<Func<object?[], object?>>`, unboxing each used variable from its
array slot into a block variable, and compiles that. One reference-type parameter hits `FuncThunk1` on Mono, and a
closed generic type known at compile time has code under NativeAOT. The desktop keeps compiling to IL as before.

**Probe results** (the nine cases of the probe: the order-line expression, three- and four-variable arithmetic, a
three-variable rule, `Math.Round`, string functions, `DateOnly`, no variables, `Guid`):

| Runtime | Before | After |
|---------|--------|-------|
| NativeAOT console, `PublishAot=true`, `osx-arm64` | 8 of 9 fail | 9 of 9 pass |
| iOS simulator (iPhone 17), Release `iossimulator-arm64` | 3 of 9 fail (the three- and four-parameter ones) | 9 of 9 pass |
| Mac Catalyst, Release `maccatalyst-arm64` | the same 3 of 9 fail | 9 of 9 pass |

**The gate**: `InterpretedInvokerGateTests` builds the evaluator with its internal `preferInterpretation` switch, so the
desktop takes the same `LightLambda` → `DelegateHelpers` path as iOS, then checks that no compiled expression is a
delegate over a `DynamicMethod` (such a delegate has no declaring type). With the old code it fails exactly the three
cases the iOS simulator failed, with the same thunk names. A positive-control test compiles the typed three-parameter
lambda and asserts it **is** detected, so the gate notices if a future runtime changes how it picks thunks.

**The general lesson** is the one in the MessagePack section, and it is in `.claude/rules/apple-mobile-trim.md` now: the
desktop switch and NativeAOT are not stand-ins for Mono. A pass there does not mean iOS passes.

### Reproduction without a device (the tool shared by both measurements)

Add to the console project's csproj:

```xml
<RuntimeHostConfigurationOption
    Include="System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported"
    Value="false" Trim="false" />
```

Or call `AppContext.SetSwitch(...)` on the first line of the entry point (before any serializer). The desktop CLR then
takes the same reflection-only BCL path that iOS device AOT is locked to. **Whenever you doubt the AOT compatibility
of any serialization / computation engine, use this first; do not book a device.**

**Update (2026-08-10)**: no csproj change is needed; one command-line property is enough, and it goes through the same
path in the .NET SDK (`DynamicCodeSupport` is mapped to the `RuntimeHostConfigurationOption` above, which is how the
iOS SDK sets it):

```bash
dotnet test <test project> -c Release --settings .runsettings -p:DynamicCodeSupport=false
```

The two hard requirements for interpreting the result (the exception type cannot be used for diagnosis; Android cannot
verify this half; for really no Emit use NativeAOT) have been moved into `.claude/rules/apple-mobile-trim.md`. They are
always-loaded rules and are not repeated here.

## Expression engine pitfall 1: variable key casing (the first to blow up, the hardest to track down)

**Symptom**: on the client, "the field is not computed live" (no crash); on the server, saving returns JSON-RPC
**-32000**. The two symptoms look completely unrelated, but they have the same root cause.

**Root cause**: at the time `DataTableExtensions.AddColumn` stored column names in **uppercase**
(`fieldName.ToUpper()`); `FormExpressionCalculator.BuildVariables` at one point used `column.ColumnName` (uppercase
`QUANTITY`) as the variable key, but expressions refer to **the declared field names** (lowercase `quantity`), and
**DynamicExpresso identifiers are case-sensitive** → `UnknownIdentifierException` → wrapped as
`ExpressionEvaluationException`.

The front end and the back end share `BuildVariables`, so both were hit: the client's recompute was caught by
`RunGuarded` → latched off; the server's save had no guard → unhandled exception → -32000.

**Why CI did not catch it**: the Phase 1 tests all built DataTables by hand with **lowercase** column names, and never
tested the uppercase column names of a real wire/DataSet.

**Fix (commit [`96821c04`](https://github.com/jeff377/bee-library/commit/96821c04))**: use `FormField.FieldName` (the casing declared in the schema) as the variable key.
`DataRow` indexing and `Fields.Contains` are case-insensitive anyway, so writing back is unaffected.

**Since then**: ADR-029 changed `AddColumn` to store lowercase (`ToLowerInvariant`), which happens to match the
declared field names and hides the symptom of code that keys by `DataColumn.ColumnName`. The rule is unchanged:
key by the declared field name, decoupled from the stored casing. **Regression tests must build the DataTable with
column names whose casing differs from the declared field names** (under the current implementation, uppercase);
the hard rule is in `.claude/rules/serialization.md`.

## Expression engine pitfall 2: coercing string-typed Guid/Binary columns

**Symptom (two demos in a row blew up)**: ① `InvalidCastException` (the client crash window);
② after switching to `Guid.Parse` → `FormatException`, the client latched the preview off, and saving on the server
returned -32000.

**Root cause**: `ExpressionPolicy.CoerceValue` (shared by front end and back end) **cannot rely on
`Convert.ChangeType` alone**: `Guid` / `byte[]` are not `IConvertible`.

**Why only the client hits it and the back end does not**: the back-end DB returns a `Guid`-typed column directly; but
on the client, a GUID column that `GetData` reads back from SQLite is **String-typed** (see the SQLite entry in
[database.md](database.md)), and the wire round-trip keeps that column type.
`BuildVariables` coerces **every column of the row** (including Guid key columns such as `product_rowid` that the
expression never references). The second blow-up was caused by an **empty-string** Guid column (a detail line with no
product selected) → `Guid.Parse("")`.

**Fix (commit [`e2623195`](https://github.com/jeff377/bee-library/commit/e2623195))**: `Guid` → an empty/whitespace string returns `Guid.Empty`, otherwise `Guid.Parse`;
`byte[]` → an empty string returns an empty array, otherwise `FromBase64String`. This aligns with the "null/DBNull →
the type's default value" policy.

## Troubleshooting tips

- JSON-RPC **-32000 = an unhandled server exception** (not a business rule; a business interruption is -32099
  UserMessage). Outside development mode the message is masked as `Internal server error`, which hides the real cause.
- On the client, "all live computation suddenly stops" → check `FormLiveComputation.IsDegraded` first (an evaluation /
  coerce failure latches off **the whole session**, so the symptom is "nothing moves", not "one cell does not move").
- The failure mode of definition responses on the MessagePack wire is a **silent empty shell**: no exception, scalar
  fields intact, nested collections emptied. External probing goes through the public `MessagePackPayloadCodec`
  (`MessagePackCodec` is internal; test projects rely on `InternalsVisibleTo`).
