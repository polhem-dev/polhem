# Pitfall log: tests, CI and publishing

The matching hard rules are in `.claude/rules/testing.md` and `.claude/rules/commit-verification.md`.

## The verification blind spot created by the CI path filter

The `push` trigger of `.github/workflows/build-ci.yml` has a `paths` filter (the list is in the workflow file itself);
the `pull_request` trigger deliberately has none, because `build` is a required check of the branch protection on
`main` and a required check that never starts would leave a documentation-only pull request waiting forever (the
reason is also written next to the trigger).

**Why the push filter exists**: samples/ are demos and docs/ are documents; changes to either do not affect the
correctness of the NuGet packages, and this saves runner time.

**Day-to-day effect**: every change reaches `main` through a pull request (`maintainers/branch-protection-setup.md`),
so the pull request's own run is the check. After a pull request that touches only `samples/**` or `docs/**` is
merged, **the push to `main` starts no Build CI run**: there is nothing to wait for and nothing for `/ci-watch` to look
at. SonarCloud only runs in full mode after the tests, so a samples-only fix is not reflected there either.
(If the same merge touches both src/ and samples/, CI runs.)

### ⚠️ The related gap: three solutions are never built at all

`tools/` / `samples/` / `apps/` are **neither in `Polhem.slnx` nor in the push path filter**, and no CI step builds
their solutions (`tools/Polhem.Tools.slnx`, `samples/Polhem.Samples.slnx`, the Northwind slnx), so "local
`dotnet build Polhem.slnx` + `./test.sh` all green" **does not mean they still compile**, and CI will not find out for
you either.

**`tools/Polhem.LoadTests` and `tools/Polhem.Cli` are the exceptions for "compiling"**: each has a unit test project in
`Polhem.slnx` that points to it with a `ProjectReference`, so it gets built along with it and goes red on the spot if it
does not compile. `build-ci.yml` also packs `Polhem.Cli` in Release (its comment explains why that step builds it
itself). **But the exception stops at compiling**: SonarCloud still cannot see their source code; the reason is in this
file's section § A 0 from Sonar may mean "not looked at" rather than "clean". Do not extend it to the rest of `tools/`
(`tools/DefineEditor`, for one); no CI step builds those.

**Instance**: after `BackendComponents.EnterpriseObjectService` was deleted, leftover axaml bindings in
`tools/DefineEditor` caused AVLN2000. Both local and CI were green, until `tools/Polhem.Tools.slnx` was built by hand
and it blew up.

**When deleting or renaming any public type/member, these three solutions must also be built**:

```bash
dotnet build samples/Polhem.Samples.slnx --configuration Release
dotnet build tools/Polhem.Tools.slnx --configuration Release
dotnet build apps/Polhem.Northwind/Polhem.Northwind.slnx --configuration Release -p:ValidateXcodeVersion=false
```

XAML/axaml bindings are especially dangerous: they are **string bindings that grep can find but the C# compiler cannot
see**.

## A wrongly chosen test fixture misjudged as flaky

**Symptom**: `LogoutJsonRpcRoundTripTests` / `LeaveCompanyJsonRpcRoundTripTests` occasionally go red in CI; the
assertion `Assert.Null(response.Error)` fails, and the actual value is the masked `-32000 Internal server error`.

**Root cause**: these two classes use `IClassFixture<PolhemTestFixture>`, but after session persistence landed,
`Logout` DELETEs and `LeaveCompany` UPDATEs `st_session`: they have a real dependency on the database.
**`PolhemTestFixture` does not create the schema** (only `SharedDbFixture` does), so they only pass when "another test
process happened to create the tables first". `EnterCompanyJsonRpcRoundTripTests` in the same folder had long used
`SharedDbFixture`, for exactly this reason.

**Why it was misjudged as flaky**: one `gh run rerun --failed` happened to turn it green (a race condition does exactly
that), and outside development mode the exception message is masked as `Internal server error`, so you cannot see that
"the table does not exist".

**The takeaway**: **one rerun turning green is not enough to call a test flaky.** If the same group of tests is red on
the **first** run of different commits, investigate it as a real bug. A rerun is only for collecting evidence, not a
reason to close the case.

### An exhaustive scan to find every offending class

**Do not substitute grep reasoning for execution.** The trigger surface is wider than you think: not just
`IAccessTokenValidator`, but any `SessionInfoService.Get(uncached token)`, including `GetLangText` /
`GetCurrentCustomizeId` / looking up the current company inside a BO.

How: drop `st_session`, then run, project by project, the subset that "excludes every `SharedDbFixture` class". The
classes that create tables do not take part, so tests that depend on the table are bound to show up.

```bash
docker exec sql2025 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '<pw>' -C \
  -d common -Q "DROP TABLE IF EXISTS st_session;"
# Exclude every class in the project that inherits SharedDbFixture.
dotnet test tests/<Proj>/<Proj>.csproj -c Release --settings .runsettings \
  --filter "FullyQualifiedName!~.ClassA.&FullyQualifiedName!~.ClassB."
```

Always wrap the `--filter` value in double quotes (it contains `&`, which the shell would otherwise consume).
The table is recreated by the next `SharedDatabaseState.EnsureSchemaAndSeed`, leaving nothing behind for other tests.

> On 2026-08-04 this method found 4 offending classes in one pass (`ClientDefineAccessTests`,
> `JsonRpcExecutorCoverageTests`, `LogBusinessObjectTests`, `CacheTests`), while grep reasoning alone had found only
> the first one.

## A cache that loads from the DB itself assumes "the DB is always configured"

**Symptom**: all green locally, red in CI.

**Root cause**: `DbConnectionManagerService.GetConnectionInfo` throws `KeyNotFoundException` for an unregistered
databaseId (the KeyedCollection indexer blows up first; the `InvalidOperationException` in the code is a **dead path**),
while the caller often only wants to know "does this data exist".

**Why it cannot be caught locally**: the DatabaseSettings in `tests/Define` have common, so local fixtures can always
connect; fixtures without a DB, such as `Polhem.ObjectCaching.UnitTests`, only reach the unconfigured path in CI.

**Fix**: before loading by itself, check that the DB is configured
(`IDatabaseSettingsProvider.Get().Items.GetOrDefault(id)`); if it is not configured, treat it as "there is no such data
source" and return null. **Do not swallow the exception.** Hit and fixed on 2026-07-30 in the SessionInfoCache rebuild
([`caf45975`](https://github.com/jeff377/bee-library/commit/caf45975)).

## `[Ll]og/` in `.gitignore` swallows source folders

**Symptom**: a new `.cs` in `src/**/Log/` or `tests/**/Log/` is **completely invisible** to `git status`, and
`git add -A` does not add it → the commit is missing the file, and CI fails to compile because of the missing file.
`git check-ignore -v <file>` points to `.gitignore:<n> [Ll]og/`.

**Why**: `[Ll]og/` (the Visual Studio template default) is meant to ignore log output directories, but it ignores
**any** folder named `Log/` or `log/`, including namespace folders. IDE0130 also forces folders to map to namespaces,
so you cannot rename just the folder and keep the `.Log` namespace; a `!` negation that re-includes files under "a
directory matched by the rule" is also **ineffective**.

**Fix**: the read side of audit queries always uses `AuditLog/` (namespace `...AuditLog`), **not** `Log/`.
`AuditLog` also matches the progId / axis name. In any future case that wants `Log` as a folder name, use another name
in the same way (`AuditLog` / `Logging` both work; `Logging` is not ignored).

## Dead code scans: attributes must be grepped by their short form

**Symptom**: some `*Attribute` is judged "unused", and the build fails as soon as it is deleted.

**Root cause**: C# attribute usages almost always use the **short form without the `Attribute` suffix**
(`[TreeNodeIgnore]`); grepping only for the full name `TreeNodeIgnoreAttribute` gives a false "unused" conclusion.

**Instance**: the dead code list of the 2026-07-28 framework health check listed `TreeNodeIgnoreAttribute` as unused,
when it actually had 7 production uses (`CollectionItem` / `KeyCollectionItem` / `FormField` / `FormRule` /
`FormSchema` / `MessagePackCollectionItem` / `MessagePackKeyCollectionItem`).

**Fix**: for `*Attribute` types use `grep -rn "TypeName"` (without the `Attribute` suffix and without a trailing
boundary). The same applies to any type with a syntactic-sugar short form.

## A 0 from Sonar may mean "not looked at" rather than "clean"

**Symptom**: after fixing a Sonar rule, querying `/api/issues/search?rules=csharpsquid:SXXXX` returns 0, and it is
judged clean. In fact that file was never inside SonarCloud's analysis scope, and **the 0 has nothing to do with the
code**.

**Root cause**: SonarCloud actually only looks at `src/` and `tests/`. Measured on 2026-09-10 on the old bee-library
SonarCloud project (before the move to `polhem-dev_polhem`): of the 1,760 analyzed files in the whole project, `tools/` accounts for only **2**, and neither is C# (`tools/scripts/gen-public-api.py`,
`tools/DefineEditor/publish.sh`, which came in through generic file detection). **There was not a single
`tools/**/*.cs`**, even though `tools/Polhem.LoadTests` really is built through the `ProjectReference` of
`tests/Polhem.LoadTests.UnitTests`. **The mechanism has not been worked out** (SonarScanner should be able to intercept
transitively built projects); only the reproducible facts are recorded here.

**Fix**: before claiming a rule is clean, confirm that the file is inside the analysis scope.

```bash
curl -s "https://sonarcloud.io/api/components/tree?component=polhem-dev_polhem&qualifiers=FIL,UTS&ps=500" \
  | python3 -c "import sys,json;[print(c['path']) for c in json.load(sys.stdin)['components']]"
```

The response is paginated (`ps` is capped at 500); if there are more files, page through and merge.

**Incidentally, branch A/B cannot be read**: this organization's plan does not allow reading data from non-main
branches, and `?branch=<name>` always returns `Organization is not allowed to access data from non main branches`.
To compare two ways of writing something, do not go through CI; use the local reproduction below.

### Reproducing Sonar rules locally (also covers `tools/`, which CI cannot see)

```bash
dotnet add <project>.csproj package SonarAnalyzer.CSharp
dotnet build <project>.csproj -c Release --no-incremental -p:TreatWarningsAsErrors=false
# After reading the warnings, remove the PackageReference again.
```

Much faster than waiting for a full-mode CI run, and the only way to run Sonar rules on the C# under `tools/`.

### One claim this method overturned: where the guard sits has nothing to do with S2077

This file once said "move the allowlisting guard from the caller to the concatenation site, and S2077 will be
accepted". **On 2026-09-10 an A/B with the local analyzer above (a diff of 0 lines apart from the guard's position)
proved that wrong**: both placements still report S2077; only the line number differs. The "drop to zero" seen at the
time was an illusion caused by the false green light described above.

**The reason for putting the guard at the concatenation site still fully holds, but it is for people**: a reader
standing at the concatenation line should be able to see what protects it. **Do not claim again that it silences the
scanner.**

### What still holds: identifiers and values are handled separately

"Switch to parameters" does not work for identifiers: database names, table names and column names are identifiers,
and `CREATE DATABASE @name` is a syntax error in every engine. **Identifiers can only be allowlisted and then
concatenated; only values should be parameterized**, and the two often coexist in the same method. The effect measured
in the same A/B (`SchemaPreparer.cs`, SonarAnalyzer 10.34.0):

| Location | Before commit [`2e80fafb`](https://github.com/jeff377/bee-library/commit/2e80fafb) | After |
|------|------|------|
| PostgreSQL existence probe (**value** → parameterized) | S2077 | **gone** |
| PostgreSQL `CREATE DATABASE` (**identifier** → can only be allowlisted) | S2077 | **still there** (`SchemaPreparer.cs:169`) |
| The serve address in `Program.cs` (S1075 → moved into settings) | S1075 | **gone** |

**Parameterizing makes S2077 go away; allowlisting does not.** The identifier kind is bound to stay, and is handled
by the human review in `.claude/rules/sonarcloud.md`. Only, here in `tools/` there is not even a place to mark it
False Positive, because SonarCloud cannot see it.

## The step most easily missed when adding a published package

This applies to every published package, including the ones under `tools/` (today `tools/Polhem.Cli`, a dotnet
tool), not only the ones under `src/`.

The build-and-pack step of `.github/workflows/nuget-publish.yml` and the pack step of `build-ci.yml` **enumerate
the projects one by one; they are not a glob**. If a new package is left out:

- **nuget-publish**: the package is **not pushed to NuGet**, but the workflow still **shows success** (it only pushes
  what is already in `./nupkgs`). Consumers restoring other packages of that release that depend on the new package
  will fail.
- **build-ci**: pack verification does not cover the package.

**Instance, 2026-07-09 (Bee.NET era)**: Bee.NET 4.14.0 released `Bee.Expressions` (a new package, today's
`Polhem.Expressions`), and the pack lists of both workflows left it out → the first publish succeeded, but
`Bee.Expressions.4.14.0` was not on NuGet, while Bee.Business / Definition / UI.Avalonia all depended on it. Fix: add
the pack line to both workflows, commit, **delete the tag and push it again** onto the commit containing the fix to
trigger publish; `--skip-duplicate` skips what was already published and pushes only the new package.

**Instance, 2026-09-30 (1.1.0)**: `Polhem.Base` was renamed to `Polhem.Core`. The pack lists were updated, but
nobody treated the renamed package as a new one, so the policy still listed only `Polhem.Base`. The push runs in file
name order: the `Polhem.Api.*`, `Polhem.Business` and `Polhem.Cli` packages were pushed, then `Polhem.Core` was
rejected with 403 and the job stopped, leaving six 1.1.0 packages on nuget.org that depend on a `Polhem.Core 1.1.0`
that did not exist. Fix: add the ID to the policy and re-run the failed job; `--skip-duplicate` skipped the six. The
gate described in step 2 below was added so that the next miss stops the release before the first push.

**Signs to look for**: the publish workflow is green, but
`curl https://api.nuget.org/v3-flatcontainer/<pkg-lowercase>/index.json` returns BlobNotFound
(and it is not index delay). Check whether the push step log has `Pushing <Pkg>.nupkg... Your package was pushed.`;
if not, it was left out.

### Steps for a new package

1. Add the project to the list in the build-and-pack step of `nuget-publish.yml` (dependencies before dependents)
   and to the pack step of `build-ci.yml`. A project under `tools/` is not in `Polhem.slnx`, so the solution build
   in `build-ci.yml` does not build it in Release (a project outside the solution reached through a
   `ProjectReference` builds in its default Debug configuration): its pack line there has no `--no-build`, as the
   `tools/Polhem.Cli` line shows. `nuget-publish.yml` builds every listed project itself, but restores `Polhem.slnx`
   only, so a new `tools/` project also needs its own `dotnet restore` line there.
2. **Add the package ID to the nuget.org Trusted Publishing policy** before the first release that contains it (see
   "Publishing: NuGet Trusted Publishing" below). Otherwise its push is rejected, even though the pack lists are
   right. **A renamed package is a new package ID too.** The step "Check for package IDs new to nuget.org" in
   `nuget-publish.yml` stops the release before anything is pushed when a packed ID is not on nuget.org yet and not
   confirmed; its error message gives the steps.
3. Update the documents that list packages (for bilingual documents, both languages change):
   - `docs/en/architecture/dependency-map.md` (then its translation under `docs/zh-TW/`, restamped with
     `./check-docs-i18n.sh --stamp`): add the node + dependency edges to the mermaid diagram, a row to the external
     package table if it brings one, and the Architectural Notes. The document deliberately states no project count.
   - `README.md` + `README.zh-TW.md`: add a row to one of the package tables.

## Publishing: NuGet Trusted Publishing

`.github/workflows/nuget-publish.yml` runs when a `v*` tag is pushed (`.claude/rules/releasing.md`: an agent never
pushes one on its own). It holds **no long-lived NuGet API key**:

- The job has the `id-token: write` permission, and the `NuGet/login` step exchanges the GitHub OIDC token for a
  temporary API key, which the push step uses.
- The login step needs the repository secret `NUGET_USER`: the nuget.org user name (the profile name, not an e-mail
  address) of the account whose Trusted Publishing policy is used. It is not a credential.
- **The nuget.org policy must name this repository (owner + name) and the workflow file name** (`nuget-publish.yml`,
  file name only). The job declares no GitHub environment, so the policy's optional environment field stays empty.
  Renaming the workflow file or moving the repository to another owner changes what the OIDC token claims, and the
  login step fails until the policy is updated.
- **The policy's scopes decide which package IDs it may push** (a package glob, and whether pushing a *new* package
  is allowed at all). This repository's policy lists the package IDs, so a new package ID has to be added there
  before its first publish, or its push is rejected. CI cannot read the policy; the gate step before the login step
  stops the job instead when a packed ID is not on nuget.org yet.
- The temporary key is valid for about an hour, which is why the login step runs after the build and pack.

The nuget.org side is described in Microsoft's "Trusted Publishing" page for nuget.org.

The workflow file is the authority for the steps; this section only records what lives outside the repository.

## Methodology of the framework health check (`polhem-framework-review`)

The results and graded plan of each round are one document per round, kept in the maintainer's local `local/plans/`
(not under version control; those listing unfixed security issues go in `local/internal/`).
The rounds from the bee-library period are archived in the old repository's `docs/plans/archive/`. The methods below
**carry over across health checks**:

1. **A "falling score" is mostly deeper scanning, not a regression, but you may only say so after verifying each item
   with git.**
   In the 2026-07-28 round, most of the drops came from bringing `PackageReference`, `git show` history comparison and
   **actual execution checks** into the scan; each agent used git to check item by item when the problem was
   introduced, and confirmed that most had existed for a long time. You cannot say "this is not a regression" from a
   feeling.
2. **A health check baseline must not state unverifiable claims like "dead code 0"; it must list concrete types.**
   The previous round's baseline claimed "empty classes 0, dead code 0", and the next round found at least 15 unused
   types, all older than the previous round. The judgement was too optimistic (it most likely scanned only files with
   no references at all, and did not track types that survive as false positives through "declaration + DI
   registration" or "declaration + placeholder test"). **Placeholder tests make dead code show up as tested in the
   coverage report.**
3. **A P0 finding is worth the cost of measurement, to pin a "theoretical inference" down as a "known failure mode".**
   The P0 in the serialization dimension (definition responses losing all their content on the MessagePack wire) was
   originally only an inference. After building a standalone console project in the scratchpad with a
   ProjectReference to `Polhem.Api.Core` and measuring through the public `MessagePackPayloadSerializer`, the failure
   mode was pinned down as a **silent empty shell**; the choice of fix depended on this answer.

> **A process gap that has been closed**: the public API gate missed marking breaking changes two rounds in a row (the
> removals of `IExcelHelper` and `IEvictableCache` were not marked `!`). The root cause was "the commit prefix is the
> only source of the changelog, yet no mechanism checks whether the public surface had deletions or changes".
> `PublicAPI.Shipped.txt` / `Unshipped.txt` of `PublicApiAnalyzers` have been introduced (see
> `maintainers/public-api-baseline.md`), so a missing mark fails the build; what the analyzer cannot see, "declared
> but binary incompatible", is laid out as a notice by the pre-commit hook.
>
> **But "gate closed" does not mean "old debts paid"** (added 2026-08-07): the two cases ended differently.
> `IEvictableCache` **was** recorded in the CHANGELOG, while `IExcelHelper` was not even in the CHANGELOG until the
> 2026-08-07 health check found it and it was backfilled into the Bee.NET 4.16.0 detail file. Introducing a mechanism blocks
> "from now on"; what leaked out before has to be backfilled by hand. **The next time any gate is introduced, also make
> a list of "what had already leaked out before the gate".**
