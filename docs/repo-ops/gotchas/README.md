# Pitfall log (gotchas)

Pitfalls actually hit while maintaining polhem that are **likely to be hit again**, with the symptom, the root cause
and the fix.

**This is not a rules document.** Hard rules are written in `.claude/rules/` (always loaded in every session); this
directory is context and reasoning to **read on demand**. It records "why that rule looks the way it does" and "what
the symptom looked like at the time", so that nobody walks into the same hole with the same misjudgement again.

**Nor is this a public document** (see `.claude/rules/public-docs.md`): its readers are polhem maintainers, not
framework users. Public design decisions go in `docs/adr/`; public descriptions of behavior go in `docs/en/` (the source) and its translations under `docs/<lang>/`.

| File | Covers |
|------|--------|
| [database.md](database.md) | Oracle `''`=NULL / positional binding / `RAW(16)` read back as `byte[]`, MySQL TEXT/UUID, SQLite GUID casing, decimal scale, the datetime2 parameter layer, the cost of deep-pagination `OFFSET` and the decision on it, the schema shared by load tests and unit tests |
| [serialization-and-expressions.md](serialization-and-expressions.md) | Wire facts (payload format vs codec), the retired MessagePack ctor-order and `[Union]` constraints (kept as history), the two expression engine pitfalls, measured AOT conclusions |
| [avalonia-controls.md](avalonia-controls.md) | Proven Avalonia control pitfalls (DataGrid, read-only appearance, events, parallelism) |
| [mobile-trim-aot.md](mobile-trim-aot.md) | Mobile trim / AOT: the reasoning behind the decision tree, the fidelity of the reflection-only reproduction, build and verification command recipes, **interpreting iOS build warnings (never 0 warnings)** |
| [test-ci-release.md](test-ci-release.md) | Test fixture gaps, the verification blind spot of the CI path filter, **a 0 from Sonar may mean "not looked at" rather than "clean" (`tools/**/*.cs` is outside the analysis scope)**, how to reproduce Sonar rules locally, pitfalls in the publishing and health check processes |
| [northwind-heads.md](northwind-heads.md) | The toolchains of the four Northwind heads (including the Xcode version binding of iOS), the sync process for the standalone repository and that repository's CI |
| [definition-and-customization.md](definition-and-customization.md) | The two ways of counting the customization scope (the same omission hit three times in a row), the granularity of the override layer, the two derivations of the `FormSchema` hub diagram |

## Neighbours that are not in this directory

The **external evidence** for the pagination approach (how Odoo / SAP RAP / SAP CAP / Microsoft ASP.NET OData each
paginate) is in [../pagination-prior-art.md](../pagination-prior-art.md). That is not a pitfall log; it is evidence
for a design decision, with no symptom and no fix. The deep pagination entry in this directory's `database.md` keeps
only the measurements, the decision and the scope, and always points there for the external comparison.

The pitfalls of the public API baseline (`PublicApiAnalyzers`) are in
[../public-api-baseline.md](../public-api-baseline.md). That document is already the authoritative operational
document for the analyzer, and keeping it in two places guarantees drift.
**When you hit `RS0027` (an existing overload has optional parameters, so you cannot add a new overload with more
parameters), read that document first.** It belongs to the kind that means "change the design", not "update the
baseline file".

## Writing principles

- **Record only what is likely to be hit again.** One-off environment problems, and problems the framework has fixed
  at the root and that will not recur, are not kept.
- Each entry must answer three things: **what the symptom looks like**, **the root cause**, and **the fix**. Without
  the symptom, nobody can find it.
- A pitfall that has been fixed at the root is still worth keeping, but it must say "fixed (commit)" explicitly and
  state the **remaining caveats**. If nothing remains, delete it.
- If the matching hard rule is already in `.claude/rules/`, this directory keeps only the context and does not repeat
  the rule text.
