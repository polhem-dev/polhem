# Maintaining the public API baseline (PublicAPI.*.txt)

Every package under `src/` has a pair of baseline files that record the public surface the assembly has declared:

| File | Content |
|------|------|
| `PublicAPI.Shipped.txt` | The public surface of the **released** version. Updated in one batch only at release time |
| `PublicAPI.Unshipped.txt` | Public APIs added **since** the last release |

`Microsoft.CodeAnalysis.PublicApiAnalyzers` compares them on every build and blocks in both directions:

| Situation | Diagnostic | What you do |
|------|------|-----------|
| A public type / member was added | `RS0016` | Add the line from the diagnostic message to `PublicAPI.Unshipped.txt` |
| Something was removed or its signature changed | `RS0017` | Delete the old line from the baseline file (**this is exactly the diff review needs to see**) |
| Nullable-oblivious signatures in Razor-generated code | `RS0041` | Turned off with `NoWarn` in `Polhem.Web.Blazor.Server.csproj`; see the comment there |
| A new overload has more parameters than an existing overload "with optional parameters" | `RS0027` | **Cannot be solved by adding to the baseline file**; the design must change, see the next section |

> **Why this mechanism exists**: before it, the only guard on "the public surface was removed or changed" was that
> the commit subject had to carry `!`, and that manual check had missed real breaks twice in a row (`IExcelHelper`,
> `IEvictableCache`; neither commit subject had `!`). The baseline files turn it into a build failure and a line of
> diff visible in review.
>
> **Note that "the gate is closed" does not mean "the old debt is paid"** (added 2026-08-07): the two cases above
> ended differently. Although the `IEvictableCache` commit was not marked `!`, the CHANGELOG **did** record it
> (Bee.NET 4.16.0, both languages of the root file and both languages of the detail file); `IExcelHelper` had **no CHANGELOG entry at
> all** until the framework health check on 2026-08-07 found it and it was recorded retroactively.
> Introducing the baseline files blocks "from now on"; what had already leaked out before still needs to be backfilled
> by hand and does not disappear on its own.

## `RS0027`: when an existing overload has optional parameters, you cannot add a new overload with more parameters

**Symptom**: you want to add a new overload with "more parameters" to an existing public method to carry a new
feature, and the build fails right away:

```text
error RS0027: 'TransformTo' violates the backcompat requirement:
'API with optional parameter(s) should have the most parameters amongst its public overloads'
```

**This one differs from the other diagnostics in the table above: it is not "not declared in the baseline file", and
adding the new signature to `PublicAPI.Unshipped.txt` does nothing at all.** The analyzer blocks the combination of
signatures itself.

**Root cause**: the existing overload already has a default parameter:
`TransformTo(ApiPayload, PayloadFormat, byte[]? encryptionKey = null)`. RS0027 requires that "an API with optional
parameters must have the most parameters among all its public overloads", so any new overload with more parameters
makes **the existing one** a violation. And the existing overload has shipped, so removing its default value is a
breaking change. Neither side can move.

**The right fix: do not add an overload; put the new data on the type as a property that the method reads.**
Stage 1 of JSON-RPC replay protection on 2026-09-01 ([`509b17e7`](https://github.com/jeff377/bee-library/commit/509b17e7))
solved it this way: the new frame data was placed on `ApiPayload.Frame` (`[JsonIgnore]`,
`src/Polhem.Api.Core/JsonRpc/ApiPayload.cs`), and not a single character of the two signatures
`ApiPayloadConverter.TransformTo` / `RestoreFrom` changed.

**The side effect is actually good**: because the signatures did not change, the caller
(`src/Polhem.Api.Client/Connectors/ApiConnector.cs`) did not need to change either. "Being forced to use a property"
is not a compromise here: the new data belongs to the payload's state and should have been a property rather than an
extra parameter from the start.

**The second-best option is to give the new overload a different name** (such as `TransformToFramed`): it compiles,
but the public API surface gains a set of names with overlapping meaning. Consider it only when the new data really
belongs to no existing type.

> To decide: **is the new thing "a parameter of this call" or "state of this object"?**
> If it is state, make it a property; RS0027 just brings this design question into the open early.
> If it really should be a parameter, do not rush to give it a default value when you first design a public method:
> an optional parameter **permanently pins** that method as "the overload with the most parameters", a harder
> long-term constraint than you might think.

## Day to day: what to do when you changed a public API

The build failure message itself contains the line in the correct format, for example:

```text
error RS0016: Symbol 'Polhem.Core.Foo.Bar() -> void' is not part of the declared public API
```

Paste the string inside the single quotes as a whole line into that project's `PublicAPI.Unshipped.txt` (keeping it
sorted is not a hard requirement, but recommended).
In the IDE you can also use the code fix the analyzer provides (*Add to public API*) to add it automatically.

## At release time

Merge the contents of each project's `PublicAPI.Unshipped.txt` into the same project's `PublicAPI.Shipped.txt`, then
empty `Unshipped` (keep the `#nullable enable` header). The contents of `Unshipped` before the merge are the complete
list of public APIs added in that version, and can be used directly to reconcile the CHANGELOG.

## Rebuilding the baseline in bulk (rarely)

Needed only when the baseline files are badly out of line, for example right after introducing the analyzer, or when
moving many namespaces at once.

```bash
SARIF=$(mktemp -d)
for i in $(seq 1 10); do
  rm -f "$SARIF"/*.sarif
  dotnet build Polhem.slnx --configuration Release -p:PolhemSarifDir="$SARIF" >/dev/null 2>&1
  for proj in src/*/*.csproj; do
    name=$(basename "$proj" .csproj)
    [ -f "$SARIF/$name.sarif" ] && python3 tools/scripts/gen-public-api.py "$SARIF/$name.sarif" "$(dirname "$proj")/PublicAPI.Shipped.txt"
  done
done
```

The loop is needed because a dependent project must compile successfully before the next layer gets analyzed. Each
round unlocks one layer; the dependency depth of this repo needs about 6 rounds to converge.

The `-p:PolhemSarifDir` switch is defined in `src/Directory.Build.props` and has no effect at all when no value is
passed.

---

## The same mechanism for analyzer rules (`AnalyzerReleases.*.md`)

`src/Polhem.Analyzers/` has another pair of baseline files, the same shape as `PublicAPI.*`, compared by the release
tracking of `Microsoft.CodeAnalysis.Analyzers`:

| File | Content |
|------|------|
| `AnalyzerReleases.Shipped.md` | The rules of **released** versions, in sections by `## Release x.y.z` |
| `AnalyzerReleases.Unshipped.md` | Rules added / removed / changed in severity **since** the last release |

| Situation | Diagnostic |
|------|------|
| A new rule is not declared | `RS2000` |
| A shipped rule disappeared and was not declared under Removed Rules | `RS2003` |

> ⚠️ **`RS2003` never fires against an empty Shipped file.** In Bee.NET, the framework Polhem continues, this file
> **had not a single line** from its creation until before 4.28.0, while the analyzer had shipped with the
> definition package since 4.16.0 (both Bee.NET versions). The consequence was exactly what an empty baseline file
> implies: `RS2000` still blocked "a new rule is not declared", but the "a shipped rule was removed" half effectively did not exist.
> **BEE4001–BEE4004 were retired in Bee.NET 4.19.0, and nothing made a sound.**
>
> Polhem's `Shipped.md` starts at a single `## Release 1.0.0` section listing the rules active at that release. The
> Bee.NET history is not carried over, and POLHEM4001–POLHEM4004 are reserved and never reused
> (`DiagnosticIds.ReservedIds`, checked by `DiagnosticIdDocumentationTests`).

### What to do at release time

Besides `PublicAPI.Unshipped.txt → Shipped.txt`, **there is a third baseline to move**:

```
AnalyzerReleases.Unshipped.md  →  AnalyzerReleases.Shipped.md (add a new section ## Release x.y.z)
```

Forgetting to move it gives no signal at all: the rules stay in Unshipped forever, and `RS2003` never protects them.
That is exactly what happened in Bee.NET between 4.16.0 and 4.27.0.
