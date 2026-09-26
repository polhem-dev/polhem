# SonarCloud integration setup guide

This document records the complete setup needed to connect a GitHub repo to SonarCloud and upload test coverage from
CI. New projects can follow it to set up the same integration quickly.

## When it applies

- .NET projects (C#)
- GitHub Actions as CI
- Coverage collected with `coverlet.collector`
- SonarCloud identifies the project by the combination of Organization and Project Key

## Why CI-based analysis is needed

SonarCloud enables "**Automatic Analysis**" by default. It only does static analysis and **cannot receive coverage
reports uploaded from CI**. To show coverage in SonarCloud:
- Automatic Analysis must be turned off
- CI runs the analysis with SonarScanner for .NET and uploads the coverage instead

## Overall flow (one-time setup)

1. Import the project into SonarCloud (if it does not exist yet)
2. Turn off Automatic Analysis
3. Generate a SonarCloud token
4. Add the token as a GitHub repo secret (named `SONAR_TOKEN`)
5. Add the SonarScanner steps to the CI workflow
6. Push to trigger CI, and verify that SonarCloud received the coverage

## Step 1 | Import the SonarCloud project

1. Sign in to https://sonarcloud.io
2. `+` → `Analyze new project` → choose the GitHub repo
3. When it is done, note the two identifiers:
   - **Organization Key** (for a personal account usually the GitHub account, such as `jeff377`)
   - **Project Key** (usually `{org}_{repo}`, such as `jeff377_bee-library`)

## Step 2 | Turn off Automatic Analysis

1. Open the SonarCloud project → `Administration` → `Analysis Method`
2. Switch "**Automatic Analysis**" **off (OFF)**

> If it stays on, the CI upload is rejected with the error: *"You are running CI analysis while Automatic Analysis is enabled"*

## Step 3 | Generate a SonarCloud token

1. SonarCloud avatar at the top right → `My Account` → `Security`
2. Under `Generate Tokens`, enter an identifying name (suggested format `<repo>-ci`, for example `polhem-library-ci`)
3. `Generate` → **copy the token immediately** (it is shown only once)

## Step 4 | Add the GitHub repo secret

1. Go to `https://github.com/{owner}/{repo}/settings/secrets/actions`
2. `New repository secret`
   - Name: **`SONAR_TOKEN`** (the same name in every project)
   - Secret: paste the token from step 3
3. `Add secret`

> The name is fixed as `SONAR_TOKEN` so that the workflow can be reused across projects without changing the
> reference name in each one.

## Step 5 | CI workflow setup

The following is a minimal working `build-ci.yml` (Windows runner):

```yaml
jobs:
  build:
    runs-on: windows-latest

    steps:
    - name: Checkout code
      uses: actions/checkout@v4
      with:
        fetch-depth: 0          # SonarCloud needs the full git history for SCM blame

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: 10.0.x

    - name: Setup Java (for SonarScanner)
      uses: actions/setup-java@v4
      with:
        distribution: zulu
        java-version: '17'

    - name: Cache SonarCloud packages
      uses: actions/cache@v4
      with:
        path: ~\.sonar\cache
        key: ${{ runner.os }}-sonar
        restore-keys: ${{ runner.os }}-sonar

    - name: Install SonarScanner for .NET
      run: dotnet tool install --global dotnet-sonarscanner

    - name: SonarScanner Begin
      env:
        SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
      shell: pwsh
      run: |
        dotnet sonarscanner begin `
          /k:"{ProjectKey}" `
          /o:"{OrganizationKey}" `
          /d:sonar.token="$env:SONAR_TOKEN" `
          /d:sonar.host.url="https://sonarcloud.io" `
          /d:sonar.cs.opencover.reportsPaths="**/TestResults/**/coverage.opencover.xml" `
          /d:sonar.coverage.exclusions="tests/**,samples/**"

    - name: Build
      run: dotnet build <Solution>.slnx --configuration Release

    - name: Test with coverage
      run: dotnet test <Solution>.slnx --configuration Release --no-build --verbosity normal --collect:"XPlat Code Coverage;Format=opencover"

    - name: SonarScanner End
      env:
        SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
      shell: pwsh
      run: dotnet sonarscanner end /d:sonar.token="$env:SONAR_TOKEN"
```

When copying it into a new project, replace:
- `{ProjectKey}` → the SonarCloud Project Key (such as `jeff377_bee-library`)
- `{OrganizationKey}` → the SonarCloud Organization Key (such as `jeff377`)
- `<Solution>.slnx` → the actual solution file name

## Step 6 | Verify that coverage was uploaded

Push to trigger CI. After CI succeeds, confirm through the API:

```bash
curl -s "https://sonarcloud.io/api/measures/component?component={ProjectKey}&metricKeys=coverage,line_coverage,branch_coverage,ncloc" | python3 -m json.tool
```

The three metrics `coverage`, `line_coverage` and `branch_coverage` should have values. If there is only `ncloc`,
SonarCloud received the analysis but not the coverage → go back and check:
- whether `coverage.opencover.xml` was produced (the `dotnet test` log)
- whether the `sonar.cs.opencover.reportsPaths` path pattern covers the actual output location

You can also open it directly:
```
https://sonarcloud.io/summary/new_code?id={ProjectKey}&branch=main
```

## Common errors and fixes

| Symptom | Cause | Fix |
|------|------|------|
| `sonar.token= is invalid` (empty value) | The GitHub secret name does not match the workflow reference | Confirm the secret is named `SONAR_TOKEN` |
| `You are running CI analysis while Automatic Analysis is enabled` | Automatic Analysis was not turned off | Go back to step 2 |
| SonarCloud shows only `ncloc` and no `coverage` metric | Automatic Analysis mode, or the coverage file path does not match | Turn off Automatic Analysis and check the `reportsPaths` pattern |
| `No coverage` badge | Same as above | Same as above |
| All tests Skipped in CI, so coverage is abnormally low | `[LocalOnlyFact]` is skipped in the CI environment | Expected; do not use `LocalOnlyFact` for pure logic tests |

## Notes

- **A Windows runner is required**: SonarScanner for .NET works on a Linux runner, but this project uses a Windows
  runner and PowerShell syntax and has not been verified cross-platform
- **Java 17 is required**: SonarScanner v6+ needs a Java 17 runtime
- **`fetch-depth: 0` is required**: otherwise SonarCloud cannot do SCM blame, and the new-code analysis is inaccurate
- **`--collect:"XPlat Code Coverage;Format=opencover"`**: `Format=opencover` must be given. Otherwise coverlet
  outputs Cobertura by default, which SonarScanner for .NET cannot read
- **Test results (trx) are not uploaded**: this setup uploads only coverage, not test execution results. For
  SonarCloud to show the total number of tests, add `--logger trx` and set `sonar.cs.vstest.reportsPaths`
- **Coverage excludes `tests/` and `samples/`**: this keeps the test projects themselves out of the coverage
  denominator, which would inflate it. Adjust `sonar.coverage.exclusions` to the project structure

## References

- [SonarScanner for .NET official documentation](https://docs.sonarsource.com/sonarqube-cloud/advanced-setup/ci-based-analysis/sonarscanner-for-net/)
- [coverlet `--collect` parameter reference](https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/VSTestIntegration.md)
- This project's implementation: [build-ci.yml](../../.github/workflows/build-ci.yml)
