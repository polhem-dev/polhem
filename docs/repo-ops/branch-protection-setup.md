# GitHub branch protection setup guide

This document records how the protection rules for the `main` branch are set up. It suits a single developer who
works across several devices (Mac / Windows / App).

## When it applies

| Device | Build environment | How you work |
|------|----------|----------|
| Mac / Windows | Yes | Can push to `main` directly |
| App (such as Claude Code) | No | Create a branch → PR → merge after CI passes |

## Settings

Use the GitHub Classic Branch Protection API to enable the following rules on the `main` branch:

| Setting | Value | Description |
|----------|----|------|
| `required_status_checks.contexts` | `["build"]` | A PR must pass the `build` job before it merges |
| `required_status_checks.strict` | `true` | A PR branch must be up to date with main before it can merge |
| `enforce_admins` | `false` | Repo admins can push directly, without the PR restriction |
| `required_pull_request_reviews` | `null` | No code review required (personal project) |
| `restrictions` | `null` | No restriction on who can push |
| `allow_force_pushes` | `false` | Force pushes are forbidden |
| `allow_deletions` | `false` | Deleting the main branch is forbidden |
| `required_linear_history` | `false` | Merge commits are allowed |
| `required_signatures` | `false` | Commit signatures are not required |

## Command

Set it up in one step with the `gh` CLI:

```bash
gh api repos/{owner}/{repo}/branches/main/protection \
  --method PUT \
  --input - <<'EOF'
{
  "required_status_checks": {
    "strict": true,
    "contexts": ["build"]
  },
  "enforce_admins": false,
  "required_pull_request_reviews": null,
  "restrictions": null
}
EOF
```

> The `"build"` in `contexts` must match the job name in the CI workflow.

## Verifying the settings

```bash
# Show the current protection rules
gh api repos/{owner}/{repo}/branches/main/protection

# Remove the protection rules (to reset them)
gh api repos/{owner}/{repo}/branches/main/protection --method DELETE
```

## Prerequisites

1. **The CI workflow must already exist and have run**: GitHub must have run the `build` job at least once before it
   recognizes that status check context
2. **The workflow must include a `pull_request` trigger**:

```yaml
on:
  pull_request:
    branches:
      - main
```

## Notes

- `enforce_admins: false` is what lets admins push directly. Set it to `true` and everyone must go through a PR
- If several people work on the repo, set `required_pull_request_reviews` to `{"required_approving_review_count": 1}`
- `strict: true` requires a PR branch to be up to date with main (rebased) before it merges. Set it to `false` to
  relax this
