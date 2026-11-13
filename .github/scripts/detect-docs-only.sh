#!/usr/bin/env bash
# Decides whether a Build CI job builds at all, and writes `build=true` or `build=false` to `$GITHUB_OUTPUT`.
# Every job of `.github/workflows/build-ci.yml` (`build`, `templates` and `browser`) runs it as its step `changes`.
#
# Only a pull request whose changes are all `.md` files skips the build; a push or a manual dispatch always builds.
# The skip is done step by step rather than on the job: GitHub reports a job skipped by `if:` as a passing check, so
# if the detection lived in a separate job, a failure there would skip the job and let the pull request merge. Inside
# the job, a failed detection fails the job.
#
# HEAD is the merge commit GitHub creates for the pull request, so `HEAD^1..HEAD` is exactly what the pull request
# would change on the current `main`, even when the branch is behind it. The checkout must therefore reach HEAD's
# parents. `--no-renames` lists both sides of a rename, so renaming a `.cs` file to `.md` still builds. Anything
# unexpected (no merge commit, an empty list) builds.
#
# Input: the `EVENT_NAME` environment variable (`github.event_name`).
set -euo pipefail

build=true
if [[ "${EVENT_NAME:-}" == "pull_request" ]] && git rev-parse -q --verify HEAD^2 > /dev/null; then
  files=$(git diff --name-only --no-renames HEAD^1 HEAD)
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
