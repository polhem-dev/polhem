# Branch protection and repository settings

This document records how `main` of `polhem-dev/polhem` is protected and which repository settings go with it.
The workflow they enforce is described in `.claude/rules/pull-request.md`: every change, including the maintainers',
reaches `main` through a pull request.

## Branch protection on `main`

Set with the classic branch protection API:

| Setting | Value | Why |
|---------|-------|-----|
| `required_status_checks.contexts` | `["build", "docs"]` | The `build` job of `build-ci.yml` and the `docs` job of `docs-check.yml` must pass |
| `required_status_checks.strict` | `true` | The branch must be up to date with `main` before it merges |
| `enforce_admins` | `true` | The rules apply to administrators too; nobody pushes to `main` directly |
| `required_pull_request_reviews.required_approving_review_count` | `0` | A pull request is required, but no approval: with a single maintainer, a required approval would block every pull request they open, because GitHub does not let authors approve their own |
| `required_pull_request_reviews.require_code_owner_reviews` | `false` | `.github/CODEOWNERS` only requests a review |
| `restrictions` | `null` | No restriction on who can push to pull request branches |
| `allow_force_pushes` / `allow_deletions` | `false` | `main` cannot be rewritten or deleted |

```bash
gh api repos/polhem-dev/polhem/branches/main/protection --method PUT --input - <<'EOF'
{
  "required_status_checks": { "strict": true, "contexts": ["build", "docs"] },
  "enforce_admins": true,
  "required_pull_request_reviews": {
    "required_approving_review_count": 0,
    "require_code_owner_reviews": false,
    "dismiss_stale_reviews": false
  },
  "restrictions": null,
  "allow_force_pushes": false,
  "allow_deletions": false
}
EOF
```

Show the current rules with `gh api repos/polhem-dev/polhem/branches/main/protection`.

When a second maintainer joins, raise `required_approving_review_count` to `1` and consider
`require_code_owner_reviews`; from then on each maintainer's pull requests can be approved by the other.

## Repository settings

| Setting | Value | Why |
|---------|-------|-----|
| `allow_squash_merge` | `true` | The only merge method: one pull request becomes one commit on `main` |
| `allow_merge_commit` / `allow_rebase_merge` | `false` | |
| `delete_branch_on_merge` | `true` | Merged branches are removed |
| `allow_auto_merge` | `true` | `gh pr merge --auto --squash` merges once the checks pass. It merges as the person who enabled it, so the push to `main` starts workflows normally and no token is needed |

```bash
gh api repos/polhem-dev/polhem --method PATCH \
  -F allow_squash_merge=true -F allow_merge_commit=false -F allow_rebase_merge=false \
  -F delete_branch_on_merge=true -F allow_auto_merge=true
```

## Constraints on the workflows

- **A required check must start on every pull request.** A required workflow with a `paths` filter on
  `pull_request` never reports on a pull request outside those paths, and that pull request waits forever.
  This is why the `pull_request` trigger of `build-ci.yml` has no `paths` filter; its `push` trigger still has one.
  A pull request that changes only `.md` files is recognized inside the `build` job instead: the job starts,
  skips the build, tests and SonarCloud, and reports success. The detection is a step of `build`, not a job of its
  own: GitHub reports a job skipped by `if:` as passing, so a failed detection job would let the pull request merge.
  The step and its reasons are in the workflow file.
- **The check names are the job names** (`build`, `docs`). Renaming a job requires changing the protection too;
  otherwise every pull request waits for a check that no longer exists.
- **The push that created `main` did not start `build-ci.yml`.** On 2026-09-26 the first push of this repository
  started `docs-check.yml`, which has no `paths` filter, but not `build-ci.yml`, whose `push` trigger has one.
  The full run was started with `workflow_dispatch` (`db_scope=all`) instead.
