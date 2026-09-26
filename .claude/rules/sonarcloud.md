# SonarCloud rule guide

Rules that commonly show up after SonarCloud scans this project. Follow them proactively when writing new code.

> Security SAST rules (SQL injection, XXE, path safety, resource disposal, exception handling basics) are in
> `scanning.md`; naming and formatting are in `code-style.md`.
>
> **Rules already enforced through `.editorconfig` are not listed** (the build fails): S1118 / S3442→CA1052,
> S2325→CA1822, S2933→IDE0044, S4487→IDE0051/0052, S927→CA1725, S6580→CA1305.
>
> **Not included**: the CA series (the compiler already guards them).

## Rule overview

| Rule | Principle |
|------|------|
| **S3925** | A class whose name contains `Exception` must inherit `System.Exception` |
| **S2094** | Empty classes should not exist; remove them or make them an interface |
| **S3260** | A `private` nested class that is not inherited should be `sealed` |
| **S2344** | An `enum` should not explicitly specify `int` as its underlying type (it is the default) |
| **S2342** | Enums with collection / flags semantics end in `s` (e.g. `TraceLayers`) |
| **S101** | Class names are Pascal case; in a run of capitals only the first letter of an acronym is capitalized (`Utf8StringWriter`, not `UTF8StringWriter`) |
| **S1006** | An override / implementing method must keep the same default parameter values as the base |
| **S4144** | Methods with identical implementations should be merged, or one should call the other |
| **S1066** | Mergeable nested `if`s are merged into a single `if` + `&&` |
| **S127** | A `for` loop should not modify its stop-condition variable in the body |
| **S4023** | Use pattern matching (`is MyType t`) instead of `is` + cast |
| **S1116** | Remove redundant empty statements (`;`) |
| **S3604** | A field explicitly assigned in the constructor should not also have an inline initializer |
| **S3963** | Static fields that can be initialized inline do not go in a static constructor |
| **S3877** | A static constructor should not throw (it makes the whole type unusable) |
| **S2743** | A `static` field on a generic type is not shared across closed constructed types; confirm it is intentional |
| **S6562** | `new DateTime(...)` must specify `DateTimeKind` explicitly |
| **S3267** | Use `.Where()` instead of filtering with `foreach` + `if` |
| **S3878** | A `params` call site does not need to build an array explicitly; pass the elements directly |
| **S112** | Do not throw `ApplicationException`; use a custom exception or `InvalidOperationException` |
| **S1133** | `[Obsolete]` code that definitely has no callers should be removed |
| **S3885** | Use `Assembly.Load` (by `AssemblyName`) instead of `Assembly.LoadFrom` (by path; the load context becomes inconsistent) |
| **S2701** | The first argument of `Assert.True` / `Assert.False` should not be a literal |
| **S3776** | Cognitive complexity limit is 15; exceeding it usually means one method does two things, see below |

## S3776 — Cognitive complexity (included since 2026-09-10)

The threshold is 15. The scoring emphasises **nesting increments**: an `if` inside a loop scores 2, one level deeper
scores 3, while a flat `switch` scores 1 no matter how many cases it has. So a high score almost always points to
"nested too deep", not "too many branches".

**This rule used to be listed under "Not included"; the exemption was removed on 2026-09-10.** The reason at the time
was "it is a matter of judgement". That statement was true, but it had become an excuse not to look. After actually
working through the four methods over the threshold one by one, **all four had a real seam**:

| Method | Score | The real problem |
|------|------|-----------|
| `WireValueJsonConverter.ResolveCode` | 22 | 22 consecutive `if (type == typeof(X)) return` statements; it was a lookup table all along |
| `FormBusinessObject.EnforceWriteScope` | 20 | One loop both "collects master rowids" and "checks for out-of-scope rows", and the two disagree about which rows count |
| `FormBusinessObject.EnforceDetailOwnership` | 18 | Three nested `foreach` loops; the middle one is a complete "check one detail table" |
| `PolhemFrameworkServiceCollectionExtensions.AddPolhemFramework` | 18 | A 317-line method with a self-contained audit writer registration embedded in it |

**How to read it**: treat it as a probe for "is this method doing two things", not as a number to push down.
If you can name the second thing, extract it; if you cannot (for example, a series of independent business rules that
cannot be merged), mark it *Won't Fix* in the SonarCloud UI with the reason written down. **Do not force out an
unnamed method just to lower the score.** That only moves the score elsewhere, and the reader still has to jump
around.

**Local reproduction** (CI runs Sonar only in full mode): S3776 is not enabled by default in the NuGet analyzer
package. You need to temporarily add a `PackageReference` and enable it explicitly in `.editorconfig`; details are in
`docs/repo-ops/gotchas/test-ci-release.md`.

## S6444 — Always pass a timeout to Regex (ReDoS protection)

`Regex.IsMatch` / `Regex.Replace` / `new Regex(...)` **always** pass `TimeSpan.FromSeconds(1)`, even when the pattern
is a compile-time constant or already `Regex.Escape()`d.

```csharp
Regex.IsMatch(input, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
new Regex(pattern, RegexOptions.Compiled, TimeSpan.FromSeconds(1));
```

## S7636 / S7637 — GitHub Actions

Do not expand secrets directly in `run:` (they end up in the log); inject them with step-level `env:` and reference
that instead. Pin third-party actions to the full commit SHA, and keep the version tag in a trailing comment for
readability.

```yaml
- name: Push to NuGet
  env:
    NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
  run: dotnet nuget push ... --api-key "$env:NUGET_API_KEY"

- uses: actions/checkout@34e114876b0b11c390a56381ad16ebd13914f8d5  # v4.3.1
```

Get the SHA: `gh api repos/<org>/<name>/git/ref/tags/<tag> --jq .object.sha`

## S125 — Dead code detection (high false-positive rate; do not delete blindly)

Remove only code that really is commented out; use `git log` to keep history.
**This rule has a high false-positive rate on English WHY comments**: the heuristic parser hits as soon as it sees
English identifiers + a trailing `;`.

Process: `/sonar-fix` already excludes S125 from automatic fixes (it always goes to `humanReview`) → a human reviews
the location → a legitimate WHY comment is marked *False Positive* in the SonarCloud UI; only real dead code is
deleted by hand.
**Do not delete comments to pass the scan.** For the positive way to write them (complete English sentences, ending
with `.`, avoiding a trailing `;`), see `code-style.md`.
