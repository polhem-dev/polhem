# Testing rules (core)

> **Before touching any file under `tests/`, Read `tests/CLAUDE.md` first.** The full rules are there. It loads
> automatically when you touch that directory, but **creating a new file does not always trigger it**, so read it
> once yourself.
> Copy-ready code templates are in `docs/repo-ops/testing-patterns.md`.
>
> This file keeps only what "you must know before writing, and would be too late if loaded later".

- **xUnit** v2 (the version is pinned in the test `.csproj` files) + **coverlet**; the global
  `<Using Include="Xunit" />` is already configured.
- Each `src/<Module>` maps to `tests/<Module>.UnitTests`; shared utilities are in `tests/Polhem.Tests.Shared/`.
- Method naming is `<MethodName>_<Scenario>_<ExpectedResult>` (`ValidateToken_ExpiredToken_ReturnsFalse`), and
  always add a `[DisplayName]` with an English description.
- Each test verifies **one behaviour**; security logic such as encryption / hashing **must** have tests; new public
  APIs get tests at the same time.

## Five decisions that cost you if you get them wrong

These are decided **before you write the first line**, so they stay in the always-loaded section:

1. **If it touches the DB, use `SharedDbFixture`, not `PolhemTestFixture`.**
   The latter **does not create the schema**; it only passes when "another test class happened to create the tables
   first". This is the number-one cause of "green locally, red in CI". **Reading counts as touching the DB too**
   (a token not seeded into the cache makes the server take the rebuild path and read `st_session`).
2. **When you need a database, use `[DbFact(DatabaseType.X)]` / `[DbTheory]`, not `[Fact]`.**
   It skips automatically when `POLHEM_TEST_CONNSTR_{DBTYPE}` is not set; `[Fact]` goes red in environments without
   the container. In CI, a skip for a database the run's mode requires fails `RequiredTestDatabaseGateTests`. It
   **does not apply** to pure logic / serialization tests: a bug there should be fixed directly, not skipped.
3. **Do not modify production `static` state** (except fixture initialization). xUnit runs different test classes in
   parallel, so touching the same static is bound to race; restoring it in `try/finally` only holds when running
   serially. When it cannot be avoided, put all related classes in the same `[Collection]`, and **use a `const`, not a
   string literal** (a typo creates an implicit group nobody shares, which looks serialized but is not, and does not
   cause a compile error).
4. **Do not write to `tests/Define/`.** It is fixed data shared by several projects; if a round-trip rewrites it,
   things break in a chain. Any `SaveDefine`-family call must first switch to an isolated temp directory.
5. **When the containers are not running, do not change test code or src to "make the tests pass".** Check the
   container status first, then judge.

## Running tests

`./test.sh` (detects and starts the local DB containers) or `./test.sh tests/<Proj>/<Proj>.csproj`.
The local environment check, the exception type → container mapping table and the flaky-test triage process are all
in `tests/CLAUDE.md`.

## CI database scope: ask the user before pushing

`build-ci.yml` runs only **SQL Server + SQLite** by default (lite mode, a few minutes, and **no SonarCloud**).
Running every database (PostgreSQL, MySQL and Oracle as well) must be **requested explicitly**: put `[all-db]` in
the commit message (for a PR, the PR title), or trigger `workflow_dispatch` manually with `db_scope=all`.

**Before pushing a branch or opening a pull request, always ask the user whether this change should run the full
mode.** Do not decide yourself: whether to run everything depends on the user's risk judgement about this change,
which cannot be read from the diff.

Signals that you should proactively **recommend** a full run: the change touches `src/Polhem.Db/Providers/**`,
`src/Polhem.Repository/**`, `SchemaSyntax` / `DbTypeMapper` / `NormalizeDbType`, or any SQL generation logic.
**Before a release, a full-mode run is mandatory.**

The cost of a missed run is low (just dispatch it again manually), so this is a written rule rather than a hook: a
`PreToolUse` hook can only allow/deny and cannot ask interactively, so enforcing it would mean blocking every push
once, which is not worth it.

> A side effect of lite mode skipping Sonar: `/sonar-fix` and `/ci-watch` need to look at **full-mode** runs, not the
> run from every push.
