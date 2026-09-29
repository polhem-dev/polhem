# ADR-006: Dual target framework strategy (netstandard2.0 + net10.0)

## Status

**Superseded** — this ADR's decision was "the core packages target both `netstandard2.0` and `net10.0`".
That decision no longer holds: on 2026-04-14 everything moved to the single target framework `net10.0`.

The text below keeps the context and reasons at the time of the decision, to explain "why dual targeting was chosen
back then"; **it does not describe the current state**.

### Original status

Accepted

## Context

The framework needs to decide the target frameworks of its NuGet packages. The options:

1. **netstandard2.0 only**: maximum compatibility, but newer APIs cannot be used
2. **net10.0 only**: the latest features and performance, but it limits users' environments
3. **Dual targeting (netstandard2.0 + net10.0)**: both, at a higher maintenance cost

## Decision

For now the core packages target both `netstandard2.0` and `net10.0`. The API hosting package
(Polhem.Api.AspNetCore) targets `net10.0` only.

## Historical reasons

- **Support for .NET Framework**: early versions had to support WinForms / console applications running on .NET
  Framework 4.7.2+, so netstandard2.0 was used to ensure compatibility.
- **Optimization on newer versions**: the net10.0 target allows modern .NET features such as `Span<T>`, the new
  cryptography APIs and performance improvements, with conditional compilation (`#if NETSTANDARD2_0`) providing the
  best implementation for each framework.
- **Automatic selection by NuGet**: when users install a NuGet package, NuGet automatically picks the best matching
  target framework.

## Trade-offs

- **Higher maintenance cost**: conditional compilation blocks (`#if`) add code complexity, and testing has to be done
  separately on both frameworks.
- **API limits**: APIs available only on .NET 5+ (such as `System.Half` and the new `Span` overloads) cannot be used
  in the netstandard2.0 target.
- **Longer build times**: every build compiles two target frameworks.

## Future direction

It has been confirmed that everything can move to **net10.0+**, dropping netstandard2.0 support.

### Analysis of the front-end consumers

The framework has three front-end repositories that consume Polhem's NuGet packages, and each front end generates its
interface dynamically from FormLayout:

| Front end | Target framework | Needs netstandard2.0? |
|-----------|------------------|-----------------------|
| **WinForms** (desktop) | net10.0+ (the new .NET WinForms) | No |
| **Web** (ASP.NET Core / Blazor) | net10.0+ | No |
| **APP** (MAUI mobile devices) | net10.0+ | No |

**Conclusion: netstandard2.0 has no consumers**; every front end is net10.0+.

### Benefits of moving to net10.0+

- **Remove conditional compilation**: eliminate every `#if NETSTANDARD2_0` block and simplify the code
- **Use modern APIs**: freely use new features such as Span, the new cryptography APIs and Generic Math
- **Lower maintenance cost**: no need to test separately on two frameworks
- **Pave the way for the STJ migration**: System.Text.Json is fully featured on net10.0 (see
  [ADR-002](adr-002-newtonsoft-json.md))

## Consequences

- Every project's `.csproj` is unified on `<TargetFramework>net10.0</TargetFramework>`
- `Polhem.Base/Security/PasswordHasher.cs` has had its `#if NETSTANDARD2_0` conditional compilation removed and uses
  PBKDF2-SHA256 throughout
- The netstandard2.0-related restrictions in the development guidelines have been removed as well

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

- **2026-09-27: target frameworks other than `net10.0`.** The packages and tools target `net10.0`, with two kinds of
  exception. `src/Polhem.Analyzers` targets `netstandard2.0`, because Roslyn analyzers are loaded by the compiler,
  which requires it. The platform heads of the demo application target platform-specific frameworks
  (`net10.0-ios`, `net10.0-android`, `net10.0-browser` under `apps/Polhem.Northwind/`).
