# Branch protection and repository settings

The repository settings, the protection of `main` and its required checks are those of the polhem-dev organization's
baseline, [`repo-baseline.json`](https://github.com/polhem-dev/.github/blob/main/repo-baseline.json), audited and
applied with the `org-baseline` skill of the dev-workflow plugin. The
[organization's contributing guide](https://github.com/polhem-dev/.github/blob/main/CONTRIBUTING.md) explains why
they are what they are. The workflow they enforce is described in `.claude/rules/pull-request.md`. This page records
only what is specific to Polhem.

Show the current rules with `gh api repos/polhem-dev/polhem/branches/main/protection`.

## Required checks

`build`, `templates` and `browser` are the jobs of `build-ci.yml`, and `docs` is the job of `docs-check.yml`.
`templates` runs the template tests (`tests/Polhem.Templates.UnitTests`) beside `build`, which leaves them out: they
generate, build, publish and start whole applications one after another, and in `build` they decided how long its test
step took. `browser` runs the browser tests (the `*.BrowserTests` projects) beside `build` in lite mode, for the same
reason; in full mode `build` runs them so that their coverage reaches SonarCloud, and `browser` skips them.

## Constraints on the workflows

- **Documentation-only pull requests pass inside `build`.** The `pull_request` trigger of `build-ci.yml` has no `paths`
  filter; its `push` trigger still has one. A pull request that changes only `.md` files is recognized inside the
  `build`, `templates` and `browser` jobs: each starts, skips its build and tests (and `build` its SonarCloud
  analysis), and reports success. The detection is a step of each job, not a job of its own: GitHub reports a job
  skipped by `if:` as passing, so a failed detection job would let the pull request merge. Every job runs
  `.github/scripts/detect-docs-only.sh`, whose header records the rules and their reasons.
- **The push that created `main` did not start `build-ci.yml`.** On 2026-09-26 the first push of this repository
  started `docs-check.yml`, which has no `paths` filter, but not `build-ci.yml`, whose `push` trigger has one.
  The full run was started with `workflow_dispatch` (`db_scope=all`) instead.
