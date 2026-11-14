#!/usr/bin/env bash
# Decides whether a Build CI run is in full or lite mode, and writes `full=true` or `full=false` to `$GITHUB_OUTPUT`,
# with `sonar`, whether SonarScanner analyses this run.
# The `build` and `browser` jobs of `.github/workflows/build-ci.yml` run it as their step `scope`, so both read the
# same decision: `build` runs the browser tests itself in full mode, where it collects coverage for SonarCloud, and
# `browser` runs them in lite mode.
#
# Lite (SQL Server + SQLite, no SonarCloud) is the default. Full mode must be requested explicitly: a manual dispatch
# with `db_scope=all`, or `[all-db]` in the commit message of a push or of the merge queue's commit (which starts with
# the pull request's title), or in the title of a pull request.
#
# SonarScanner runs in full mode except on a merge queue run: the queue builds on a temporary branch with a new name
# each time, and an analysis there would leave a branch in SonarCloud for every queued pull request. The push to
# `main` that follows the merge is analysed as before.
#
# It also writes `POLHEM_TEST_REQUIRED_DATABASES` to `$GITHUB_ENV`. `RequiredTestDatabaseGateTests`
# (tests/Polhem.Db.UnitTests) fails when a database listed there has no connection string under the name the tests
# read. Without it, a renamed variable skips every test for that database and the run stays green.
#
# Input: the `DISPATCH_SCOPE`, `EVENT_NAME`, `COMMIT_MSG` and `PR_TITLE` environment variables. The commit message and the title are
# user-controlled strings, so the workflow passes them through `env:`; expanding them with `${{ }}` inside `run:`
# would allow script injection (the S7636 reason in `.claude/rules/sonarcloud.md`).
set -euo pipefail

full=false
if [ "${DISPATCH_SCOPE:-}" = "all" ]; then
  full=true
# A here-string, not a pipe: `grep -q` exits at the first match, and under `pipefail` the writer's SIGPIPE on a long
# commit message would turn a requested full run into a lite one.
elif grep -qF '[all-db]' <<< "${COMMIT_MSG:-}${PR_TITLE:-}"; then
  full=true
fi
echo "full=$full" >> "$GITHUB_OUTPUT"

sonar=$full
if [ "${EVENT_NAME:-}" = "merge_group" ]; then
  sonar=false
fi
echo "sonar=$sonar" >> "$GITHUB_OUTPUT"

if [ "$full" = true ]; then
  echo "POLHEM_TEST_REQUIRED_DATABASES=SQLServer,SQLite,PostgreSQL,MySQL,Oracle" >> "$GITHUB_ENV"
  echo "Database scope: all (SQL Server + SQLite + PostgreSQL + MySQL + Oracle), SonarCloud $([ "$sonar" = true ] && echo enabled || echo 'skipped in the merge queue')"
else
  echo "POLHEM_TEST_REQUIRED_DATABASES=SQLServer,SQLite" >> "$GITHUB_ENV"
  echo "Database scope: lite (SQL Server + SQLite), SonarCloud skipped"
  echo "  → For full mode: put [all-db] in the commit message, or run workflow_dispatch manually with db_scope=all"
fi
