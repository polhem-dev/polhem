# Testing rules (full)

This file loads automatically when the agent reads any file under `tests/` (a nested `CLAUDE.md` loads lazily;
confirmed by a top-level session on 2026-08-12. **Whether "only Write a new file, never Read" triggers it has not been
verified**, so the always-loaded file keeps the safeguard "Read this file before you start writing"). The skeleton and
the five hard constraints you "must know before you start writing" are in `.claude/rules/testing.md` (always loaded);
paste-ready code templates are in `docs/repo-ops/testing-patterns.md`.

When the two conflict, this file wins. The always-loaded one is a summary.

---

## Environment checks before running tests locally (only locally, and only when docker is available)

> **Deciding whether this applies takes one line**: `command -v docker`. No output means skip this whole section and
> run the tests directly (`[DbFact]` skips automatically when its env var is not set). **Not for CI**: `build-ci.yml`
> handles its own containers and env vars in the workflow. Do not carry anything from this section into the yml.
>
> CI has **two modes**. The default is lean (SQL Server through a service container, plus SQLite; `[DbFact]` for the
> other three databases is all skipped). With the `[all-db]` marker or a manual dispatch it is full (PostgreSQL /
> MySQL / Oracle are started by a step with `docker run`). For the criteria and "ask the user before pushing", see
> `.claude/rules/testing.md` § CI database scope: ask the user before pushing.

### Why check first

`./test.sh` **gives no obvious signal** for "container missing → env var not set → `[DbFact]` skips automatically".
So "skipped as planned" and "should have run but didn't" look the same. Checking first is what lets you report clearly
"X databases skipped because container Y is not present", and it keeps you from mistaking a DB connection failure for
a code bug.

### Checks before starting

1. **Docker daemon**: `docker ps`. If it fails, **tell the user to start Docker Desktop. Do not run
   `open -a Docker` yourself** (an agent launching a GUI tool is slow and the result is uncertain).
   **Exception: this step is not needed when you go through `./test.sh`**. It has a built-in `ensure_docker_daemon`
   that starts Docker automatically on macOS and polls until it is ready.
2. **Containers exist**: compare `docker ps -a --format '{{.Names}}\t{{.Status}}'` against
   **the four containers listed in the `test.sh` header** (the default names and the `POLHEM_TEST_*_CONTAINER`
   overrides are written there; this file does not copy them, to avoid drift). If any is missing, tell the user
   "tests for that database will be skipped automatically".
   **Do not `docker run` a new container yourself** (image version / port / volume / initial schema all have
   constraints, and an ad hoc container will collide with the existing setup). A container that exists but is stopped
   needs no action; `./test.sh` runs `docker start`.

### Order for diagnosing test failures (local)

After running `./test.sh`, if you see the exception types below, **suspect container state first. Do not touch the
test code directly**:

| Exception fragment | Points to |
|-------------|------|
| `SqlException` containing "TCP" / "network-related" / "server was not found" | SQL Server container |
| `NpgsqlException` containing "connection refused" / "Failed to connect" | PostgreSQL container |
| `MySqlException` containing "Unable to connect" / "Can't connect to server" | MySQL container |
| `OracleException` containing "ORA-12541" / "ORA-50201" / "TCP transport" | Oracle container |

Procedure: `docker ps --filter "name=<container>" --format '{{.Status}}'` to confirm the container is running →
only if it is running, consider schema / seed / connection string problems → if it is not running, prompt the user to
start it. **Never** modify test code or src code to "make the tests pass".

> When the same exceptions appear in CI, follow "When CI fails" in `.claude/rules/pull-request.md`. Do **not**
> apply this section (CI does not go through the docker CLI).

### Tolerance for parallel flakiness (applies locally and in CI)

`./test.sh` and CI run several test projects in parallel inside one `dotnet test` invocation.
"The same DB test passes in isolation and fails in the full suite" is usually connection pool / container resource
contention under parallel load, and **should not be treated as a production bug straight away**. Run the failing
project once more on its own. If it passes, it is flaky: note it, do not fix it. Only if it keeps failing over 2–3 runs
is it a real bug.

---

## Test writing patterns

Use `[Fact]` for a single check and `[Theory]` + `[InlineData]` for parameterized ones. Always add `[DisplayName]`:
one English sentence in sentence case, present tense, no trailing period, describing what the test actually checks
(`GetLocale with an unknown user returns null instead of throwing`). Identifiers and literal values keep their exact
spelling.

### Needs a database: `[DbFact(DatabaseType)]` / `[DbTheory(DatabaseType)]`

These replace `[Fact]` / `[Theory]`, **and name the database type the test targets**. Both attributes are defined in
`tests/Polhem.Tests.Shared/`. They check an environment variable by the rule `POLHEM_TEST_CONNSTR_{DBTYPE}` (the
enum value in uppercase), for example `SQLServer` → `POLHEM_TEST_CONNSTR_SQLSERVER`. **If it is not set, the test
is skipped automatically.** Adding `MySQL` / `Oracle` and so on needs no new class; the rule derives the name.

Connection ID naming rule `common_{dbtype_lower}` (produced by `TestDbConventions.GetDatabaseId`):
`common_sqlserver`, `common_postgresql`, …

- **Locally** (`.runsettings` sets `POLHEM_TEST_CONNSTR_*`) and **in CI** (injected by the workflow), they run normally.
- **Any DB without its environment variable**: that DB's tests are Skipped automatically; other DBs are unaffected.
- **In CI a skip is not allowed for the databases of the selected mode**: the workflow lists them in
  `POLHEM_TEST_REQUIRED_DATABASES`, and `RequiredTestDatabaseGateTests` (`tests/Polhem.Db.UnitTests`) fails when one
  of them has no connection string under the name `TestDbConventions` derives.

`DbGlobalFixture` supports several DBs side by side and is fault tolerant: for each one it detects the env var,
verifies the connection, builds the schema and writes the seed. A failure in one DB only skips that DB.

**Use for**: pure database dependencies (queries, schema, Repository/BO).
**Do not use for**: pure logic / serialization tests. A bug there should be fixed directly, not skipped.

### Common / Log scope repositories: besides `[DbFact]`, you must also swap the router

`RepositoryDatabaseRouter` resolves `DbScope.Common` / `DbScope.Log` to the fixed `common` / `log`,
and the fixture binds `common` to **SQL Server**. So for repositories that declare Common scope, such as
`SessionRepository`, `UserRepository`, `CompanyRepository`, `UserCompanyRepository`,
`ApiKeyRepository` and `DatabaseRepository`,
**just adding `[DbFact(DatabaseType.Oracle)]` still runs against SQL Server**. The attribute is left acting only as the
env var gate.

The fix: when creating the repository, swap the router for `ProviderScopedRouter` (`tests/Polhem.Tests.Shared/`).

```csharp
private UserRepository CreateRepo(DatabaseType databaseType)
    => new UserRepository(
        TestRepositoryContext.Create(
            _fx.GetRequiredService<IDbConnectionManager>(),
            router: new ProviderScopedRouter(databaseType)),
        Guid.Empty, string.Empty);
```

**Two telltale signs** (if you see one, this is the problem):

- `private void RunXxx(DatabaseType _)`: the parameter is accepted and thrown away, which amounts to declaring
  "this test ignores the provider".
- The arrange step uses `TestDbConventions.GetDatabaseId(dbType, ...)` to write into that provider's DB,
  but the act step uses a `CreateRepo()` without dbType. **Passing vacuously** like this is worse than no test: the
  assertion always holds, and it stays green even if the logic under test is removed entirely.

The BO layer (`SystemBusinessObject*`) cannot bypass the router; it is provided by DI. Those tests are about common
scope, one provider is enough, and **the gate is marked `SQLServer`**. Do not mark it `SQLite`, which would split the
skip condition from the database that actually runs.

> Runtime inventory on 2026-09-08: of 73 test classes declaring `[DbFact]`, 20 targeted the wrong database.
> Of those, 50 tests never touched the declared database at all, and 8 passed vacuously. Correcting them surfaced two
> framework defects on the spot (Common scope repositories picked the SQL dialect from `DbCategoryIds.Common` instead
> of their own `DatabaseId`; SQLite date columns were rejected by an `is DateTime` check). Details in
> `docs/repo-ops/gotchas/database.md`.

### Needs a local service: `[LocalOnlyFact]` / `[LocalOnlyTheory]`

These check the environment variable `CI`; **when `CI=true` (the GitHub Actions default) the test is skipped
automatically.**
**Use for**: integration tests that genuinely need a locally running service (for example an API server ping).
**Do not use for**: tests that only need a DB. Use `[DbFact]`.

> **Neither currently has any users** (verified 2026-08-11), and `[DbTheory]` is also rare. They are kept because the
> scenario "integration test that needs a local service" still exists. The examples in the template file are
> **illustrations, not existing code**. Do not grep for them.

### Needs dynamic code: `[DynamicCodeFact]`

Skips when `RuntimeFeature.IsDynamicCodeSupported` is false, which is what the mobile AOT gate
(`-p:DynamicCodeSupport=false`) reproduces. **Use for**: a desktop-only capability that genuinely cannot work without
`Reflection.Emit`, such as serializing an application type that only the `SysInfo.AllowedTypeNamespaces` escape hatch
admits. **Do not use for**: making an AOT gate failure of the framework's own wire types go away; that failure is the
bug. The attribute's XML doc (`tests/Polhem.Tests.Shared/DynamicCodeFactAttribute.cs`) is the source.

### Repository root: `RepoRoot.Find()`

A test that reads a file from the working tree (a doc gate, a csproj gate, `tests/Define`) finds the root through
`RepoRoot.Find()` in `tests/Polhem.Tests.Shared/`. It accepts `.git` as a file as well as a directory, so a git
worktree resolves to itself rather than walking up into the main checkout. Projects that do not reference
`Polhem.Tests.Shared` link the file in (`<Compile Include="..\Polhem.Tests.Shared\RepoRoot.cs" Link="RepoRoot.cs" />`).
Do not write another walk-up loop.

### Per-class fixture (default pattern)

When you need DI-resolved backend services (`IDefineAccess` / `ISessionInfoService` /
`IBusinessObjectFactory` and so on), get a per-class `IServiceProvider` through `IClassFixture<PolhemTestFixture>`.
Two special cases:

| Case | Fixture | Notes |
|------|---------|------|
| Needs per-fixture file writes (the `SaveDefine` family) | `new PolhemTestFixture(b => b.UseTempDefinePath())` or a custom subclass | Switches `PathOptions.DefinePath` to an isolated temp directory |
| Needs `[DbFact]` integration tests | `IClassFixture<SharedDbFixture>` | Has `UseSharedDatabases()` built in; builds the schema and seeds the user once per process |

The `[Collection("Initialize")]` / `GlobalFixture` / `BaseTests` / `PolhemTestServices` /
`TempDefinePath` / `DefinePathInfo` / `CacheContainer` static facades **have all been removed**.
Each fixture carries its own `IServiceProvider`, and xUnit's default collection-per-class parallelism is restored.

---

## Global state and parallel safety

xUnit defaults to collection-level parallelism: **different test classes run in parallel**, and tests within one
collection run serially. Any "static / global state shared across classes" is bound to race under parallelism.

- **Test methods must not modify production `static` state, except during fixture initialization** (including static
  properties / fields and `AppDomain`).
- When production must expose global state as a static (such as `SysInfo.IsDebugMode`), prefer **refactoring it to be
  injectable** (add an overload that takes a parameter, or extract an interface and use DI).
- When refactoring costs too much, **put every test class that touches the same static in the same
  `[Collection("...")]`**.

### Why this pitfall is easy to hit

A local machine has many CPUs and loose scheduling, so the race does not always trigger. CI runners usually have
2 cores, parallelism is denser, and it surfaces there. The failure message (such as
`NoEncryptionEncryptor is only permitted in debug/development mode`) looks like a production bug, but the root cause
is tests contaminating each other. Restoring in `try/finally` "looks" safe but only holds when tests run serially.

### Serialization approach (interim)

Declare a pure marker `[CollectionDefinition("<name>")]` (no fixture) at the root of the test project, and put every
test class that modifies that static in the same `[Collection("<name>")]`. Template in
`docs/repo-ops/testing-patterns.md`.

### Narrow serializations that still exist

Most tests now use fixture-scoped DI instances, which removes the race risk naturally. Every existing `[Collection]`
protects a process-wide static that has not been moved to DI yet:

| Collection (the constant users reference) | Protects |
|---|---|
| `ClientInfoStateCollection.Name` | `ClientInfo.*` |
| `SysInfoStaticCollection.Name` | `SysInfo.*` (`Polhem.Base` and `Polhem.Api.Core` each define their own; across assemblies it has to be this way) |
| `ApiClientInfoStateCollection.Name` | `ApiClientInfo.*` |
| `ProcessWideStateCollection.Name` | the `POLHEM_MASTER_KEY` environment variable, `GlobalEvents`, DI containers built inside a test body |
| `ApiServiceOptionsStateCollection.Name` | `ApiServiceOptions.*` |

Each definition is a `static` class declaring `public const string Name`, and every user writes
`[Collection(XxxCollection.Name)]`. A mistyped constant does not compile; a mistyped string literal would silently
create a collection nobody shares.

Several assemblies instead serialize **as a whole** with `DisableTestParallelization`, which is more reliable than
adding `[Collection]` class by class. Readers grow as new tests are added, and a requirement like "remember to add
`[Collection]` when you add a test" will inevitably be missed. When it is missed, it **looks serialized but is not**,
and there is no compile or test signal.

**Which assemblies these are is not written here** (it would drift). To find out, run:

```bash
grep -rn 'DisableTestParallelization *= *true' tests/ --include='*.cs'
```

> **This paragraph itself drifted once** (fixed 2026-09-04). The original listed five assembly names, and
> `Polhem.Definition` at the time did **not** have the attribute; it relied on `ProcessWideStateCollection`, which
> only covers three classes. The harm was in the second half: the document claimed **stronger** protection, and in
> reality it had only the one that admits it can miss things. The fix was two things: add the attribute to
> `Polhem.Definition.UnitTests` so the claim became true (measured cost +0.2–0.4 seconds / 1,086 tests), and
> **replace that list with the command above**. No mechanism would ever notice the list had drifted.

> **When adding a collection, follow the same shape: a `static` definition class with a `const` name, referenced
> by the constant, never by a string literal.** A mistyped literal makes xUnit create an implicit group that nobody
> shares. It **looks serialized but is not**, and there is no compile error. To check that no literal crept back in:
> `grep -rn '\[Collection("' tests --include='*.cs'` should print nothing.

---

## Isolating shared fixture files

The XML files in `tests/Define/` (`SystemSettings.xml`, `DbCategorySettings.xml` and so on) are
**fixed data shared by several test projects**, read by `TestProcessBootstrap` at startup. No test
**may write to or modify** these files. Once they are rewritten (including xmlns order, indentation or child node
changes caused by round-trip serialization), the next read behaves abnormally or fails to deserialize, causing
cascading failures.

Any call in the `SaveDefine` family (`SaveDbCategorySettings`, `SaveSystemSettings`,
`SaveTableSchema`, `SaveFormSchema`, `SaveDefine`), **or anything that triggers one indirectly**, must switch to an
isolated temp directory:

1. **fixture-level** (recommended): `new PolhemTestFixture(b => b.UseTempDefinePath())` or a custom subclass.
   `PathOptions.DefinePath` points to `%TEMP%/polhem-fixture-<guid>` and is cleaned up on dispose.
2. **method-level**: when testing a class whose ctor takes `PathOptions` directly, such as `CacheDefineAccess` /
   `FileDefineStorage`, build an inline temp dir and pass `PathOptions { DefinePath = tempDir }`
   (the two-parameter overload `CacheDefineAccess(IDefineStorage, PathOptions)` exists for exactly this).

If you need to `GetDefine` an existing fixture first and then `SaveDefine`: **Get through the fixture's default path
first (from `tests/Define`) → construct a temp `IDefineAccess` → Save**, so that Get does not find nothing in an empty
temp directory.

Full template in `docs/repo-ops/testing-patterns.md`.

---

## Common analyzer rejections

The strict build stage of `build-ci.yml` blocks the PR outright. These are especially easy to hit:

- **S2699**: every `[Fact]` / `[Theory]` needs at least one `Assert.*`. To verify "no exception", do not make a bare
  call; capture it with `Record.Exception` / `Record.ExceptionAsync` and then `Assert.Null(exception)`.
- **CA1861**: do not pass a constant array inline as `new[] { ... }` as an argument (it allocates on every call).
  Extract it to a `private static readonly string[] s_xxx = { ... }` at the top of the file.
- **IDE0005**: copying the header from another test file easily brings in unrelated `using` directives. Remove them
  one by one once you are done.
- **CS1574 / CS1570 / CS1573**: XML comments in test code are checked like those in `src` (only CS1591, "missing
  comment", is suppressed in `tests/Directory.Build.props`). A `<see cref>` to a type or member that no longer exists
  fails the build. A private member of another assembly cannot be a cref; name it with `<c>`. In a test project that
  has a `System` sub-namespace (`Polhem.Api.Core.UnitTests.System`, for example), qualify BCL types in a cref with
  `global::`.

---

## Recurring root causes of "green locally, red in CI"

The local environment is "more complete" than CI (it has the DatabaseSettings in `tests/Define`, persistent DB
containers, and possibly leftover old seed data), so the gaps below **can never be detected locally**. Pitfall
instances and the investigation are in `../docs/repo-ops/gotchas/test-ci-release.md`.

### 1. Tests that touch the DB must use `SharedDbFixture`

**`PolhemTestFixture` does not build the schema** (only `SharedDbFixture` does). If a test makes a BO touch the DB
(session / audit / any repository read or write), the fixture must be `SharedDbFixture`. Otherwise it only passes when
"some other test class or process happened to build the tables first". **`PolhemTestFixture` plus DB access is a
suspect on sight.**

Shortcut: in the test environment `AuditLogOptions.Enabled` defaults to `false` and `tests/Define/SystemSettings.xml`
does not override it → a change that only touches audit writes is never evaluated in tests, so it can be ruled out
first.

**"Writing" is not the only trigger; reading fails just the same.** As soon as a test calls an API that requires
authentication with **a token not planted in the cache**, the server gets a session cache miss → takes the rebuild path
and reads `st_session`. How to recognize it: the test uses `Guid.NewGuid()` directly as the access token (instead of
`TestSessionFactory.CreateAccessToken(fx)`, which writes the SessionInfo into the cache and therefore never reaches the
DB).

**The third path: every request that goes through the controller touches `st_api_key`.**
`ApiServiceController.ValidateApiKey` → `ApiKeyValidator.Validate` → `ApiKeyGate.GetState()`
is a read-through that opens a common connection and reads `st_api_key` on a miss. `AddPolhemFramework` **always**
registers the real `ApiKeyValidator`, so this path has nothing to do with the access token. Any test that hits the
controller takes it.

**And `SharedDbFixture` only solves half of it.** Once the table exists, whether the gate is in force depends on
**whether the table currently holds an enabled key**, which no test guarantees: `ApiKeyRepositoryTests` writes keys
into the same common database, and a persistent local container keeps leftover rows across runs. When the gate is in
force, any header that does not match the key format becomes `ApiKeyStatus.Invalid` → 401. **If the test is not about
the key gate, override `IApiKeyValidator` with an instance the test supplies itself**
(`TestOverrideServiceProvider` accepts a `null` instance and short-circuits the inner provider; that is the only way to
reach the "no validator registered" branch). Do not rely on the table happening to be empty.

> The symptom of this one does not point to the real cause at all, and its direction is the **opposite** of every
> other item in this section: CI always starts with fresh containers and `st_api_key` is always empty, so it is
> **red locally, green in CI**. That was two tests in `Polhem.Api.AspNetCore.UnitTests` on 2026-09-08.
> The symptom was `Assert.IsType<ContentResult>` getting an `ObjectResult`, which looked like an MVC version
> difference. One of them, `Post_NoValidatorRegistered_UsesPresenceCheck`, **had never taken the path it is named
> after**: it only added the override when the validator was non-null, so it always fell through to the real
> `ApiKeyValidator`. The acceptance criterion is that the tests are still all green **with enabled keys still left in
> the table**. Clearing the database first and then running proves nothing about decoupling.

**Do not decide the scope by static grep.** The trigger surface is wider than you think: not just
`IAccessTokenValidator`, but any `SessionInfoService.Get(uncached token)`, including `GetLangText` /
`GetCurrentCustomizeId` / looking up the current company inside a BO. **Use an exhaustive scan; do not substitute
reasoning for execution**: drop `st_session`, then run, project by project, the subset that "`--filter` excludes every
`SharedDbFixture` class". The classes that build tables do not take part, so tests that depend on the table are bound
to show up. The full command and the measured results from 2026-08-04 are in the gotchas above.
**That time this method found 4 offending classes in one pass; pure grep reasoning beforehand had found only 1.**

### 5. Leftover rows in a persistent local container contaminate **other** test projects, and `git stash` does not restore them

A half-edited test **leaves traces once it has run**. On 2026-09-08, while rewriting `ApiKeyRepositoryTests`,
insert and delete pointed at different databases for a while, the `finally` cleanup cleaned up on another engine, and
**7 enabled `rt-*` keys** were left in `common.st_api_key` on `sql2025`. Those 7 rows kept `ApiKeyGate` **permanently**
in force locally, and as a knock-on effect the two controller tests from the previous section were red for hours.
The symptom (`ContentResult` getting an `ObjectResult`) did not point to keys at all.

**Two judgment disciplines to remember:**

1. **`git stash` cannot establish "this was red before the change".** stash only restores version-controlled files;
   leftover database rows, container state and environment variables do not go back to that point in time. That time,
   stash led to the misjudgment "pre-existing red, unrelated to this change", and a round was spent searching the code.
   **Check the actual database state before concluding** (one `SELECT` costs far less than one misleading round).
2. **When it is "red locally, green in CI", ask about container lifecycle first, not core count.** CI uses
   `services:` + `docker run`, fresh every time, with tables always empty; locally the containers are persistent and
   leftovers accumulate across runs. The initial misjudgment was "more local cores, overlapping parallel windows", and
   one piece of evidence overturned it: **running a single test project alone reproduces it**. Real parallel contention
   needs the other side to be running at the same time.

**Investigation technique**: when you suspect leftovers, query that table's rows and `sys_insert_time` directly. If the
timestamp is earlier than the start of the session, it was not produced by this run. The way to confirm HEAD itself
does not leak is to **count again after the run**: if the row count is unchanged, the cleanup is correct.

### 2. One rerun turning green is **not enough** to call it flaky

`gh run rerun --failed` happening to turn green is the normal behaviour of a race condition, not grounds to close the
case. A rerun is only for **collecting evidence**: at minimum, check whether the **first** run of different commits is
red every time. If they are all red, investigate it as a real bug.

> This does not conflict with "Tolerance for parallel flakiness" above: that item is about **passing in isolation /
> failing in the full suite within one commit** (decided by running 2–3 times); this item is about **first runs being
> red across commits**.

### 4. Gates that enumerate `GetTypes()` must be verified under coverage instrumentation

**Coverage instrumentation injects types into the assembly.** coverlet injects
`Coverlet.Core.Instrumentation.Tracker.<assembly name>_<guid>`, carrying methods such as `RecordHit` /
`RegisterUnloadEvents`. Any gate that "enumerates an assembly's types and asserts on their shape" counts it.

**And this cannot be verified on lean-mode CI**: coverage is only collected in **full mode**
(`--collect:"XPlat Code Coverage"` in `build-ci.yml`). So such a gate can be green locally and green on lean-mode CI
for weeks, until some run with `[all-db]` turns it red.

To reproduce locally, pass the same flag:

```bash
dotnet test <test project> -c Release --settings .runsettings --collect:"XPlat Code Coverage;Format=opencover"
```

**The fix is to narrow by namespace to "types declared in source"**, not to list tool names (each instrumentation tool
injects under its own namespace, and a list will always miss one). And put the **anti-vacuous assertion after the
filter**: if the filter condition is wrong, the loop never runs a single iteration and stays green forever, which is
worse than a false positive.

> Instance: `ArchitectureBoundaryGateTests.ApiContracts_ContainNoImplementation`
> reported the injected Tracker as "implementation mixed into the contracts axis" (fixed 2026-09-04).

### 3. Table creation and seed idempotency must both be atomic across processes

The `SharedDatabaseState` setup is executed concurrently by several parallel test processes against **the same
physical DB**. **Table creation and seeding are two halves of the same structural problem**, and both halves must be
handled.

#### 3a. Table creation (fixture setup)

Internally `TableSchemaBuilder.Execute` is read-then-create: if it cannot read the table, it plans a `CREATE TABLE`.
When two processes come in at the same time, each plans one, and the loser hits
`There is already an object named 'st_user'` (SQL Server 2714; each other provider has its own wording).

**The fix has three layers, and none can be skipped:**

1. **Serialize the whole setup across processes**: `CrossProcessLock` (opening a file with `FileShare.None` to take an
   advisory lock, which the OS releases when the process ends; unlike a named mutex it cannot be left abandoned). If it
   cannot get the lock it waits, and after a timeout it **lets the setup through instead of hanging** (fail open),
   because each step is still fault tolerant on its own.
2. **Decide conflicts by looking at the database, not the error code**: "table did not exist before the action, table
   exists after the action" means another process built it, and it counts as benign. **Do not match each provider's
   error codes / messages**; that means maintaining 5 lookup tables, and they drift.
   The same goes for seed row races: rerun the probe once, and if the winner's rows are visible, adopt them.
3. **A single failed step must not abort the whole setup**: each table / each seed step records its own failure and
   **carries on**. Once, a single `CREATE TABLE` conflict made the entire seed skip, and the symptom only blew up dozens
   of tests later as `User not found.` / `Cannot resolve user rowid`, which looked nothing like a setup problem.

**A database that can be connected to but has no seed user throws immediately.** That is the one hard indicator that
"setup did not complete", and it must be raised while the cause is still at hand. An individual step failing (for
example a log table that cannot be upgraded) only costs its own tests. It does not throw, but it prints the full
exception with a `!!!` prefix.

> **`catch (Exception)` was the root of all this**: it made "setup failed" look like "setup completed" in the log.
> The only tolerable exception is `DbException`, and only in the single case "container not running → skip the whole
> DB".

#### 3b. Seed rows

Idempotency by per-table `SELECT COUNT(*)>0 then skip` **is not atomic across processes**: two processes both see 0
and each inserts once. Tables with a unique business key (`sys_id`) protect themselves because the unique conflict
makes the loser throw; **tables without a unique business key get seeded twice**.

**The fix**: wrap the entire seed in **a single transaction**, and change the gate to check whether **the first table
with a unique `sys_id`** already has rows. The winner commits the whole set atomically and the loser rolls back;
because of transaction isolation, other processes only ever see one of two states, "empty" or "complete".
