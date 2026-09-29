# Polhem.Expressions

[繁體中文](README.zh-TW.md)

The DynamicExpresso-backed implementation of the framework's expression engine. Shared by the
business layer (field computation and rule validation before save) and by UI clients (live preview
while typing), so a computed field yields the same result on both sides.

## Key Public APIs

| Type | Purpose |
|------|---------|
| `DynamicExpressoEvaluator` | The default `IExpressionEvaluator`. Parses and compiles once, caches by expression text plus parameter signature, then invokes per row |

## The abstraction lives in `Polhem.Base`

`IExpressionEvaluator`, `ExpressionPolicy` and `ExpressionEvaluationException` are in
`Polhem.Base.Expressions`, not here. That split keeps `Polhem.Definition` and `Polhem.Business` free of any
DynamicExpresso dependency — they consume the engine through the abstraction, and only a
composition root (`Polhem.Hosting`, or a UI head building its own evaluator) references this package.
See [ADR-038](../../maintainers/adr/adr-038-definition-dependency-boundary.md).

**Reference this package when you need to pick an implementation. Reference `Polhem.Base` when you
only need to accept one.**

## Time zone

`Evaluate` takes a `timeZoneId` and a `DateTimeBasis`. `Today()` returns the calendar day (`DateOnly`) in the
user's zone, so a row created from another region still defaults to the user's own day. `Now()` follows the
basis of the data set being evaluated: the user's zone for `DateTimeBasis.UserZone` (the default, a client
preview) and UTC for `DateTimeBasis.Utc` (the server's pre-save pass, where the stored values are UTC).
`UtcNow()` states UTC outright. An empty zone id means UTC. See
[ADR-032](../../maintainers/adr/adr-032-datetime-timezone.md).

## Security

**This is not a sandbox.** Unregistered *type names* (`File`, `Assembly`, `Process`) fail at parse
time, but member access on a value is resolved by reflection, and `GetType()` is a public member of
`object` — any variable in scope is a starting point into the reflection API. Upstream DynamicExpresso
states the same limitation.

What keeps this safe is the *source* of the expressions, not the parser: expressions live in
definition files, and writing a definition is a deployment-time operation (`SystemBusinessObject.SaveDefine` is
declared `ApiProtectionLevel.LocalOnly`). Any change that would let a remote or lower-privileged caller supply expression text
turns this into remote code execution on the server — that boundary is the control.

## AOT / trimming

`Expression.Compile` falls back to the interpreter when `IsDynamicCodeSupported` is false, but it still
has to create a delegate of the compiled lambda's signature, and a runtime without a JIT cannot create
every signature: on iOS a lambda with more than two parameters fails with `ExecutionEngineException`, and
under NativeAOT one with a value-type parameter has no code. `DynamicExpressoEvaluator` therefore compiles
every expression to the same delegate, `Func<object?[], object?>`, which both runtimes can create, so the
engine works on iOS, Android and WASM without disabling anything. A test that forces the interpreter on the
desktop fails if an expression is compiled to any other shape.

If you evaluate expressions with DynamicExpresso directly rather than through `IExpressionEvaluator`, the
same limit applies to your code: `Lambda.Invoke` and `Lambda.Compile` build the typed delegate.

Trimming is a separate matter: DynamicExpresso finds `Math.*`, `string.*` and the other members an expression
names by reflection. The package ships an embedded `ILLink.Descriptors.xml` that keeps the types expressions
can reach, so a trimmed head (the default partial trim of mobile builds) keeps them without further
configuration.

## Dependencies

`Polhem.Base` (for the `IExpressionEvaluator` abstraction it implements) · DynamicExpresso
