# Branch protection and repository settings

The repository settings, the protection of `main` and its required checks are those of the polhem-dev organization's
baseline, [`repo-baseline.json`](https://github.com/polhem-dev/.github/blob/main/repo-baseline.json), audited and
applied with the `org-baseline` skill of the dev-workflow plugin. The
[organization's contributing guide](https://github.com/polhem-dev/.github/blob/main/CONTRIBUTING.md) explains why
they are what they are. The workflow they enforce is described in `.claude/rules/pull-request.md`. This page records
only what is specific to Polhem.

Show the current rules with `gh api repos/polhem-dev/polhem/branches/main/protection`.

## Required checks

`build` is the job of `build-ci.yml` and `docs` the job of `docs-check.yml`.

## Constraints on the workflows

- **Documentation-only pull requests pass inside `build`.** The `pull_request` trigger of `build-ci.yml` has no `paths`
  filter; its `push` trigger still has one. A pull request that changes only `.md` files is recognized inside the
  `build` job: the job starts, skips the build, tests and SonarCloud, and reports success. The detection is a step of
  `build`, not a job of its own: GitHub reports a job skipped by `if:` as passing, so a failed detection job would let
  the pull request merge. The step and its reasons are in the workflow file.
- **The push that created `main` did not start `build-ci.yml`.** On 2026-09-26 the first push of this repository
  started `docs-check.yml`, which has no `paths` filter, but not `build-ci.yml`, whose `push` trigger has one.
  The full run was started with `workflow_dispatch` (`db_scope=all`) instead.
