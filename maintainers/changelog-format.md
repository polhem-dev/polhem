# How to write the changelog

The changes of each version are written in two layers, for two kinds of reading:

| Layer | Files | Read to answer |
|-------|-------|----------------|
| Index | `CHANGELOG.md`, `CHANGELOG.zh-TW.md` | "What is in this version, and does anything break for me?" |
| Release notes | `docs/<lang>/changelogs/<version>.md` | "What exactly changed, and what do I do about it?" |

The index is scanned; the release notes are read by someone upgrading. Everything a reader has to act on is in the
release notes, so the index can stay short.

## What goes in at all

Only changes a consumer of the packages can see: public API, behavior, the wire, supported platforms, published
tools and the public documents under `docs/`. Tests, CI, internal refactorings, the maintainer documents and the
agent guidance are left out of both layers; the pull request records them.

## The index: `CHANGELOG.md`

Each version has a heading, a link to its release notes, and three to six entries:

```markdown
## [1.7.0] - YYYY-MM-DD

Details and migration steps: [docs/en/changelogs/1.7.0.md](docs/en/changelogs/1.7.0.md)

- **Breaking:** The server side of `Polhem.Api.Core` moves to the new `Polhem.Api.Server` package.
- **Breaking (wire):** <a wire change that an existing client or server notices>.
- `FormView` and `ListView` run without a Polhem backend, on a schema, layout and connector the host supplies.
```

- **One sentence per entry.** Name the change, not the members it touches and not the migration: those belong in the
  release notes.
- **Breaking changes first**, prefixed **Breaking:**, or **Breaking (wire):** for a change to `wire-contracts/`,
  `wire-fixtures/` or the JSON-RPC error codes. Every breaking change of the version gets an entry, even when that
  takes the list past six; group related ones into one entry rather than dropping one.
- No `###` subsections. The prefix carries the one distinction a reader scans for.
- `[Unreleased]` follows the same shape and links to the release notes of the version it will become. The version
  follows [ADR-052](adr/adr-052-breaking-changes-in-1-x-minors.md): a breaking change makes it the next minor.
- `CHANGELOG.zh-TW.md` is changed in the same commit. No script checks the two root files against each other.

## The release notes: `docs/en/changelogs/<version>.md`

The source is `docs/en/`; `docs/zh-TW/` is its translation, restamped with `./check-docs-i18n.sh --stamp` (see
`.claude/rules/public-docs.md`). Every file has the same two sections, in this order:

```markdown
# Polhem 1.7.0 — Release Notes

[繁體中文](../../zh-TW/changelogs/1.7.0.md)

<One paragraph: the theme of the version, and whether its wire is compatible with the previous version.>

## New features and changes

### <Title>

<What it is and how to use it. An example when it helps.>

## Breaking changes

### <Title>

- **Affects:** <which applications notice: be specific, such as "servers with a custom `IApiAuthorizationValidator`">
- **Before:** <the old behavior or shape>
- **Now:** <the new behavior or shape>
- **Migrate:** <the steps; a numbered list when there are several, and a table of old → new for moved types>
- **Background:** <a link to the ADR; leave the line out when there is none>
```

- When several kinds of application are affected differently, the opening paragraph may be followed by a short list
  that names, for each kind, the breaking changes it has to act on. It points to the items; it does not repeat them.
- When a section has nothing, it says `None.` rather than disappearing, so a reader knows nothing was missed.
- Reasons and measurements go in the item they explain, not in a section of their own. Long reasoning belongs in the
  ADR the item links to.
- A behavior change that existing code notices without a compile error (a default that changes, a check that now
  rejects something) is a breaking change and uses the template, even though the API does not change.

## Before a release

The breaking changes the analyzer catches show in the diff of `PublicAPI.Unshipped.txt`. Behavior changes and binary
breaks it cannot see (an optional parameter added to an existing public member, for one) have to be found by reading
the merged pull requests. Check that each has an entry in both layers.
