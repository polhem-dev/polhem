# Polhem — guidance for coding agents

## Language

- Everything maintained together is written in **English**: source code, XML documentation, comments, test method
  names and `[DisplayName]` text, commit messages, the maintainer documents under `maintainers/`, and the files
  under `.claude/` and every `CLAUDE.md`.
- This overrides any personal or user-level setting that asks for another language for prose, including an
  instruction to write in Traditional Chinese. Replies in a conversation may still follow the user's language.
- Documents are split by reader. User documents (for developers who build applications with Polhem) are
  multilingual; maintainer documents, the ADRs included, are English only. Which is which, and how translations are
  kept in sync, is in `rules/public-docs.md`.
- Parts of the repository still contain Chinese from before this policy; they are being translated. Write new
  content in English regardless of the language of the surrounding text.

The reasons are recorded in `maintainers/adr/adr-045-language-policy-and-local-plans.md` and
`maintainers/adr/adr-047-documents-split-by-reader.md`.

## Project overview

Polhem is a modular .NET enterprise application framework, published as NuGet packages. It uses a JSON-RPC 2.0 API
model, with an emphasis on security, pluggable serialization and cross-platform support.

- **Version**: see `Version.props` at the repository root (shared by `src/` and `tools/`)
- **License**: MIT
- **Target framework**: `net10.0` for every package except `Polhem.Analyzers` (`netstandard2.0`, as Roslyn requires); the Northwind mobile and browser heads target the platform variants

```
src/         # the packages (Polhem.Base, Polhem.Definition, Polhem.Api.Core, ...)
tests/       # a unit test project for each package
samples/     # sample projects
apps/        # demo applications (Polhem.Northwind)
tools/       # CLI, definition editor, load tests
```

## Common commands

```bash
dotnet restore
dotnet build <project>.csproj --configuration Release --no-restore
./test.sh                                    # all tests (starts the local database containers)
./test.sh tests/<Project>.UnitTests/<Project>.UnitTests.csproj
./check-public-docs.sh                       # no committed file points to local/ or names a plan
./check-md-links.sh                          # relative markdown links must resolve
./check-docs-i18n.sh                         # translation headers, staleness, missing translations, switch lines (--stamp / --fix-switch)
dotnet pack src/<Project>/<Project>.csproj --configuration Release --output ./nupkgs
```

Container detection, automatic skipping and environment variable overrides for `./test.sh` are described in
`tests/CLAUDE.md`, which loads when you work under `tests/`.

## Architecture layers

The projects, their layers and every allowed dependency edge are in `docs/en/architecture/dependency-map.md`; this file does not
keep a copy.

> ⚠️ **`Polhem.Base` and `Polhem.Definition` are the two lowest assemblies.** Every project depends on
> `Polhem.Base`, and the direct consumers of `Polhem.Definition` span every layer. **Do not add package references
> to these two projects unless it is necessary**: any dependency added here spreads along the dependency chain to
> every consumer. `Polhem.Api.Contracts` is locked the same way. The criteria, the correct approach and the gates are
> in `rules/dependency-boundary.md`.

## Workflow

### Plan before you build

Any task that needs planning first (a refactoring, a new feature, an architectural change):

1. Write the plan as a Markdown file in `local/plans/`, named `<topic>.md` or `plan-<topic>.md`.
2. **Every time you create or change a plan, link it in your reply** with a relative Markdown link, so the user can
   open it from the conversation.
3. Wait for the user to confirm before carrying it out.
4. **When the plan is done, mark it completed at the top of the file straight away.**

A plan records what someone intended at the time; it is not a specification. Decisions of lasting value are promoted
to an ADR in `maintainers/adr/`; work that other maintainers need to see belongs in a GitHub issue or pull request.

### Local working documents

- `local/` at the repository root is ignored by git. Keep documents there that are not meant for every maintainer or
  for publication: plans, drafts, personal notes, and review findings that list unfixed security issues
  (`local/internal/`).
- Never commit anything under `local/`, never add it with `git add -f`, and never link to it or name a file in it
  from a committed file. `./check-public-docs.sh` reports such references.
- The language rule above does not apply to `local/`, because its documents are not maintained together.
- A session in a git worktree cannot see `local/`. Hand off work that depends on it to a session in the main
  working tree.

### Changes reach `main` through pull requests

How branches, pull requests and CI failures are handled is in `rules/pull-request.md`. Releases have two guardrails
that apply even when nobody asked for a release: `rules/releasing.md`.

## Architecture reference

Before implementing a feature or a module, read `docs/en/README.md` (the source; `docs/zh-TW/README.md` is its
translation): the index of the public documents, covering the architecture overview, development guidelines and
constraints, databases and design concepts. Then open the documents it points to. The background of design
decisions is in `maintainers/adr/`; the details of each package are in the `README.md` of each `src/` project.

**The pitfall log `maintainers/gotchas/`** (maintainer documents, not public) records pitfalls that have been hit
and are likely to be hit again, with the symptom, the root cause and the fix. Hard rules are already in `rules/`
(always loaded); the gotchas are context to read on demand. Before touching one of these areas, read the matching
file: **databases and provider dialects**, **serialization and the expression engine**, **Avalonia controls**,
**tests, CI and publishing**, **the Northwind heads**. The index is `maintainers/gotchas/README.md`.

The core mental model (anchors for implementation; the details are in the documents above):
- **FormSchema** is the definition hub. It drives the UI (FormLayout), the database (DbTable) and validation rules.
- **DataSet** is the DTO across layers. It carries master-detail data and no logic.
- A **Business Object (BO)** holds business logic and does not access the database directly.
- **Repository** has two tracks: CRUD is driven by FormSchema; reports and batch jobs are implemented by the BO
  itself (AnyCode).
- Architecture: a mix of N-tier, Clean Architecture and MVVM.

## Rules

These rules are part of the repository. Where a personal or user-level rule covers the same subject, the rule here
takes precedence in this repository.

@rules/code-style.md
@rules/scanning.md
@rules/single-source.md
@rules/pull-request.md
@rules/releasing.md
@rules/public-docs.md
@rules/dependency-boundary.md
@rules/database.md
@rules/definition.md
@rules/serialization.md
@rules/testing.md
@rules/security.md
@rules/sonarcloud.md
@rules/commit-verification.md
@rules/apple-mobile-trim.md
@rules/avalonia.md
