#!/usr/bin/env bash
# Decides whether a Build CI job builds at all, and writes `build=true` or `build=false` to `$GITHUB_OUTPUT`.
# Every job of `.github/workflows/build-ci.yml` (`build`, `templates` and `browser`) runs it as its step `changes`.
#
# Only a pull request, or a merge queue run, whose changes are all `.md` files skips the build; a push or a manual
# dispatch always builds.
# The skip is done step by step rather than on the job: GitHub reports a job skipped by `if:` as a passing check, so
# if the detection lived in a separate job, a failure there would skip the job and let the pull request merge. Inside
# the job, a failed detection fails the job.
#
# HEAD is the merge commit GitHub creates for the pull request, so `HEAD^1..HEAD` is exactly what the pull request
# would change on the current `main`, even when the branch is behind it. The checkout must therefore reach HEAD's
# parents. `--no-renames` lists both sides of a rename, so renaming a `.cs` file to `.md` still builds. Anything
# unexpected (no merge commit, an empty list) builds.
#
# A merge queue run compares the queue's commit with the `main` it starts from (`MERGE_GROUP_BASE`), so it covers the
# pull requests queued ahead as well: a documentation-only pull request behind one that changes code still builds,
# because its run is what certifies the combination. The base is fetched here, since the checkout is shallow.
#
# Input: the `EVENT_NAME` (`github.event_name`) and `MERGE_GROUP_BASE` (`github.event.merge_group.base_sha`)
# environment variables.
set -euo pipefail

build=true
base=""
if [[ "${EVENT_NAME:-}" == "pull_request" ]] && git rev-parse -q --verify HEAD^2 > /dev/null; then
  base=HEAD^1
elif [[ "${EVENT_NAME:-}" == "merge_group" && -n "${MERGE_GROUP_BASE:-}" ]] \
  && git fetch --quiet --no-tags --depth=1 origin "$MERGE_GROUP_BASE"; then
  base=$MERGE_GROUP_BASE
fi
if [[ -n "$base" ]]; then
  files=$(git diff --name-only --no-renames "$base" HEAD)
  echo "Changed files:"
  printf '%s\n' "$files"
  # A here-string, not a pipe: `grep -q` exits at the first match, and under `pipefail` the writer's SIGPIPE on a
  # long list would turn a pull request with code into a documentation-only one.
  if [[ -n "$files" ]] && ! grep -qv '\.md$' <<< "$files"; then
    build=false
  fi
fi
echo "build=$build" >> "$GITHUB_OUTPUT"
if [[ "$build" == false ]]; then
  echo "Only .md files changed: skipping the build and tests (docs-check.yml checks the documents)"
fi
