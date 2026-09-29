# ADR-048: `Polhem.Base` is renamed to `Polhem.Core` in 1.1.0, a one-time break within 1.x

## Status

**Accepted (2026-09-30)**

A one-time exception to the compatibility rule stated in the context of
[ADR-046](adr-046-api-evolution-policies-for-1-0.md) ("a change that breaks it waits for the next major version").
It is not a precedent.

## Context

The lowest assembly of the framework was always meant to be called `Polhem.Core`. Polhem 1.0.0 was published on
2026-09-28 with the name it inherited from Bee.NET, `Polhem.Base`.

Renaming it changes the package ID and the namespace. Every consumer of every Polhem package writes
`using Polhem.Base...`, so the rename breaks source compatibility for all of them. Under the rule that ADR-046 states,
it would have to wait for 2.0.0.

When the decision was made, `Polhem.Base` had one version on NuGet, 1.0.0, two days old, with 59 downloads in total.
A package that new collects that many from mirrors and crawlers alone; no application built on it was known.

## Decision

1. **The assembly, the package and the namespace are renamed together**: `Polhem.Base` → `Polhem.Core`, including
   the unit test project. Renaming only the assembly and keeping the `Polhem.Base` namespace would leave a name that
   matches nothing. `TypeForwardedTo` cannot forward a namespace change, so there is no compatibility shim.
2. **The rename ships in 1.1.0**, not 2.0.0. It is a deliberate source-breaking change within 1.x.
   A minor version rather than a patch: automated dependency updates apply patch versions most readily, and a break
   hidden in a patch is the one most likely to fail someone's build unnoticed.
3. **Every `Polhem.*` 1.0.0 package is unlisted** once 1.1.0 is published, so no new application starts on the old
   name. Unlisting keeps them restorable for anyone who pinned them. `Polhem.Base` is also marked deprecated on
   NuGet, with `Polhem.Core` as the alternate package.
4. **The `PublicAPI.Shipped.txt` baselines are rewritten in place** (the old name replaced by the new one), not
   recorded as removals and additions. With 1.0.0 unlisted, the baseline stands for the 1.x line; a thousand
   `*REMOVED*` entries would have no reader.
5. **This is the only exception.** From 1.1.0 on, 1.x follows ADR-046 without exceptions.

The earlier ADRs keep the name `Polhem.Base` where they describe what was decided at the time; read it as
`Polhem.Core`.

## Consequences

- An application that did adopt 1.0.0 has to replace the package references and the `using` directives when it moves
  to 1.1.0. The CHANGELOG entry for 1.1.0 says so at the top, and describes the steps.
- The JSON-RPC type namespace allowlist in `SysInfo` now contains `Polhem.Core`. A type name under `Polhem.Base` is
  rejected like any other unknown namespace. The built-in list is held once in `SysInfo`, and
  `SysInfoSecurityTests.BuildAllowedTypeNamespaces_NoCustomNamespaces_ReturnsBuiltInDefaults` pins its contents.
- The name `Polhem.Core` sits next to `Polhem.Api.Core` and `Polhem.UI.Core`, where `Core` means the core of one
  area. `Polhem.Core` is the core of the whole framework, which is the same meaning at a wider scope.

## Alternatives considered

- **Release the rename as 2.0.0.** Strictly correct under semantic versioning. Rejected because a major version two
  days after 1.0.0, for a rename with no known adopters, would signal an upheaval that did not happen, while the
  unlisted 1.0.0 already keeps new users off the old name.
- **Keep `Polhem.Base` for good.** No break at all. Rejected because the cost of a rename only grows with the number
  of users, and it would never again be as low as now.
- **Release the rename as 1.0.1.** Rejected for the reason in decision 2.
