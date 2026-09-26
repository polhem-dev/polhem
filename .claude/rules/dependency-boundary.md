# Dependency boundary rules (`Polhem.Base` / `Polhem.Definition`)

## `Polhem.Base` and `Polhem.Definition` are the two lowest assemblies

**Unless it is necessary, do not add any further package reference (`PackageReference`) to these two projects.**

They are the bottom of the framework's dependency graph: the direct downstream of `Polhem.Definition` spans the
contracts, data access, caching, business logic, API and UI layers, and `Polhem.Base` is a dependency of **every**
project. **Any package added to these two layers spreads along the dependency chain to every consumer**, including
pure UI heads and definition file tools that only want to read definitions.

Likewise, **a `ProjectReference` to a downstream project counts too**: `Polhem.Definition` once transitively depended
on `DynamicExpresso.Core` through `ProjectReference → Polhem.Expressions` (resolved by adr-038). The visible symptom
was an extra dependency in the nuspec, but grepping for the package name never finds it.

## Where "necessary" ends

| Category | Example | Allowed? |
|------|-----|------|
| BCL / platform vocabulary | `System.Xml.Serialization`, `[JsonIgnore]` | ✅ Yes |
| Microsoft first-party, **pure abstraction**, versioned with .NET | `Microsoft.Extensions.Localization.Abstractions` | ✅ Yes |
| Third-party package | `DynamicExpresso.Core`, `MessagePack`, `Newtonsoft.Json` | ❌ No |
| First-party **implementation** package (carries a concrete implementation, own release cadence) | — | ❌ No, treated like third-party |

Test: **does this dependency make a technology choice on the consumer's behalf?** A pure abstraction package does
not: it carries no implementation and does not lock in an engine.
For the full criteria and reasoning see [adr-038](../../docs/adr/adr-038-definition-dependency-boundary.md)
and [adr-036](../../docs/adr/adr-036-wire-serialization-externalized.md).

## The correct approach when you want to add something

Abstraction and implementation live in two layers: **the abstraction sinks down, the implementation stays up**.

- If the abstraction uses only BCL types → put it in `Polhem.Base` (for example
  `Polhem.Base.Expressions.IExpressionEvaluator`).
- An implementation that brings a third-party package → stays in its own assembly (for example
  `Polhem.Expressions.DynamicExpressoEvaluator`), and the **composition layer** (`Polhem.Hosting`, each UI head)
  decides which implementation to use.

`Polhem.Base` is not a junk drawer either: only move in abstractions that have "zero external dependencies and are
shared by several layers".

## Two gates (complementary, not duplicates)

| Gate | Location | When it goes red |
|------|------|--------|
| **Build-time lock** `POLHEM9001` | `src/Directory.Build.targets` | The moment you write it, `dotnet build` fails |
| **Transitive closure test** | `tests/Polhem.Definition.UnitTests/DefinitionDependencyGateTests.cs` | When tests run |

**Both are needed**, because the build-time lock cannot see "a **package** brought in indirectly through some
`ProjectReference`" (that is exactly how DynamicExpresso got in), while the closure test only tells you when tests
run.

The list of governed projects and the allowlist are both in `src/Directory.Build.targets`
(`PolhemAllowedDependency`); this file does not copy them. **You do not need to memorize the steps for allowing
something either**: the `POLHEM9001` error message itself lists the three places ("add to `PolhemAllowedDependency`,
add to the test allowlist, record it in adr-038"), so you see them when you hit it.
**This friction is deliberate**: the goal is to force a decision, not to let it pass silently. First ask once more
about "the correct approach" in the previous section.

> **Correction (measured 2026-08-11): the build-time lock can see transitive *project references*; it only cannot
> see transitive *packages*.** The .NET SDK's `IncludeTransitiveProjectReferences` merges transitive project
> references into `@(ProjectReference)` before Build. Evidence: the csproj of `Polhem.Api.Contracts` lists only
> `Polhem.Definition`, yet as soon as the gate was enabled it reported POLHEM9001 for `Polhem.Base`. Therefore
> **the allowlist must list the whole project reference closure**, not only the entries written in the csproj.

The build-time lock checks only references that **flow to consumers**: packages with `PrivateAssets="all"`
(SourceLink, analyzers) and project references with `ReferenceOutputAssembly="false"` do not count, because they do
not appear in the nuspec.
