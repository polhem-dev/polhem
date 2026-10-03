---
name: polhem-framework-review
description: A repeatable methodology for a "full health check" of the polhem framework. Read-only review across eleven dimensions (architecture layering, dependency layering and cycles, security, maintainability, scattered/unnecessary classes, serialization consistency, public API surface, test quality and coverage, documentation drift, performance/hot paths, concurrency and global state), scanned per dimension by parallel subagents (exhaustive, not sampled), cross-deduplicated and consolidated into a graded (P0~P4) refactoring plan plus a 10-point score per dimension. Includes a concrete checklist per dimension, known pitfalls, and "should stay clean" baseline items (for regression detection). Use it when the user asks for a "full framework health check", "architecture review", "architecture health check", "framework health", "framework review", "full review", "architecture audit", "score/rate the framework", "are there scattered or unnecessary classes", "propose a refactoring plan" and similar; trigger it proactively for such whole-framework review requests even if the user does not say "health check". **The target is the framework code under `src/`**; if the user wants to check the configuration corpus (CLAUDE.md / rules / skills), that is a separate configuration audit, not this skill. **This skill only does read-only review, scoring and producing the refactoring plan; it does not change code** (fixes follow the normal workflow).
---

# Full health check of the polhem framework

Run a structured health check over the whole framework (every project under `src/`) and produce a **graded refactoring plan**
and a **10-point score per dimension**. The core method is **read-only scanning per dimension by parallel subagents**,
followed by cross-deduplication and consolidation.

## When to use / what it produces

- **Trigger**: the user asks for a full review / health check / architecture audit / scoring / finding scattered classes /
  a refactoring plan.
- **Output**: one report per dimension plus a graded refactoring plan (graded findings + execution order) and a score
  table in the conversation. **Every report goes to a durable location the moment it is written**: each subagent
  writes its own report into `local/internal/health-<review-date>/<dimension>.md`, and the plan goes to
  `local/plans/`. Never leave reports in the scratchpad (see the 2026-09-26 lessons). `local/` is ignored by git:
  **never commit the report** (never `git add -f` it), because it can list unfixed security issues. If it lists unfixed
  vulnerabilities, keep those details in `local/internal/` instead. If the user only wants a verbal conclusion you can
  skip writing the file, but writing it is the default (in line with the "Plan before you build" section of
  `.claude/CLAUDE.md`).
- **Discipline**: **read-only throughout; change no code.** Fixes are a separate later step, decided by the user after
  reviewing the plan.

## Ask first (use AskUserQuestion)

Ask each question with explicit options and mark the recommended one:

1. **Extra dimensions** (multi-select): the default eleven dimensions are covered; ask whether to add more (for example
   cross-platform trim/AOT, i18n coverage).
2. **Scope** (single): every `src/` project (recommended) / core backend only (excludes the UI heads Avalonia/Blazor.Server;
   highest density of refactoring signals) / including apps+samples+tools.
3. **Mode** (single): read-only review + plan document (recommended) / verbal report only / multi-agent workflow deep scan
   (requires the user's explicit consent to large-scale orchestration).

## The eleven dimensions

| # | Dimension | Core question |
|---|------|---------|
| 1 | Architecture layering | Is N-Tier + Clean Architecture + MVVM applied consistently; is the Domain Core pure |
| 2 | Dependency layering and cycles | Any cycles, reverse dependencies, cross-layer detours, Server depending on Client |
| 3 | Security | Encryption pipeline, Session/Token, SQL injection, XXE, randomness, resource disposal, access control |
| 4 | Maintainability | Naming consistency, comments, one type per file, culture in identifier comparison, large files |
| 5 | Scattered/unnecessary classes | Grab-bags, pure facades, `*Func` leftovers, multiple types per file, dead code |
| 6 | Serialization consistency | Explicit wire registration, Plain/JSON/MessagePack carrying the same values, the `object` value envelope, decoder limits, trim/AOT |
| 7 | Public API surface | Contract-axis namespace consistency, BO interface purity, breaking-change surface, four-layer alignment |
| 8 | Test quality and coverage | Ineffective assertions (S2699), coverage gaps, fixture pollution, flaky tests, `[Collection]` serialization |
| 9 | Documentation drift | Public doc claims vs actual code, dead links, bilingual gaps, breaking changes missing from CHANGELOG |
| 10 | Performance/hot paths | Uncached per-request reflection, serializer options rebuilt, wire shape cost, collection lookup complexity |
| 11 | Concurrency and global state | Shared cache instances mutated, process-wide static, bare collections, DI lifetimes |

> Dimensions 1 and 2 overlap heavily; one agent can cover both, but score them separately.
>
> **Dimensions 10 and 11 are standing items since 2026-08-07.** On their first measurement they were the two lowest of
> the eleven (performance 6.0, concurrency 7.0), but that is **the first baseline, not a regression**; regression only
> means something from the next round on. What the two share is "build green, tests green, scanners silent; it only
> shows up under a profiler or under load", the same class as documentation drift: "no automated mechanism will find it".
>
> **Dimension 9 is a standing item since 2026-07-28**; before that it was scattered as P3 items across the other
> dimensions. Its first independent score was the lowest of nine (4.5/10). Being diluted by high structural scores is
> exactly why it needs its own score: documentation drift does not turn the build red and does not fail tests; it only
> makes external developers write code that does not compile, and no automated mechanism will find it.

## Method: parallel subagents per dimension

Dispatch **10 `general-purpose` subagents** (in the background, in parallel), one per dimension (architecture and
dependencies together, one each for the rest). Each agent:
- Is **strictly read-only on the repository**; the prompt states explicitly "you must not modify any repository file;
  the only file you write is your own report", and names the report path under `local/internal/health-<review-date>/`.
- Receives **the full checklist for its dimension + known pitfalls** (see below) + a summary of the relevant rules
  (distilled from `.claude/rules/`; do not tell the agent to go read the whole set itself).
- Reports in a uniform format: three levels, each item with `project/file:line`, the problem (WHY) and a recommendation.
- Uses grep/glob for **exhaustive scanning, not sampling**; must list the **complete set** ("there are some" is not
  accepted).

After all reports are in, the main agent **cross-deduplicates**: a finding reported independently by two agents gets
higher confidence and higher priority (in the 2026-08-07 round, a collection comparer bug was confirmed by both the
maintainability and the serialization agents).

> This uses ordinary subagent delegation (not billed large-scale workflow orchestration). Only if the user chooses the
> workflow deep scan do you switch to the Workflow tool.

## Division of labour with the CI build gate (especially code style: do not rescan)

The health check is a **semantic/structural review**, not a format checker. Whatever the `build-ci.yml` strict build
already blocks, the health check **does not rescan**; a green build is itself the proof, and rerunning adds no signal:

- Pure formatting (indentation / LF / UTF-8 without BOM / `using` ordering; managed by `.editorconfig`).
- Analyzer rules already enforced by `.editorconfig` (CA1052/CA1822/IDE0044/IDE0051/CA1725/CA1305/CA1861… see the
  "already enforced, no longer listed" list at the top of `sonarcloud.md`).
- Nullable/CS warnings under `TreatWarningsAsErrors=true` (if it does not compile, it cannot get into a PR).
- Issues SonarCloud already scans automatically.

The health check **only verifies semantic code-style rules that machines cannot enforce** (already part of dimension 4,
maintainability): the **semantic choice** of `Ordinal` vs `CurrentCulture` for identifier comparison (CA1305 only covers
`IFormatProvider`, not `StringComparison`), one type per file, **per-folder exceptions** to folder↔namespace (IDE0130 is
a global rule and cannot be relaxed per folder, so the prompt layer guards it), where static utilities belong
(path A/B/C/D), grab-bags / pure facades, the deprecated `*Func`/`*Helper` naming, WHY-not-WHAT comments and manual S125
judgement.

> In one sentence: **formatting and hard analyzer rules belong to the build gate; semantics and structure belong to the
> health check. There is no separate "code style dimension": the machine handles one half, and the other half is in
> maintainability.**

## Checklist per dimension (paste these when dispatching agents)

### 1+2. Architecture layering and dependencies
- Read `docs/en/architecture/dependency-map.md`, `docs/en/architecture/architecture-overview.md`, `docs/en/architecture/development-constraints.md` to build the
  baseline.
- Extract `<ProjectReference>` from every `.csproj`, draw the actual dependency graph, and verify **no cycles** with a
  topological sort.
- Verify hard constraints: the BO (`Polhem.Business`) has **no** `Polhem.Db` reference; the backend
  (AspNetCore/Hosting/Business/Repository/Db) has **no** `Polhem.Api.Client` reference (note that
  `Polhem.Web.Blazor.Server` is a front-end RCL, so its Api.Client reference is correct); the Repository abstraction
  (`Polhem.Repository.Abstractions`) is not bypassed; `Polhem.Api.Contracts` is not polluted by implementations.
- Look for: god projects (overloaded responsibilities vs breadth of responsibilities; a large line count does not mean
  it should be split, look at cohesion), a Domain Core carrying infrastructure responsibilities, gaps between the
  documented dependency graph and the actual csproj files.

### 3. Security (rule sources: `.claude/rules/security.md` + `.claude/rules/scanning.md`)
- SQL: always the `{0}` placeholders of `DbCommandSpec`; grep `$"...SELECT/INSERT/UPDATE` and SQL built with
  `string.Format`; identifiers must be escaped via `QuoteIdentifier`.
- Encryption: AES-CBC-HMAC (256-bit + SHA-256 + random IV); HMAC uses the constant-time
  `CryptographicOperations.FixedTimeEquals` (not `==`); the payload pipeline serialize→compress→encrypt must not be
  reordered; access validation must happen **before decryption**.
- Randomness: security uses always `RandomNumberGenerator`; `System.Random` is forbidden.
- XXE: parsing untrusted XML requires `DtdProcessing.Prohibit` + `XmlResolver=null`.
- Exceptions: no `catch(Exception)` on base types, no empty catch, no `throw ex;`; exceptions/logs must not leak
  keys/tokens/passwords/stack traces/internal paths.
- Resources: `IDisposable` uses `using`; no scattered manual `.Dispose()`.
- Access control: do all externally exposed methods have an appropriate `[ApiAccessControl]`; is the unannotated case
  fail-closed (deny rather than allow); does the default validator actually verify the key value (not only that it is
  non-empty). **What an action name can reach** is decided by `JsonRpcMethod.IsResolvableAction` (Polhem.JsonRpc.Server); check that
  POLHEM3001 and the runtime agree on it (the 2026-09-26 round found accessors and static methods reachable while the
  analyzer assumed they were not).
- Anonymous surface: everything reachable before authentication (the decoder, anonymous actions, replay protection)
  is a denial-of-service surface; `AnonymousApiSurfaceTests` pins the anonymous action list.
- Hardcoding: keys/certificates/passwords in connection strings; MD5/SHA1 used for security hashing; whether
  the `none` encryptor (`NoPayloadEncryptor`) can be enabled outside debug.

### 4. Maintainability (rule sources: `.claude/rules/code-style.md` + `.claude/rules/sonarcloud.md`)
- **Culture-sensitive comparison misused on identifier strings** (high-value check): grep `CurrentCultureIgnoreCase`,
  `CurrentCulture`, `.ToLower()`/`.ToUpper()` (without Invariant). Collection keys, field names, ProgIds, type names and
  delimiter splitting always use `Ordinal`/`OrdinalIgnoreCase`/invariant; Turkish-I is a correctness risk.
- Leftover `*Func` static classes (the rules say they were all removed on 2026-05-01; verify there is no regression).
- One type per file: grep for files with several `public class/interface/enum` (especially an interface and its
  implementation in the same file).
- Folder↔namespace consistency (IDE0130); large files (> 500 lines) as split candidates.
- Naming violations: Hungarian prefixes, `*Helper` suffix, parameters not in camelCase.
- Commented-out old code (S125), empty classes (S2094), private nested classes not sealed (S3260).

### 5. Scattered/unnecessary classes
- Grab-bags / god classes (names containing Helper/Utils/Common/Misc/Manager with divergent content).
- Pure facades / 1-line delegation wrappers (no added value; DI abstraction seams are an exception).
- Dead code: **non-public** APIs with 0 callers, `[Obsolete]` with no callers (the framework's public API surface is kept
  even with 0 callers; only pure BCL wrappers are deleted).
- Duplicate implementations (S4144): identical logic in several places should be merged.
- **Clarification trap**: the `ExecFunc*` family is a domain type (the JSON-RPC "execute function" pattern), not a
  deprecated `*Func` static class; do not report it.

### 6. Serialization consistency (rule sources: `.claude/rules/serialization.md`, `src/Polhem.Api.Core/CLAUDE.md`, the `polhem-serialization` skill)
- **Three wires, one set of values**: `Plain` (the envelope's System.Text.Json), the JSON body codec and MessagePack
  (the codec is negotiated per request, adr-044; undeclared means MessagePack). Look for a value that survives one wire
  and changes or fails on another, above all `object`-typed members (`Parameter.Value`, `FilterCondition.Value`) and
  `DataTable` rows. Measure the suspicious ones: in the 2026-09-26 round the Plain path turned a valued filter into a
  `JsonElement` and a `DataTable` into an empty table, and only a probe proved it.
- **Explicit registration**: every wire type is registered in `WireContracts.*.cs`. The gates are
  `WireContractDriftTests` (registration and member list), `WireCodecParityTests` (every contract through both codecs,
  member by member), `WireDefaultOmissionTests` (non-default initializers are always written). Check that each gate is
  not vacuous (canaries, lower bounds), and look for what it cannot see: hand-written formatters outside
  `WireContract`, closed generic instantiations in `WireContracts.Generics.cs`.
- **The `object` envelope**: `WireValueFormatter`'s closed set, the named-type table and the escape hatch. The
  allow-list (`WireTypeWhitelist`) must be applied symmetrically on both ends and both codecs; the escape hatch needs
  dynamic code, so it fails on iOS.
- **Decoder limits**: nesting depth (`MessagePackCodec.MaxObjectGraphDepth`, the depth steps inside hand-written
  formatters such as `FilterNodeFormatter`, `MessagePackDepthLimitTests`) and sizes. The decoder runs before
  authentication, so a missing limit is an anonymous denial of service.
- **Cross-language contract**: `wire-contracts/messages.d.ts` and `wire-fixtures/` are the authority
  (`WireFixtureTests`, `WireContractGeneratorTests`); a wire change without a diff there is a finding.
- **Definition types**: no transport attributes in `Polhem.Definition` (adr-036); collections inherit
  `KeyCollectionBase<T>` / `CollectionBase<T>`; empty collections are omitted by get-only `XSpecified`, not
  `ShouldSerializeX`; serializing a cached instance has no side effects (`CachedDefinitionSerializationTests`); the
  reflection-only `XmlSerializer` shape rules (`XmlSerializerShapeGateTests`, POLHEM4005, POLHEM4006).
- **Trim/AOT**: which test projects the CI AOT gate (`DynamicCodeSupport=false` in `build-ci.yml`) covers, and which
  wire paths none of them exercise; the supported trim modes and `POLHEM9004`; the trim descriptors
  (`ILLink.Descriptors.xml` in `Polhem.Definition` and `Polhem.Expressions`, `TrimmerDescriptorGateTests`).
- Newtonsoft.Json leftovers (should be 0).

### 7. Public API surface
- Contract-axis namespace↔folder consistency (`Polhem.Api.Contracts.{System,Form,AuditLog}` ↔
  `Polhem.Api.Core.Messages.{...}`).
- **BO interface purity**: `I<Axis>BusinessObject` only holds methods that other BOs call; pure API methods
  (Ping/GetFormSchema/GetFormLayout/GetLanguage) are public on the concrete class only, with `[ApiAccessControl]`, and
  are not on the interface.
- Spot-check four-layer alignment: wire DTO (`XxxRequest/Response`) ↔ contract interface (`IXxxRequest/Response`) ↔ BO
  implementation ↔ Client connector; names and signatures consistent, no orphans.
- Breaking-change surface: public mutable fields, publicly exposed concrete collection types (external contracts should
  narrow to `IReadOnlyList`), implementation types that are public but should be internal.
- Dead-path infrastructure: mechanisms with zero registrations/zero calls in production that still sit on a hot path
  (mark them as "reserved" rather than wrongly assuming they are in effect).
- `///` XML doc comments: missing on public APIs, or written in Chinese (should be English).

### 8. Test quality and coverage (rule source: `.claude/rules/testing.md`)
- Ineffective assertions (S2699): `[Fact]/[Theory]` without `Assert.*`; verifying "no exception" requires
  `Record.Exception` + `Assert.Null`. **Note**: delegating to an asserting helper (such as `AssertXxx`, `TestFunc`) is
  legitimate; do not report it. Read suspicious files to confirm.
- **Hollow round-trips**: does a serialization test helper only `Assert.NotNull` without comparing the restored values
  (a false green that does not match its name)? The correct template is `tests/Polhem.Api.Core.UnitTests/TestFunc.cs`.
- Coverage gaps: compare file counts in src vs tests; security logic (encryption/hashing), triple-format serialization
  round-trips and public BO methods must be tested; look for critical paths with zero coverage, such as `In` over
  MessagePack.
- Fixture pollution: `SaveDefine`-family tests must switch to a temp directory; writing to `tests/Define/` is forbidden.
- `[Collection]` serialization (every collection is a `const` name on a definition class, per `tests/CLAUDE.md`;
  `grep -rn '\[Collection("' tests --include='*.cs'` finds literals that crept back), tests modifying production
  statics, real wall-clock flakiness (recommend `TimeProvider` + `FakeTimeProvider`).
- Database tests that CI would silently skip: `RequiredTestDatabaseGateTests` must still match the workflow's
  database list.

### 9. Documentation drift (rule source: `.claude/rules/public-docs.md`)

The scope is **public documentation** (written for NuGet package consumers): the repository-root `README*` /
`CHANGELOG*`, `docs/README.md` and every `.md` under `docs/<lang>/`, and `README.md` /
`README.zh-TW.md` **in every location** (`src/` `samples/` `apps/` `tools/`).
`local/` (plans and internal notes), `maintainers/` (the ADRs included) and `.claude/` are not public documentation.

- **Compilability (highest value)**: check every type name, method name, DI extension method and enum member in the
  docs against the source code: does it exist, with matching case? Prioritise the passages external developers copy on
  day one (cookbook, quick start, README samples). **No automated mechanism catches this class of error.**
- **Distorted design records**: does the mechanism an ADR describes still exist (a type renamed / removed but still
  described as the current mechanism); do the hard constraints in `development-constraints` still hold; are overturned
  ADRs marked `已取代(Superseded)`.
- **Reverse index for renamed / deleted types**: each time a public type is removed or renamed, has
  `grep -rn "<old name>" --include="*.md"` been cleaned up? This is the source of about sixty percent of the problems
  in this dimension.
- **Dead links**: check all relative links and anchors exhaustively. Watch in particular for batch errors in
  **subdirectories** such as `docs/<lang>/changelogs/*.md` that wrongly use repository-root relative paths (resolved from a
  subdirectory they gain one extra level, and all 404 on GitHub).
- **Bilingual sync**: compare section structure and amount of content in bilingual pairs; find one-sided updates; find
  documents that should be bilingual but exist in only one language.
- **CHANGELOG**: the `Version.props` version vs the commits after the tag; are all commits marked `!` recorded;
  is there an Unreleased section; have existing statements been overturned by later commits.
- **Quantitative baselines**: project counts / dependency edges / package lists claimed by docs vs the actual csproj
  files.

### 10. Performance/hot paths

**Identify the hot paths before looking for problems**, and require each finding to state "how often this code runs"
and to trace the callers to prove it. Anything not proven to be on a hot path drops to P4: an O(n²) that runs once at
startup does not matter; running per request is a problem.
**Do not report speculative micro-optimisations without measurements** ("could use span here").

Main hot paths: the API request pipeline (decrypt→deserialize→validate→dispatch→serialize response), session/token
validation (**2–5 times per request**), definition lookup (**6–10 times per request**), permission layer-1/layer-2,
FormSchema-driven CRUD, the MessagePack wire (**per row × per column**), expression evaluation
(**per row × per computed column**), Grid cell materialisation (per visible cell).

- **Uncached per-request reflection**: `GetType().GetMethod` / `GetProperties` / `GetCustomAttributes` /
  `Activator.CreateInstance` on the request path with no `ConcurrentDictionary` cache. The classic ailment of a JSON-RPC
  reflection-dispatch framework.
- **Serializer options rebuilt on every call** (the P0 of 2026-08-07): `JsonSerializerOptions` is STJ's type-contract
  cache container, and a per-call `new` makes the cache miss 100% of the time. **Control group**: in the same repository
  `MessagePackCodec.Options` is `static readonly`; one side done right and the other not is usually a leftover of a
  mechanical translation during a migration.
- **Wire shape cost**: the DTO shape multiplies every transport cost. Look for "the same data sent twice" (such as
  Unchanged rows sending both Current + Original), "column names repeated per row" (`Dictionary<string,object?>` instead
  of an array in the same order as columns), "per-cell typeless dispatch" (`Guid`/`decimal` write an ext header with the
  full type name).
- **Collection lookup complexity**: `FirstOrDefault(f => f.FieldName == x)` inside a per-row loop is O(n·m).
  **Always actually read the implementation of the framework's most central collection base** (is `KeyCollectionBase<T>`
  really O(1), is there a `dictionaryCreationThreshold` trap); if it is wrong, the whole framework is affected.
- **Per-request object allocation**: uncached `new XmlSerializer(type)` (a classic severe leak), `HttpClient` created
  every time, `Regex` not static/compiled, encryptors created every time.
- **Cache design**: is lookup O(1), lock scope, **unbounded growth** (`MemoryCache` without `SizeLimit` + negative
  caching = unauthenticated requests can drive memory up).
- Synchronous blocking on async (`.Result` / `.Wait()` / `GetAwaiter().GetResult()`), N+1 queries, unconditional
  compression of small payloads (gzip has a fixed overhead of 18 bytes and can be larger than the original).

### 11. Concurrency and global state (rule source: the cache immutability section of `.claude/rules/definition.md`)

**Every finding must prove it is reachable concurrently**: a static set once at startup and read-only afterwards is
safe; trace every write site.

- **Shared cache instance mutated** (highest value; an explicit hard constraint of the framework): trace every call site
  of `IDefineAccess.GetX(...)` exhaustively, and confirm for each whether the object obtained is written to afterwards
  (property assignment, collection Add/Remove, mutation of child objects). Only safe with `Clone()`.
  Serializing a cached instance used to count as mutation (per-object serialize state, removed before Polhem 1.0);
  confirm nothing has brought back state that serialization writes onto the value. Look for "the guard only covers
  some types": on 2026-08-07 the review found that the only
  implementer of the `ISerializableClone` guard in `SerializeDefine` was exactly the one type that **did not need** it
  (server-only), while every definition type that actually goes over the wire bypassed it, and the XML doc claimed it
  was protected, **which is more dangerous than no protection at all**. (That interface was removed the same day; it is
  kept here to remember **the way to look**, not so the next round goes looking for that type.)
- **Process-wide mutable statics**: grep exhaustively for static fields that are neither `readonly` nor `const` and for
  static properties with setters, and judge for each whether it is written on the request path. Look in particular for
  "per-user state of a client-side library kept in a static": it holds for desktop heads, but the assumption breaks
  when the same code is consumed by a **multi-user process** such as Blazor Server, and **nothing marks this boundary**.
- **Non-thread-safe collections as shared state**: bare `Dictionary`/`List`/`HashSet` as static or singleton members.
  **The test layer counts too**: production being read-only after startup does not mean tests will not read and write
  concurrently (registry types are the most common case); this is the typical root cause of "green locally, red in CI".
- **Lock design**: IO/DB inside a lock, nested lock ordering, `lock(this)`/`lock(typeof(X))`, double-checked locking
  missing `volatile`.
- **Async correctness**: `async void`, sync-over-async, libraries missing `ConfigureAwait(false)` (judge whether the
  consumer has a SynchronizationContext), fire-and-forget swallowing exceptions.
- **DI lifetime mismatch** (captive dependency): a Singleton injected with Scoped/Transient.
- `[ThreadStatic]` failing under async flow, `event` subscription leaks (static event + never unsubscribing =
  cross-container pollution), static ctor throwing (S3877), ADO.NET objects stored on a singleton.

## Consolidation and output

### Grades (P0~P4)
| Grade | Meaning |
|----|------|
| **P0** | Correctness/functional risk (wire deserialization failure, culture bug in identifier comparison, false-green tests) |
| **P1** | Security and serialization-attribute consistency (mostly low-risk batch fixes) |
| **P2** | Structural refactoring (splitting responsibilities, large files, multiple types per file, marking dead paths) |
| **P3** | Documentation drift (dependency graph, contract docs, distorted rule statements); low cost, high value |
| **P4** | Observations / awaiting the user's decision (convention exemptions, minor extra tests) |

Each item has `file:line`, the problem (WHY), a recommendation and a severity. At the end of the document add a
"items scanned as clean" list (for future regression detection) + a "recommended execution order".

### Scores (10-point scale per dimension)
After consolidation, give a score table: one score per dimension + the main deductions (mapped to finding numbers) +
a weighted overall average. Scoring logic:
- **9+**: zero accumulated technical debt in that dimension, all rules followed, only doc-level or very few flaws.
- **7~8.5**: solid foundation but clear, fixable consistency gaps.
- **6~7**: pulled down by concrete functional/correctness bugs (not mechanism design problems).
- State "after fixing which P0/P1 items it can climb back to what score", giving the user a path to a higher score.

## Plan document format
Follow the plan convention in the "Plan before you build" section of `.claude/CLAUDE.md`: a single status line at the
top, `**狀態:📝 擬定中(YYYY-MM-DD)**` (Status: 📝 drafting), + a multi-phase (P0~P4) phase table. Link the written file
in the reply.

## Methodology lessons (cumulative; reuse them next time)

### Learned in the 2026-09-26 round

**F. Write every report to a durable location the moment it exists.** The subagents wrote their reports to the
scratchpad, and the scratchpad was cleared before the round was consolidated. The reports had to be rewritten from the
agents' replies without rescanning, two rewrites were blocked and had to be pieced together by the main session, and
the probe projects behind the measured findings were lost with it, so the numbers in the reports are now their only
record. **Each subagent writes its own report into `local/internal/health-<review-date>/` and keeps its probe
projects under `local/`; the scratchpad is for throwaway files only.**

**G. A finding is a pointer, not a specification: re-verify it against the code before fixing it.** The fixes ran as
a series of batches, and by the time a later batch reached a finding, earlier batches had often fixed it, fixed part
of it, or renamed the types it named. Every fixing agent re-greps and re-reads the code before editing, and reports
"already fixed" as a result in its own right, instead of copying the finding's claim into the code or a document.

**H. Parallel fix batches collide on shared files and shared names.** Batches that run at the same time in separate
clones conflict when they touch the same files, and a rename or move in one batch leaves stale names in files another
batch is editing. Draw batch boundaries by file ownership, land renames and moves before anything that documents
them, and run the documentation batches last, starting each with a grep for the old names.

### Learned in the 2026-08-07 round

**A. "Marked done" needs an independent re-verification step.**
The previous round marked the external package table in `dependency-map` ✅, but the diff of that commit only touched
the mermaid diagram and the prose; **not one line of the table changed**. Likewise the CHANGELOG gap for `IExcelHelper`
was written up as "closed" in three repository docs while it had not actually been filled.
**Fixed first step of the next round: re-verify, one by one, every item the previous round claimed as fixed**; do not
trust status markers.

**B. The shape of the scan target determines the blind spot.** This round the scattered-classes agent found the 71
`TreeNodeAttribute` annotations but missed the 579 `[Category]`/`[Description]`/`[Browsable]` annotations, 7 times larger,
because it scanned "does the **type** have callers", and those are BCL attributes outside the scan target.
**Ask every round: what is the scan unit of this dimension? What shape of problem becomes invisible because of it?**

**C. A higher score does not mean fewer problems; require agents to break down the attribution.** This round
serialization's +1.5 came mainly from the failure mode moving from silent to blocked at compile time (a new analyzer);
of documentation's +1.5, about 1.0 was real improvement and 0.5 was structural progress reflected in "the scan went
deeper and it still rose". Without the breakdown you cannot tell "fixed" from "not seen this time".

**D. Guard mechanisms have inherent blind spots; building one does not mean coverage.** The public API snapshot (the
highest-leverage recommendation of the previous round) was built and works, but at the moment it was built it ratified
as "shipped", unchanged, a broken API (`ExecFuncLocal`) that had been broken for 10 months.
**A snapshot guards "do not change"; it cannot guard "was wrong to begin with"**: when establishing a baseline, do a
separate correctness check.

**E. When two agents report the same problem, their severity judgements can differ by two grades.** This round
`SerializeDefine` was rated P1 by the concurrency agent and P3 by the serialization agent. **A cross-hit raises
confidence that it "exists", not confidence in "how severe"**: severity must be settled by reading the source yourself.

### Learned in the 2026-07-28 round

**1. Write baselines as concrete lists, not unverifiable assertions such as "dead code 0".**
The previous baseline claimed "empty classes 0, dead code 0"; this round found at least 15 zero-use types, **all older
than the previous health check**. The previous judgement was too optimistic (it probably only scanned files with no
references at all, and did not follow false-positive survivors of the "declaration + DI registration" or
"declaration + placeholder test" kind). **Placeholder tests make dead code appear as tested in coverage reports.**

**2. A falling score usually means a deeper scan, not a regression in the code, but you may only say so after verifying
each item with git.**
Require each agent to use `git log`/`git show` to find when each "new finding this round" was introduced, separating
"regression" from "existing problem scanned for the first time". The two have different priorities, and mixing them
misleads the user.

**3. P0 findings are worth the cost of measurement.**
This round "the content of definition-type responses is completely wiped out on the wire" was at first only a
theoretical inference. Only after building a standalone console project in the scratchpad (ProjectReference to the
target package, going through the **public** serialization entry point) and measuring it was the failure mode pinned
down as **a silent empty shell rather than a thrown exception**, and the choice of fix depended on exactly that answer.
Note that `MessagePackCodec` is internal; external probing must go through `MessagePackPayloadCodec`.

**4. Cross-check agents' conclusions, especially any judgement that "this is dead code".**
This round an agent marked the `LocalOnly` check in `ApiAccessValidator` as dead code, when actually only the second
half of the condition was redundant and the mechanism itself is effective; accepting it wholesale would have wrongly
concluded that the protection does not work. Likewise a recommended fix can rest on a wrong premise (treating a
definition type as a wire DTO); **before sending a recommendation, confirm the serialization contract of that type**.

## Known baseline (results of the last health check, for regression comparison)

Last run **2026-09-26**: the first round on the Polhem repository, before the 1.0.0 release (the earlier rounds ran on
Bee.NET, `jeff377/bee-library`; the Bee.NET 4.17.0 numbers are kept as a record). Eleven-dimension average about
**7.1**. The deeper scan (first measurements of the JSON and Plain wires, first compile check of every `docs/en`
sample) explains most of the drop, not a code regression. **These scores were taken before the pre-1.0 fix batches**;
the next round measures the fixed code and should expect them to rise.

| Dimension | 2026-07-28 (Bee.NET) | 2026-08-07 (Bee.NET) | 2026-09-26 |
|------|-----------|-----------|-----------|
| Architecture layering | 8.8 | 8.6 | 8.5 |
| Dependency layering | 9.2 | 9.0 | 9.0 |
| Security | 7.8 | 7.0 | 6.0 |
| Maintainability | 8.5 | 8.5 | 7.0 |
| Scattered/unnecessary classes | 7.5 | 7.0 | 6.8 |
| Serialization consistency | 7.0 | 8.5 | 6.5 |
| Public API surface | 8.5 | 8.5 | 7.5 |
| Test quality and coverage | 8.2 | 8.5 | 7.4 |
| Documentation drift | 4.5 | 6.0 | about 5.5 |
| Performance/hot paths | — | 6.0 (new) | 7.0 |
| Concurrency and global state | — | 7.0 (new) | 6.5 |

The 2026-09-26 round also scored three extra dimensions: release readiness 7.5, trim/AOT 6.5, i18n 5.0.

**Should stay clean** (going from clean to not clean is a regression; flag it red and prioritise it). **Compute every
quantity from the code at review time; never compare against a number carried over from an earlier round** (the 30
dependency edges recorded for Bee.NET no longer matched after legitimate moves, and comparing against the old number
would have reported a regression that was not one):

- Dependencies: no cycles (topological sort of every `<ProjectReference>` in `src/`, leaving out the build-ordering
  references to `Polhem.Analyzers`); the mermaid graph in `docs/en/architecture/dependency-map.md` matches the csproj files edge for
  edge; BO has no Db reference; the backend has no `Polhem.Api.Client` reference; the Repository abstraction is not
  bypassed; `Polhem.Api.Contracts` has no implementation types.
- Code: `*Func` leftovers 0, `*Helper` types 0, Newtonsoft 0, `[Obsolete]` 0, `CurrentCultureIgnoreCase` 0,
  `new DateTime(` without Kind 0, `Regex` without timeout 0, public mutable fields 0, files over 500 lines only where
  the type has no seam (`FormField`, a property bag).
- Wire: every wire type registered and the gates non-vacuous (`WireContractDriftTests`, `WireCodecParityTests`,
  `WireDefaultOmissionTests`); contract axes aligned across the four layers.
- Security: SQL injection 0 (all values parameterised, all identifiers escaped), XXE 0, `new Random(` 0, hardcoded
  secrets 0, MD5/SHA1 for security 0, bare manual `Dispose` 0, `throw ex;` 0.
- Tests: S2699 0, fixture pollution 0, wall-clock flakiness 0, `[DisplayName]` on every test, `[Collection("` string
  literals 0.
- Docs: `./check-md-links.sh`, `./check-public-docs.sh` checks (1) to (3), `./check-docs-i18n.sh` and
  `./check-xmldoc-refs.sh` all clean (the Docs Check workflow runs them); XML docs with zero Chinese and zero `<param>`
  mismatches.
- Concurrency and performance: DI captive dependencies 0, `async void` 0, `Task.Run` wrapping synchronous code 0,
  per-row LINQ linear field lookups 0, N+1 queries 0.

**⚠️ Read this paragraph before reading the list.** The 2026-08-07 health check tripped here once: it saw the
struck-through dead-code list in the previous round's plan and re-listed "items not on the deleted list" as "missed
cleanups" to do (D-1), but in the previous round those items had actually been **deliberately kept after a decision**;
the decision was written in another table **below** the struck-through list, and the scan did not read it.
**"Not deleted" does not mean "not yet handled".** Next round, keep the two categories below separate.

**(a) Deliberately kept: decided, not dead code, do not list as to-do again**:

| Item | Reason kept |
|------|---------|
| `TreeNodeIgnoreAttribute` (together with `TreeNodeAttribute` / `IDisplayName`) | Re-judged as "a design not yet wired up"; the tree view builder that consumes it is separate future work |
| The designer attributes on definition types: `[Description]`, `[Category]` (with the `PropertyCategories` constants), `[Browsable]`, `[NotifyParentProperty]`, `[TypeConverter(typeof(ExpandableObjectConverter))]` | Decided in the 2026-09-26 round: kept for the planned Avalonia property grid editor, which is to read them. Nothing reads them today, which is expected until that editor exists. `[DefaultValue]` is not in this row: `XmlSerializer` honours it |
| `IDefineField` | Implemented by `DbField`; an abstraction not yet consumed, not dead code |
| `IElementCapabilityResolver` | Its implementation `ElementCapabilityResolver.Default` (now in `Polhem.Api.Client`) is called by `LayoutCapabilityApplier`, `FormView.Commands` and `ListView.Commands`, and by DemoCenter |
| The `IUIViewService` seam | Kept by decision on 2026-08-07: it has no production implementation, but it is a documented host extension point (cookbook tutorial step / terminology entry / adr-013 argument / the family criterion in dependency-map) |
| `PermissionBindingValidator` | Decided 2026-08-07: keep the code and fix the docs instead; it is a validation API the host calls itself, not something that takes effect at load time |
| `DateTimeExtensions.GetYearMonth` | Zero production callers, but the BCL has no equivalent of "first day of the month" and it is not a pure wrapper; kept under code-style's "keep 0-caller framework public APIs" |
| The `I*Request` / `I*Response` interfaces of `Polhem.Api.Contracts` | Most have no consumer that uses them as a type (a few are pattern-matched, for example in `DateTimeWireGuard`); `ApiContractPairingTests` and `BusinessContractPairingTests` find them by reflection. They are the member contract shared by a wire message and its BO args / result: `ApiInputConverter` copies between the two by property name and skips a name that does not match silently, so implementing the interface on both sides is what makes the compiler keep their members in step. Not dead code, even where the only reference is the implementing type |

**(b) Removed** (a record, so the next round does not look for them):
- Before Polhem 1.0.0 (the 2026-09-26 fix batches; the commit messages and `PublicAPI.*.txt` diffs list the rest):
  the per-object serialize state (`IObjectSerialize`, `SerializeState`, `SerializationUtilities`); the tracing
  subsystem; `DateInterval`, `IPValidator`, `VersionInfo`, `DataTableComparer`, `DefaultBoTypeResolver`;
  `TableSchemaBuilder.Compare` and the legacy schema comparison path (`DbUpgradeAction`); the one-time session flag.
  Renamed: `IPolhemContext` → `IBusinessObjectContext`, `PolhemStringLocalizer` → `LanguageResourceStringLocalizer`,
  `NullAuditLogWriter` → `NullLogWriter`, the Log axis → `AuditLog*`.
- Earlier: the `CheckPackageUpdate` / `GetPackage` stack and `ApiErrorInfo` (Bee.NET, 2026-09-04 and before); the
  `ExecFuncLocal` public surface (2026-08-07); `IEnterpriseObjectService`, `EnterpriseObjectService`,
  `InitializeOptions`, `ApplicationType`, `SysFuncIDs`, `VersionFiles`, `DefaultBoolean`, `NotSetBoolean`,
  `SystemActions.GetLocalDefine`/`SaveLocalDefine`, `DateTimeExtensions.IsEmpty`.

**(c) Reserved identifiers**: POLHEM4001–POLHEM4004 are never reused (they were Bee.NET's BEE4001–BEE4004, and a
project migrated from Bee.NET maps its suppressions by number); `DiagnosticIds.ReservedIds` records them.

**Established guard mechanisms** (the next health check confirms each still exists, still runs in CI, and is not
vacuous; read the list from the code rather than trusting this one):
- API surface: `BoApiSurfaceTests` (baseline, `docs/<lang>/api/api-method-reference.md` entry by entry, replay
  protection), `ActionSurfaceTests`, `ConnectorSurfaceTests`, `ClientAsyncSurfaceTests`, `ApiContractPairingTests`
  (with `WireMessageTypes_IsNotEmpty`), `AnonymousApiSurfaceTests`, `ApiAccessControlPinTests`, and the public API
  snapshot (`PublicApiAnalyzers` + the `PublicAPI.*.txt` baselines + `maintainers/public-api-baseline.md` +
  `tools/scripts/gen-public-api.py`).
- Wire: `WireContractDriftTests`, `WireCodecParityTests`, `WireDefaultOmissionTests`, `WireFixtureTests`,
  `WireContractGeneratorTests`, `MessagePackDepthLimitTests`, `comparedCount > 0` in `TestFunc`.
- Definitions and trim: `XmlSerializerShapeGateTests`, `CachedDefinitionSerializationTests`,
  `TrimmerDescriptorGateTests`, the CI AOT gate (`DynamicCodeSupport=false`), `POLHEM9004` (unsupported trim/AOT
  configurations, `buildTransitive`).
- Boundaries: `POLHEM9001` with `DefinitionDependencyGateTests`, `ArchitectureBoundaryGateTests`,
  `BaseLayerCapabilityGateTests`; `POLHEM9002` (one version), `POLHEM9003` (definition files found).
- Analyzers: the POLHEM1xxx/2xxx definition rules (`FieldDbTypesSyncTests`, `DbCategoryScopesSyncTests` keep their
  tables in step with the runtime), POLHEM3001 and POLHEM3003 (access control), POLHEM4005 and POLHEM4006
  (collection shape).
- Tests and docs: `RequiredTestDatabaseGateTests`, `ReservedProgIdConstructionTests`, and the four `check-*.sh`
  scripts in the Docs Check workflow.

**The highest-leverage improvement recorded by the previous round is done**: `BoApiSurfaceTests` checks the baseline
against the method reference, `ActionSurfaceTests` resolves every action constant to a BO method, and
`ConnectorSurfaceTests` maps each action to its connector method. Pick the next one from the round's own findings;
do not carry this line forward unchanged.
