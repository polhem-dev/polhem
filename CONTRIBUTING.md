# Contributing to Polhem

**English** | [繁體中文](CONTRIBUTING.zh-TW.md)

Thank you for your interest in Polhem. This guide describes how changes reach the repository and the conventions
they follow.

## Before you start

- For a bug fix or a small improvement, open a pull request directly.
- For a larger change (a new feature, a change to public API, a new dependency), open an issue first so the approach
  can be agreed on before you spend time on it.
- Read the [developer documentation](docs/en/README.md) for the architecture, and the
  [architecture decision records](docs/adr/README.md) for why the design is the way it is.

## Workflow

1. Fork the repository, or create a branch if you have write access, from the latest `main`.
2. Make the change, with tests.
3. Build and test locally (see below).
4. Open a pull request against `main`.

`main` only accepts changes through pull requests. The `build` check must pass before a pull request can merge, and
review is requested from the code owners listed in [`.github/CODEOWNERS`](.github/CODEOWNERS).

## Build and test

```bash
dotnet build Polhem.slnx --configuration Release
./test.sh
```

- The build treats every warning as an error. Many code style rules are enforced through `.editorconfig` and the
  analyzers, so breaking them fails the build; the rest are described in `.claude/rules/code-style.md`.
- `./test.sh` starts local database containers when Docker has them, and runs all test projects. Tests that need a
  database are skipped when its connection string is not configured; the script header explains the container names
  and how to override them. Pass a test project path to run only that project.
- If your change touches public API, declare it in the project's `PublicAPI.Unshipped.txt` and explain in the pull
  request whether it is binary-compatible. The analyzer only checks that a change is declared, not that it is
  compatible.
- New public API comes with tests.

## Conventions

- **Language**: everything maintained together is in English: code, XML documentation, comments, test names and
  `[DisplayName]` text, and commit messages. Public documents and ADRs are bilingual (English and Traditional
  Chinese). Some parts of the repository still contain Chinese from before this policy; write new content in English.
  The reasons are in [ADR-045](docs/adr/adr-045-language-policy-and-local-plans.md).
- **Commit messages**: English, in the imperative mood, with a subject that says what changed. Use the body to
  explain why.
- **Design decisions**: a decision that others need to understand later is recorded as an ADR in `docs/adr/`.
- **Documents**: after changing Markdown documents, run `./check-md-links.sh` and `./check-public-docs.sh`.
  English is the source of the documents under `docs/<lang>/` and of the ADRs; `./check-docs-i18n.sh` reports a
  Traditional Chinese translation that has fallen behind its source. If you cannot update the translation, say so in
  the pull request.
- **Personal working documents**: plans, drafts and notes go in `local/` at the repository root, which git ignores.
  Do not commit them, and do not link to them from committed files: nobody else can open them.

## AI coding agents

Guidance for coding agents is in `.claude/`: `.claude/CLAUDE.md`, the rules in `.claude/rules/` and the skills in
`.claude/skills/`. It is committed and maintained like code, so it applies to every contributor who uses an agent.
The rules there are also a detailed reference for the conventions above.

## Releases

Maintainers publish releases. Pushing a `v*` tag publishes the packages to NuGet, and a published package cannot be
withdrawn, so a tag is only pushed deliberately, after the release build and tests pass. Do not make a failing
release build green by changing the tests or the code they check.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE.txt).
