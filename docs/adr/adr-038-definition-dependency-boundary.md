# ADR-038: The definition layer's dependency boundary: the expression abstraction moves down to `Polhem.Base`, and the criterion is enforced by gates

[繁體中文](adr-038-definition-dependency-boundary.zh-TW.md)

## Status

**Accepted (2026-08-11)**: the decision has been carried out.

This ADR continues the criterion of [ADR-036](adr-036-wire-serialization-externalized.md) and adds the definition of
its boundary. It also revises the assembly layout in [ADR-028](adr-028-expression-rule-engine.md) where "the
abstraction and the implementation live together in `Polhem.Expressions`" (the conclusions about evaluation semantics
and a single implementation shared by client and server are unchanged).

## Context

When ADR-036 moved MessagePack out of the definition layer, it set this criterion:

> The criterion is not "is it a transport format" but **"would it give the definition layer an external package
> dependency"**.

At the time, the criterion was applied by a human grepping for keywords related to "transport format", so it missed
another dependency chain:

```
Polhem.Definition ──ProjectReference──> Polhem.Expressions ──PackageReference──> DynamicExpresso.Core
```

As a result, the nuspec of the definition package (Bee.NET 4.19.0 at the time) listed the expressions package as a
dependency, and **every consumer that
installs `Polhem.Definition` was pulled into an expression engine**, including pure UI heads and definition file tools
that only want to read definitions. After scanning all 17 projects under `src/`, this was the only violation.

The cause was not an architectural misplacement but **the abstraction and the implementation living in the same
assembly**: of the four public types in `Polhem.Expressions`, only `DynamicExpressoEvaluator` actually touches
DynamicExpresso; the public surface of the other three consists only of BCL types and `Polhem.Base.Data.FieldDbType`.

## Decision

### 1. The abstraction moves down to `Polhem.Base`; the implementation stays in `Polhem.Expressions`

| Type | New location | Reason |
|------|--------|------|
| `IExpressionEvaluator` | `Polhem.Base.Expressions` | An abstraction; its public surface has zero external types |
| `ExpressionEvaluationException` | `Polhem.Base.Expressions` | Part of the interface contract (the interface documents it with `<exception cref>`) |
| `ExpressionPolicy` | `Polhem.Base.Expressions` | Pure policy; depends only on `Polhem.Base.Data.FieldDbType` |
| `DynamicExpressoEvaluator` | Stays in `Polhem.Expressions` | The **only** type that touches DynamicExpresso |

`ExpressionEvaluationException` goes in `Polhem.Base.Expressions` rather than `Polhem.Base.Exceptions`: its cohesion
with the interface is stronger than "keep all exceptions together", and the existing members of
`Polhem.Base.Exceptions` (`ForbiddenException` / `UserMessageException`) are general-purpose cross-domain exceptions,
which are of a different nature.

The dependency chain changes accordingly:

| Project | Reference to `Polhem.Expressions` | Reason |
|------|---------------------------|------|
| `Polhem.Definition` | ❌ Removed | Uses only the abstraction (`FormExpressionCalculator` takes the evaluator through constructor injection) |
| `Polhem.Business` | ❌ Removed | Same as above (`FormRuleProcessor`) |
| `Polhem.Hosting` | ✅ Kept | The composition layer; DI registration has to name a concrete evaluator |
| `Polhem.UI.Avalonia` | ✅ Kept | The client builds its own evaluator for live preview |

**`FormExpressionCalculator` does not move out of the definition layer**: `FormSchema` declares `ValueExpression` and
validation rules, so evaluation is part of the definition's semantics; and the server before saving and the client's
live preview share the same implementation, which is exactly where ADR-028's guarantee that "the value the client
computes equals the value the server writes" comes from. Moving it would break that link.

### 2. The boundary of "external package"

ADR-036's criterion only says "external package dependency" and does not define the boundary. This ADR adds it:

| Category | Example | May the definition layer depend on it? |
|------|-----|--------------|
| BCL / platform vocabulary | `System.Xml.Serialization`, `[JsonIgnore]` | ✅ Yes |
| **Microsoft first-party, pure abstraction, versioned with .NET** | `Microsoft.Extensions.Localization.Abstractions` | ✅ Yes |
| Third-party package | `DynamicExpresso.Core`, `MessagePack` | ❌ No |
| First-party **implementation** package | Carries a concrete implementation and has its own release cadence | ❌ No (treated like third-party) |

To decide: **does this dependency make a technology choice on the consumer's behalf?** A pure abstraction package
does not: it carries no implementation, does not lock in an engine, and its version follows .NET rather than a vendor.
`Microsoft.Extensions.Localization.Abstractions` (used by `Language/PolhemStringLocalizer.cs`) therefore stays in the
definition layer.

### 3. The criterion is now enforced by gates

A criterion written only in an ADR gets missed, as it did this time, so it is enforced by two complementary gates:

| Gate | Location | Coverage |
|------|------|---------|
| **Build-time lock** | `src/Directory.Build.targets` (diagnostic `POLHEM9001`) | `PackageReference` / `ProjectReference` declared **directly** by a locked assembly (the list is in the `PolhemEnforceDependencyBoundary` condition in that file and is not copied here) |
| **Transitive closure test** | `tests/Polhem.Definition.UnitTests/DefinitionDependencyGateTests.cs` | **The whole transitive dependency closure** of `Polhem.Definition` |

Both are needed, because each cannot see the other's half: the build-time lock cannot see "a package brought in
indirectly through some `ProjectReference`", which is exactly how DynamicExpresso got in; the closure test only tells
you when tests run, not at the moment you write the csproj.

The build-time lock checks only references that **flow to consumers**: packages with `PrivateAssets="all"`
(SourceLink, analyzers) and project references with `ReferenceOutputAssembly="false"` (the build ordering of
`Polhem.Analyzers`) do not count, because they do not appear in the nuspec.

The closure test reads the test assembly's own `.deps.json` and does a BFS from the `Polhem.Definition` node. It uses
`deps.json` rather than `Assembly.GetReferencedAssemblies()` because the latter only reflects assemblies "actually
referenced by IL": a package dependency that is declared but not yet used would be missed, and that is exactly what
this gate is meant to stop.

**Allowing a new dependency requires changes in three places** (`PolhemAllowedDependency`, the test allowlist, and the
reasoning in this ADR), **forcing a decision instead of letting it pass silently**.

## Rationale

### Why not "add a `Polhem.Expressions.Abstractions` package"

Splitting abstraction and implementation into separate packages is the more "orthodox" approach, but it means shipping
one more NuGet package for three types (the framework already has 17), and one more name for consumers to learn.
`Polhem.Base` is already the layer the whole framework depends on, so putting the abstraction there **costs nobody
anything extra** and adds no package. A separate package would only pay off if `Polhem.Base` itself needs slimming
later.

### Why not let the definition layer declare a tiny interface of its own

That would avoid touching `Polhem.Expressions`, but it would produce two parallel interfaces plus an adapter, which in
the long run is messier than moving the abstraction down.

### It can be moved because the abstraction surface is clean

`grep DynamicExpresso src/Polhem.Expressions/*.cs` has zero hits apart from `DynamicExpressoEvaluator.cs`. This is a
mechanical move, not a redesign: not one line of the evaluation logic changed.

## Consequences

### Breaking change (source-breaking)

Three public types change namespace, and the parameter types of three public constructors change with them:

| Member | Change |
|------|------|
| `Polhem.Expressions.IExpressionEvaluator` | → `Polhem.Base.Expressions.IExpressionEvaluator` |
| `Polhem.Expressions.ExpressionEvaluationException` | → `Polhem.Base.Expressions.ExpressionEvaluationException` |
| `Polhem.Expressions.ExpressionPolicy` | → `Polhem.Base.Expressions.ExpressionPolicy` |
| `FormExpressionCalculator(IExpressionEvaluator)` | Parameter type changes namespace |
| `FormRuleProcessor(IExpressionEvaluator)` | Parameter type changes namespace |
| `FormLiveComputation(FormSchema, RoundingContext?, IExpressionEvaluator?)` | Parameter type changes namespace |

The type names and member signatures themselves are unchanged, so the change for external consumers is mechanical
(change the `using`). The framework is pre-stable (v4.x); changes of this kind are allowed but must be listed in the
CHANGELOG.

### Zero behavior change

A pure move: evaluation logic, rounding policy and time zone handling are all untouched.

### The `RS0026` exception

Both `Evaluate` overloads of `IExpressionEvaluator` take the optional parameter `timeZoneId`, and in `Polhem.Base`
they count as "new API", so they trigger `RS0026: Do not add multiple overloads with optional parameters`. Both places
are marked with `[SuppressMessage]` and a justification: this pair of overloads is **not** new API, the two have always
come and gone together as a set, and there is no risk of "a caller being silently rebound to the other overload",
which is exactly what RS0026 guards against.

### Verification

| Item | Result |
|------|------|
| `dotnet list src/Polhem.Definition package --include-transitive` | `DynamicExpresso.Core` no longer appears |
| `Polhem.Definition.nuspec` dependencies | Only `Polhem.Base` + `Microsoft.Extensions.Localization.Abstractions` remain |
| Transitive closure test | Red before the move (catches `Polhem.Expressions, DynamicExpresso.Core`), green after the move; red again after deliberately injecting `MessagePack` (listing its transitive dependencies `MessagePack.Annotations` / `Microsoft.NET.StringTools` as well) |
| Build-time lock | Injecting `MessagePack` once into `Polhem.Base` and once into `Polhem.Definition` stops the build with `POLHEM9001` in both; 0 warnings / 0 errors after reverting |
| clean Release build | 0 warnings / 0 errors |
| Full unit test suite | All 16 test projects green |

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **The build-time lock sees transitive project references; it misses only transitive packages.** The gate table says
  the lock checks references declared **directly**. Measured on 2026-08-11: the .NET SDK folds transitive project
  references into `@(ProjectReference)` before Build, so the lock sees every project in the reference closure, and the
  allowlist in `src/Directory.Build.targets` has to name that whole closure, not only what a csproj writes down. What
  the lock still cannot see is a package that arrives through a project reference, which is the half the closure test
  covers; the reason both gates are needed is unchanged.
- **The lock and the closure test cover `Polhem.Api.Contracts` too.** It sits in the transitive closure of every UI
  head, so the same argument applies. The locked assemblies are `Polhem.Base`, `Polhem.Definition` and
  `Polhem.Api.Contracts` (the `PolhemEnforceDependencyBoundary` condition), and `DefinitionDependencyGateTests` runs
  its closure check once for each of them, each against its own allowlist.
- **2026-09-27: the localizer that uses `Microsoft.Extensions.Localization.Abstractions` was renamed.**
  `Language/PolhemStringLocalizer.cs` is now `src/Polhem.Definition/Language/LanguageResourceStringLocalizer.cs`; the
  allowed dependency and the reason for allowing it are unchanged.
