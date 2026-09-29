# SonarCloud setup

This document records how `polhem-dev/polhem` is connected to SonarCloud and how coverage reaches it from CI.
The analysis steps themselves live in [build-ci.yml](../.github/workflows/build-ci.yml) and the file-level
settings in [SonarQube.Analysis.xml](../SonarQube.Analysis.xml); this document does not copy them.

## Identifiers

| Item | Value |
|------|-------|
| Organization | `polhem-dev`, bound to the GitHub organization of the same name |
| Project key | `polhem-dev_polhem` |
| Quality gate | Built-in **Sonar way** (the organization default) |
| Quality profile | Built-in **Sonar way comprehensive** for every language (the organization default) |
| New code definition | Previous version (the default) |
| Analysis method | CI-based; Automatic Analysis is off |

The organization and project key are passed on the command line of the `SonarScanner Begin` step (`/o:`, `/k:`).
The same key appears in the README badges (both languages) and in `.claude/commands/sonar-fix.md`.

## When the analysis runs

Only in **full mode** of `build-ci.yml`: a commit message or pull request title containing `[all-db]`, or a manual
`workflow_dispatch` with `db_scope=all`. Lite mode skips SonarScanner and coverage entirely. See
`.claude/rules/testing.md` § CI database scope.

The runner is `ubuntu-latest` and the steps are bash. SonarScanner for .NET needs Java 17 (`actions/setup-java`) and
`fetch-depth: 0` on checkout, so that SonarCloud can blame lines for the new code period. Coverage is collected with
coverlet in OpenCover format (`--collect:"XPlat Code Coverage;Format=opencover"`); SonarScanner for .NET cannot read
coverlet's default Cobertura output.

## One-time setup

1. **Organization and GitHub App.** The `SonarQube Cloud` GitHub App is installed on the `polhem-dev` organization for
   all repositories. On 2026-09-26 the `polhem-dev_polhem` project appeared in SonarCloud right after the GitHub
   repository was created, with Automatic Analysis already off; nothing had to be imported by hand.
2. **Automatic Analysis must be off** (project → Administration → Analysis Method). Otherwise the CI upload is
   rejected with *"You are running CI analysis while Automatic Analysis is enabled"*. Check it with
   `curl -s "https://sonarcloud.io/api/settings/values?component=polhem-dev_polhem&keys=sonar.autoscan.enabled"`.
3. **Token.** A maintainer generates a token in SonarCloud (avatar → My Account → Security) and stores it as the
   repository secret `SONAR_TOKEN` with `gh secret set SONAR_TOKEN -R polhem-dev/polhem`, which prompts for the value
   so it does not end up in the shell history.

## Verifying coverage

After a full-mode run, SonarCloud should report coverage, not only lines of code:

```bash
curl -s "https://sonarcloud.io/api/measures/component?component=polhem-dev_polhem&metricKeys=coverage,line_coverage,branch_coverage,ncloc" \
  | python3 -m json.tool
```

## Common errors

| Symptom | Cause | Fix |
|---------|-------|-----|
| `sonar.token= is invalid` (empty value) | The `SONAR_TOKEN` secret is missing or named differently | Check `gh secret list -R polhem-dev/polhem` |
| `You are running CI analysis while Automatic Analysis is enabled` | Automatic Analysis is on | Setup step 2 |
| Only `ncloc`, no `coverage` | The coverage file was not produced, or `sonar.cs.opencover.reportsPaths` does not match its location | Check the `dotnet test` log for `coverage.opencover.xml` and the pattern in `SonarQube.Analysis.xml` |
| `Organization is not allowed to access data from non main branches` | The free plan only serves data of the main branch | Compare alternatives locally instead; see `gotchas/test-ci-release.md` |

## References

- [SonarScanner for .NET](https://docs.sonarsource.com/sonarqube-cloud/advanced-setup/ci-based-analysis/sonarscanner-for-net/)
- [coverlet `--collect` options](https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/VSTestIntegration.md)
