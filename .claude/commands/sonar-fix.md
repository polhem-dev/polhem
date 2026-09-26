---
description: Sweep SonarCloud issues and test coverage, fix them automatically and add tests (manual fix mode)
argument-hint: "[--mode=fix, the default and only mode]"
---

# Sonar Fix — quality sweep

Run a quality sweep of the SonarCloud project `polhem-dev_polhem` in fix mode (a manual local session with the
complete fix loop). `$1` may be omitted or given as `--mode=fix`.

State files: `docs/.sonar-fix-state/` (created on the first run)

## FETCH phase

The SonarCloud API allows anonymous reads for a public project, so the queries below need no token:

```bash
BASE="https://sonarcloud.io/api"
KEY="polhem-dev_polhem"

# 1. Quality gate
curl -s "$BASE/qualitygates/project_status?projectKey=$KEY"

# 2. Leak period open/confirmed issues
curl -s "$BASE/issues/search?componentKeys=$KEY&issueStatuses=OPEN,CONFIRMED&sinceLeakPeriod=true&ps=500"

# 3. Project-wide open/confirmed issues
curl -s "$BASE/issues/search?componentKeys=$KEY&issueStatuses=OPEN,CONFIRMED&ps=500"

# 4. Overall coverage
curl -s "$BASE/measures/component?component=$KEY&metricKeys=coverage,ncloc,bugs,vulnerabilities,code_smells"

# 5. Per-file coverage (sorted from low to high)
curl -s "$BASE/measures/component_tree?component=$KEY&metricKeys=coverage&qualifier=FIL&ps=500&s=metric&metricSort=coverage&asc=true"
```

> If the API adds restrictions on anonymous access in the future, set `SONAR_TOKEN` in `~/.zshrc` and add
> `-u "$SONAR_TOKEN:"` to the calls.

---

## Fix mode (manual, local session)

Fix mode uses `/loop` to repeat until an end condition holds.

### Initialization

1. FETCH the current issue list and coverage
2. Read `docs/.sonar-fix-state/skip.json` and filter out issue keys and file paths that have been given up on
3. Record the starting point: issue set, overall coverage, quality gate

### Main loop (each round)

```
/loop Repeat according to the following rules:

1. FETCH again (the first round uses the initialization result)

2. Check the end conditions (stop as soon as any holds):
   a. quality gate = OK/PASSED and overall coverage >= 90% and no unhandled BLOCKER/HIGH issue
   b. every remaining item to handle is already in skip.json
   c. 2 consecutive rounds with no progress at all (issue count and coverage both unchanged)

3. Decide what to handle this round (by priority, at most 5 items per round to keep the diff small):
   a. BLOCKER / CRITICAL / MAJOR issues (sorted by severity)
   b. MINOR / INFO issues
   c. Files with coverage < 70% (sorted by absolute gap)
   d. Files with coverage between 70-90% (sorted by absolute gap; only when overall < 90%)

4. Fix issues (maps to step 2 of the user's workflow):
   a. **Rule pre-filter (rule-level blocklist)**: the rules below are not fixed automatically. Write them directly
      into the `humanReview` block of docs/.sonar-fix-state/skip.json, with the component path
      and textRange, and wait for a human to tell them apart and handle them in the SonarCloud UI:
      - `csharpsquid:S125` (commented-out code): high false-positive rate on English WHY comments.
        An LLM has no reliable way to tell a "legitimate WHY explanation" from "code that really was commented out",
        so it goes to human review. If legitimate, mark it False Positive in the SonarCloud UI;
        if it really is dead code, delete it by hand
   b. Check against the rule tables in .claude/rules/sonarcloud.md and .claude/rules/scanning.md
      - An issue with no matching rule: add it straight to the skip list (reason: unknown rule)
   c. Keep an attempts counter for each issue (in memory, within this session)
   d. After applying the change, run:
      dotnet build --configuration Release --no-restore
      dotnet test <affected project>.csproj --configuration Release --settings .runsettings
   e. Verification passes → keep the staged change; fails → git restore, attempts+1
   f. attempts >= 3 → write to docs/.sonar-fix-state/skip.json (issues block), with the reason

5. Add coverage (maps to step 3 of the user's workflow):
   a. Add [Fact] / [Theory] following the naming rules in .claude/rules/testing.md
      - Naming: <MethodName>_<Scenario>_<ExpectedResult>
      - Add a [DisplayName] description (in the language .claude/rules/testing.md specifies)
      - Use [DbFact] when a DB is needed; [LocalOnlyFact] when a local service is needed
   b. **Never** modify existing test assertions, public API signatures, or csproj dependency versions
   c. Run the same build + test verification
   d. File coverage reaches the target (>= 90%, or a relative gain >= 20pp) → success
   e. attempts >= 3 → write to skip.json (files block), with the reason

6. If this round has staged changes:
   a. commit (message format:
      chore(sonar-fix): handle X issues, add coverage for Y files

      by /sonar-fix
      Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
   b. Get the commit to main as .claude/rules/pull-request.md describes
   c. Call /ci-watch to watch CI + the quality gate until they pass (do not implement this logic yourself)

7. Go to the next round, back to step 1

8. When finished, output a summary:
   - Which issues were fixed (key + rule + component)
   - Which files got tests
   - Skip list items added this time, with reasons
   - Overall coverage: starting X% → ending Y%
   - quality gate: start → end
```

### Safety and limits (mandatory)

- Automatic fixes only touch `src/`, never `samples/`
- Adding tests only adds files or `[Fact]`s under `tests/<Module>.UnitTests/`; existing assertions are not changed
- **Never** touch:
  - public API signatures (method signature, class visibility)
  - the encryption / session pipeline (`Polhem.Base/Cryptor/*`, `Polhem.Api.Core/Session/*`)
  - dependency versions in csproj / Directory.Build.props
- Before every commit, `dotnet build --configuration Release` is mandatory
- Before every commit, `dotnet test` on the affected projects is mandatory (the full test suite is too slow; only run
  the `tests/<Module>.UnitTests` that map to the changed files)
- Commit messages are in English, `type(scope): ...`, with "by /sonar-fix" in the body
- When pushing, `--no-verify` / `--force` are **forbidden**

### Handling no progress

If after 2 rounds the total issue count, coverage and skip list are all unchanged → stop immediately and output:
```
/sonar-fix made no progress for 2 consecutive rounds and has stopped. Check whether the remaining items need manual intervention.
```

### Managing the skip list

- Every write to skip.json includes: attempts, reason, lastAttempt (ISO date)
- skip.json blocks:
  - `issues`: issues whose automatic fix attempts failed and were given up (skipped permanently unless the user
    removes them by hand)
  - `files`: files whose coverage test additions failed and were given up (same as above)
  - `humanReview`: issues hit by the rule-level blocklist (such as S125), waiting for human review.
    After the user marks one False Positive in the SonarCloud UI or fixes it by hand, it can be removed from skip.json
- If the user later fixes an item, it can be removed by hand from `docs/.sonar-fix-state/skip.json`
- skip.json changes go into the same `chore(sonar-fix): ...` commit

---

## Reference rules

- `.claude/rules/sonarcloud.md`: SonarCloud rule reference table
- `.claude/rules/scanning.md`: baseline SAST security requirements
- `.claude/rules/testing.md`: test writing patterns
- `.claude/rules/pull-request.md`: how changes reach `main`, and CI failure handling
- `.claude/commands/ci-watch.md`: the downstream skill that watches CI after the change is pushed
