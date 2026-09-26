---
description: Watch the latest CI status and SonarCloud scan results for main; on failure, analyze the log and fix automatically
argument-hint: "[branch name, default main]"
---

# CI Watch

Start the watch procedure for branch `$1` (`main` if not given).

## Steps

Use the `/loop` mechanism to keep checking until CI passes or the user steps in:

```
/loop Watch the CI status of branch $1 (main if not given), with these rules:

1. Check the latest GitHub Actions run:
   - `gh run list -b <branch> -L 3 --json databaseId,status,conclusion,workflowName,headSha`
   - If the latest run is `in_progress` or `queued`, wait for the next round (choose a longer delay, such as 240s, to match the build time)
   - If the latest run's `conclusion` is `success`, go to step 2
   - If the latest run's `conclusion` is `failure`:
     a. Get the failure log with `gh run view <id> --log-failed`
     b. Analyze the cause: classify it as "compile error / test failure / lint / formatting / environment problem"
     c. Clearly fixable: change the code and commit (commit message follows the project convention: English, type(scope)), then get the fix to `main` as `.claude/rules/pull-request.md` describes
     d. Architectural or ambiguous: stop the loop first and explain to the user
     e. After the fix, go to the next round and wait for the new CI run result

2. Check the SonarCloud scan result (if build-ci.yml integrates SonarCloud):
   - Get the latest quality gate status through the sonarcloud API or gh checks
   - If there are new BLOCKER / HIGH / MEDIUM issues:
     a. Get the issue list and the corresponding code locations
     b. Fix them according to `.claude/rules/sonarcloud.md`
     c. Commit, get the change to `main` as `.claude/rules/pull-request.md` describes, and wait for the next round to verify
   - If they are only LOW or INFO, record and report them; do not fix automatically (the user decides whether to handle them)

3. Pass conditions (end the loop):
   - The latest GitHub Actions run is success
   - The SonarCloud quality gate is passed (or there are no new HIGH-or-above issues)
   - Report a summary: which problems were fixed, and what remains to watch
```

## Rules and limits

- **Do not fix known environmental failures** (such as a transient NuGet restore failure or a runner timeout);
  suggest the user re-run manually
- **Do not close or ignore any check**
- When pushing, `--no-verify` and `--force` are **forbidden**
- Every commit made after a fix must follow the conventions in `.claude/CLAUDE.md`
- If the same kind of problem fails to be fixed 3 times in a row, stop and ask the user for help (to avoid an infinite
  loop)
- If the user is currently working on something else, report and let the user decide whether to continue

## Reference rules

- `.claude/rules/pull-request.md`: how changes reach `main`, and the CI failure handling procedure
- `.claude/rules/sonarcloud.md`: SonarCloud rule reference
- `.claude/rules/scanning.md`: baseline SAST security requirements
