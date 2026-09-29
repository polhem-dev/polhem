# ADR-011: Adopt DI to replace the static Service Locator

## Status

Accepted (2026-05-13)

Supersedes [ADR-003](adr-003-static-service-locator.md).

## Context

The premises of ADR-003 (use a static Service Locator) no longer apply:

- The framework has moved from netstandard2.0 to **net10.0** and can use `Microsoft.Extensions.DependencyInjection`
  directly; there is no longer any need to avoid a DI container to stay compatible across targets
- The test isolation cost accumulated by the static facades exceeds the original benefit of "simpler
  initialization": an inventory at the time showed that the BackendInfo family of static classes was referenced
  by **159 tests**, which needed several mechanisms such as `GlobalFixture` / `TempDefinePath` /
  `[Collection("Initialize")]` to stay isolated, and process-wide static races still occurred frequently
- BOs have implicit dependencies (a caller's dependencies are not declared explicitly in its constructor), are
  sensitive to initialization order (a violation is only discovered at runtime), and do not follow modern .NET
  conventions

## Decision

Adopt constructor injection (ctor injection) throughout; `IServiceCollection.AddPolhemFramework(BackendConfiguration,
PathOptions)` is the entry point for registering framework services, provided by the `Polhem.Hosting` package (from
4.3; up to 4.2 it was provided by `Polhem.Api.AspNetCore`). `Polhem.Api.AspNetCore` remains responsible for the
ASP.NET Core integration (`UsePolhemFramework` and `ApiServiceController`).

The design scope, invariants and design principles are in the "Decision" and "Consequences" sections below.

## Rationale

- **Visible dependency declarations**: every service dependency appears in the constructor's parameter list; visible
  at compile time, statically analyzable by the IDE, and easy to review
- **Testability**: tests inject mocks directly with `new BO(testCtx, ...)`, with no process-wide reset mechanism; test
  fixtures become a per-class `IServiceProvider`, and xUnit parallelism is restored
- **Clear lifetime semantics**: Singleton / Scoped / Transient map onto the DI container, and the per-request scope
  boundary is clear (previously a mix of "thread context + static services")
- **Startup validation with the Options pattern**: `IValidateOptions<T>` fails earlier than an exception thrown
  quietly inside `BackendInfo.Initialize`
- **Services stay replaceable**: `BackendComponents` in `SystemSettings.xml` still declares the concrete type name for
  each replaceable interface; `AddPolhemFramework` reads it and registers the configured types in the DI container

## Trade-offs

- **The local mode (`Polhem.Api.Client` in-process) has to deal with the ServiceProvider injection point**: when
  back-end logic is called inside a client process, a static holder, `ApiClientInfo.LocalServiceProvider`, is kept
  for now as a transition. How to apply this ADR's DI registration logic will be decided when `Polhem.Api.Client` is
  refactored later
- **BO subclasses still need zero DI registration**: an ERP application will have thousands of `FormBusinessObject`
  subclasses, dispatched from the progId XML table; application developers writing a new BO should not have to touch
  the DI API. Instead, `IPolhemContext` aggregates the core services every BO needs, and the factory constructs the
  BO with `ActivatorUtilities.CreateInstance(sp, boType, accessToken, progId, isLocalCall)`
- **The migration is a v5.0 breaking change**: it takes the full DI path, leaves no `[Obsolete]` transition layer,
  and introduces no dual constructors or compatibility adapters. Each phase removes every reference to that layer's
  static facades within a single PR

## Consequences

### Static facades removed (across all of Polhem)

| Class | Original package | Role |
|-------|------------------|------|
| `BackendInfo` | Polhem.Definition | The global entry point for 8 services + encryption keys + configuration values |
| `RepositoryInfo` | Polhem.Repository.Abstractions | The global entry point for the Repository provider |
| `CacheFunc` / `CacheContainer` | Polhem.ObjectCaching | The cache operation facade and the cache singletons |
| `DefinePathInfo` | Polhem.Definition | The global entry point for definition file paths |
| `DbConnectionManager` | Polhem.Db | The static facade for database connection information |

### Process-wide statics kept (registry-style, written once, no effect on concurrency)

- `SysInfo` — process-wide version, debug flag and the type namespaces allowed on the wire (written once)
- `CacheInfo.Provider` — the cache backend (set once per host)
- `DbProviderRegistry` — the registry of ADO.NET `DbProviderFactory`s
- `DbDialectRegistry` — the registry of the framework's `IDialectFactory`s
- `ApiServiceOptions` — global configuration of the API serialization / compression / encryption components (set
  once per host)

> **Addendum (2026-07-28)**: `ApiServiceOptions` was originally listed in the "Static facades removed" table above,
> but in the implementation it was not removed along with v5.0, so this document and the code disagreed for a long
> time. After review it was **confirmed as kept** and moved into this section: unlike the `BackendInfo` family, it
> holds no per-session state and is not a source of test isolation cost; in shape it is a registry-style,
> written-once configuration, of the same kind as `CacheInfo.Provider`.
> The only concurrency risk is rewriting it in tests, which is serialized by `[Collection("ApiServiceOptionsState")]`.
- `ApiClientInfo.LocalServiceProvider` — the transitional holder for the local mode of `Polhem.Api.Client` (to be
  handled by a later ADR)

### Back-end host startup flow

```text
1. paths = new PathOptions { DefinePath = "..." }
2. settings = SystemSettingsLoader.Load(paths)
3. SysInfo.Initialize(settings.CommonConfiguration)
4. services.AddPolhemFramework(settings.BackendConfiguration, paths)
5. provider = services.BuildServiceProvider()
6. app.UsePolhemFramework()   // ASP.NET only — startup checks (warns when the API key gate is not in effect)
```

For the full reference see
[docs/en/development-cookbook.md § Framework Initialization Order](../../docs/en/development-cookbook.md#framework-initialization-order).

### Test infrastructure

- `[Collection("Initialize")]` / `GlobalFixture` / `PolhemTestServices` / `TempDefinePath` are all removed
- Replaced by `IClassFixture<PolhemTestFixture>` (a per-class `IServiceProvider`) + `SharedDbFixture` (process-wide
  shared DB schema/seed)
- xUnit parallelism restored: a local wall-clock parallel speedup of about 2.7x (2749 tests)

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **2026-09-27: how a BO is constructed.** `BusinessObjectFactory.CreateBusinessObject`
  (`src/Polhem.Business/BusinessObjectFactory.cs`) builds the BO with
  `Activator.CreateInstance(type, ctx, accessToken, progId, isLocalCall)`: the context is passed explicitly, so a BO
  constructor cannot ask the container for further services. `IPolhemContext` has been renamed
  `IBusinessObjectContext`.
- **2026-09-27: the local-mode holder.** `ApiClientInfo.LocalServiceProvider` has been removed. `LocalApiProvider` and
  the local connector constructors take the `IServiceProvider`, and the native UI heads keep it on
  `ClientInfo.LocalServiceProvider` (`src/Polhem.UI.Core/ClientInfo.cs`).
- **2026-09-27: test parallelism.** Several test assemblies disable parallelization for the whole assembly again,
  because their tests share process-wide state such as `ApiServiceOptions` (for example `Polhem.Api.Core.UnitTests`
  and `Polhem.ObjectCaching.UnitTests`; each states its reason next to its `CollectionBehavior` attribute). In
  `Polhem.Api.Core.UnitTests` the `ApiServiceOptionsState` collection named in the addendum is kept only as a marker.
  The other test assemblies still run in parallel.

## Implementation references

| Document | Content |
|----------|---------|
| [docs/en/development-cookbook.md](../../docs/en/development-cookbook.md) | The initialization flow and request pipeline after the move to DI |
| [docs/en/development-constraints.md](../../docs/en/development-constraints.md) | Initialization order constraints (DI model) |
