# Pull requests and CI

## Every change reaches `main` through a pull request

`main` is protected: a pull request can only merge when the `build` check of `.github/workflows/build-ci.yml` and
the `docs` check of `.github/workflows/docs-check.yml` pass and the branch is up to date with `main`. The protection
applies to administrators too. Nobody pushes to `main` directly, maintainers included. Pull requests are squash
merged. The settings themselves are recorded in `docs/repo-ops/branch-protection-setup.md`.

1. Branch from the latest `origin/main`. Agents name their branches `claude/<topic>`.
2. Build and test locally when the environment allows it (see below), then push the branch with
   `git push -u origin <branch>`.
3. Open the pull request with `gh pr create`. Its title follows the commit message style (English, imperative).
4. Watch the pull request's checks, and handle a failure as described below.
5. Merge only when the checks pass. The user decides when to merge unless they have said otherwise.

**When the user explicitly asks for something else** (for example "push this to my fork" or "don't open a PR yet"),
follow the request. The user's instruction takes precedence over this default.

## Verify locally first when you can

On a machine that can run `dotnet build` and `dotnet test` (macOS, Windows or Linux desktop), run before pushing:

```bash
dotnet build Polhem.slnx --configuration Release
./test.sh    # or: ./test.sh tests/<Project>.UnitTests/<Project>.UnitTests.csproj for the affected projects
```

In an environment that cannot build (a phone, a tablet, Claude on the web), the pull request's CI is the only check,
so say in the pull request that nothing was verified locally.

Whether CI runs the full database matrix is a separate decision; ask the user before pushing, as described in
`testing.md` § CI database scope: ask the user before pushing.

## When CI fails

1. Read the failed check and its log (`gh pr checks`, `gh run view <run-id> --log-failed`) and find the cause.
2. Classify it:
   - **Clearly fixable** (compile error, failed test assertion, lint, formatting) → fix it, commit and push to the
     same branch.
   - **Architectural or unclear** → explain the situation to the user before deciding.
   - **Not actionable** (for example a transient failure of an external service) → explain the cause and suggest a
     re-run.
3. Wait for CI to run again after the fix.
4. Never close, skip or ignore a failure on your own. Never change a test or the source code just to make CI green.

If CI fails on `main` after a merge, fix it with a new pull request. Do not revert merged commits unless the user
asks for it.
