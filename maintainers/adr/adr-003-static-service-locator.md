# ADR-003: Use a static Service Locator instead of dependency injection

## Status

**Superseded by [ADR-011](adr-011-di-replaces-service-locator.md) (2026-05)** — the framework has switched entirely to
DI constructor injection. This ADR is kept as a record of the historical decision.

## Original status

Accepted

## Context

The framework needs a mechanism that lets every layer reach shared providers (such as BusinessObjectProvider,
RepositoryProvider and DefineAccess). The common options:

1. **Dependency injection (DI)**: injected through the constructor or a property, with lifetimes managed by a DI
   container
2. **Static Service Locator**: a global access point provided through static classes

## Decision

Use the static Service Locator pattern, providing global access through static classes such as `BackendInfo`,
`RepositoryInfo`, `CacheFunc` and `ApiServiceOptions`.

## Rationale

- **Across host environments**: the framework must support several host environments at once: ASP.NET Core (with
  built-in DI), WinForms, console apps, Blazor and others. Static access does not depend on any particular DI
  container and is the same in every environment.
- **Historical compatibility**: the framework originally targeted netstandard2.0 and could not depend on
  `Microsoft.Extensions.DependencyInjection`. It now targets net10.0, but this pattern is already the established
  convention.
- **Simpler initialization**: at application startup the static properties only need to be set in order; there is no
  need to build a complex ServiceCollection registration flow.
- **Deterministic initialization**: static constructors guarantee that a provider is initialized on first access,
  avoiding an unclear resolution order in a DI container.
- **Established convention**: the framework has carried this pattern over from the .NET Framework era, and users in
  non-DI environments such as WinForms are used to this API.

## Trade-offs

- **Hard to test**: static state is hard to isolate between tests and needs an extra reset mechanism.
- **Implicit dependencies**: a caller's dependencies are not declared explicitly in its constructor, so they are hard
  to see when reading the code.
- **Sensitive to initialization order**: the initialization order must be followed strictly (see
  `docs/en/development-constraints.md`), and a violation is only discovered at runtime.
- **Not in line with modern .NET conventions**: new .NET projects generally use DI.

## Consequences

- `BackendInfo` (Polhem.Definition): the global entry point for providers and security keys
- `RepositoryInfo` (Polhem.Repository.Abstractions): the global entry point for the Repository provider
- `CacheFunc` (Polhem.ObjectCaching): the global facade for cache operations
- `ApiServiceOptions` (Polhem.Api.Core): the global configuration of the API serialization / compression /
  encryption components
- `ApiClientInfo` (Polhem.Api.Client): the global entry point for client connection configuration
- The initialization order is documented in `docs/en/development-constraints.md` and
  `docs/en/development-cookbook.md`
