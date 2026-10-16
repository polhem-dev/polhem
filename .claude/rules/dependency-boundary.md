# Dependency boundary rules (the foundation assemblies and the front-end layer)

## Three foundation assemblies: `Polhem.Core`, `Polhem.Definition`, `Polhem.Api.Contracts`

**Both the front end and the back end reference these three.** So they may hold nothing that belongs to one side only:
nothing server-side (database, MessagePack, JSON-RPC, ASP.NET) and nothing front-end (a UI framework, local file
storage, user state). `Polhem.Core` and `Polhem.Definition` are also the two lowest assemblies; `Polhem.Api.Contracts`
sits above `Polhem.Definition` but is inherited the same way. The reasons are in
[adr-054](../../maintainers/adr/adr-054-front-end-decoupled-from-api-client.md).

**Unless it is necessary, do not add any further package reference (`PackageReference`) to these projects.**

They are the bottom of the framework's dependency graph: the direct downstream of `Polhem.Definition` spans the
contracts, data access, caching, business logic, API and UI layers, and `Polhem.Core` is a dependency of **every**
project. **Any package added to these two layers spreads along the dependency chain to every consumer**, including
pure UI heads and definition file tools that only want to read definitions.

Which projects the gates govern is set in `src/Directory.Build.targets` (`PolhemEnforceDependencyBoundary`), not
here.

## The front-end layer never carries the transport

`Polhem.UI.Core` and `Polhem.UI.Avalonia` reach the backend only through the contracts in `Polhem.Api.Contracts`
(`IFormApiConnector` and the response interfaces), and the services they need are injected
(`Polhem.UI.Core.UIServices`). **They must not reference `Polhem.Api.Client`, `Polhem.Api.Core` or
`Polhem.UI.ApiBridge`**: those bring MessagePack and the JSON-RPC packages into every front end. A head that has a
backend references `Polhem.UI.ApiBridge` itself. The same lock (`POLHEM9001`) governs both projects, and
`FrontEndDependencyGateTests` (tests/Polhem.UI.Avalonia.UnitTests) checks their transitive closure. Do not let the
connector reference the UI either: joining the two is the bridge's job.

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
For the full criteria and reasoning see [adr-038](../../maintainers/adr/adr-038-definition-dependency-boundary.md)
and [adr-036](../../maintainers/adr/adr-036-wire-serialization-externalized.md).

## The correct approach when you want to add something

Abstraction and implementation live in two layers: **the abstraction sinks down, the implementation stays up**.

- If the abstraction uses only BCL types → put it in `Polhem.Core` (for example
  `Polhem.Core.Expressions.IExpressionEvaluator`).
- An implementation that brings a third-party package → stays in its own assembly (for example
  `Polhem.Expressions.DynamicExpressoEvaluator`), and the **composition layer** (`Polhem.Hosting`, each UI head)
  decides which implementation to use.

`Polhem.Core` is not a junk drawer either: only move in abstractions that have "zero external dependencies and are
shared by several layers".

## Two gates (complementary, not duplicates)

| Gate | Location | When it goes red |
|------|------|--------|
| **Build-time lock** `POLHEM9001` | `src/Directory.Build.targets` | The moment you write it, `dotnet build` fails |
| **Transitive closure test** | `tests/Polhem.Definition.UnitTests/DefinitionDependencyGateTests.cs`; for the front-end layer `tests/Polhem.UI.Avalonia.UnitTests/FrontEndDependencyGateTests.cs` | When tests run |

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
> `Polhem.Definition`, yet as soon as the gate was enabled it reported POLHEM9001 for `Polhem.Core`. Therefore
> **the allowlist must list the whole project reference closure**, not only the entries written in the csproj.

The build-time lock checks only references that **flow to consumers**: packages with `PrivateAssets="all"`
(SourceLink, analyzers) and project references with `ReferenceOutputAssembly="false"` do not count, because they do
not appear in the nuspec.
