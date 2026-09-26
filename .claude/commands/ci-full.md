---
description: Trigger full-mode CI (all four databases + SonarCloud), wait for it, and report the result
argument-hint: "[branch name, default main]"
---

# CI Full

Trigger **full-mode** CI on branch `$1` (`main` if not given): all four databases
(SQL Server / PostgreSQL / MySQL / Oracle) plus a SonarCloud scan, about 8 minutes.

> `build-ci.yml` defaults to lean mode (SQL Server + SQLite, Sonar skipped, about 3.5 minutes).
> For the criteria and the rule "ask the user before pushing", see
> `.claude/rules/testing.md` § CI database scope: ask the user before pushing.

## Steps

### 1. Check the remote state first (do not skip this step)

`workflow_dispatch` runs **the latest commit of that branch on the remote**, not the local working tree.
If there are unpushed local commits or uncommitted changes, the result **has nothing to do with the code in front of
you**.

```bash
git status --short
git log --oneline origin/$1..$1
```

If there is a gap, stop and tell the user; ask whether to push first or run against the remote as it is.
**Do not trigger silently.**

### 2. Trigger

```bash
gh workflow run build-ci.yml --ref <branch> -f db_scope=all
```

### 3. Get the run id and wait

dispatch does not return a run id. After a few seconds, look up the run of the latest `workflow_dispatch` event:

```bash
gh run list --workflow=build-ci.yml -L 5 --json databaseId,event,status,createdAt \
  --jq '[.[] | select(.event=="workflow_dispatch")][0]'
```

Wait in the background with `gh run watch <id> --exit-status` (about 8 minutes), and report when it finishes.

### 4. Report

- **Success** → report the total duration, and confirm the tests for all four databases actually ran
  (check that the two steps `Wait for extra database containers` and `Enable extra database connection strings`
  are success, not skipped; skipped means the mode detection did not take effect, and you effectively ran lean mode
  for nothing).
- **Failure** → get `gh run view <id> --log-failed` and classify it following "When CI fails" in
  `.claude/rules/pull-request.md`: fix what is clearly fixable and commit it, getting the fix to `main` as that file
  describes; explain architectural or ambiguous failures to the user first.
- **SonarCloud** → coverage is only reported in full mode. Follow up with `/sonar-fix` when needed.

## When to use

- The change touches `src/Polhem.Db/Providers/**`, `src/Polhem.Repository/**`, `SchemaSyntax` /
  `DbTypeMapper` / `NormalizeDbType`, or any SQL generation logic
- **Before a release** (mandatory)
- You want an extra SonarCloud scan (lean mode does not run Sonar, so issues pile up until the next full-mode run
  surfaces them)

> Another way to trigger it: put the `[all-db]` marker in the commit message (for a PR, the PR title), and the push
> runs in full mode. Good for "this change needs full verification" cases, so you do not have to rerun afterwards.
