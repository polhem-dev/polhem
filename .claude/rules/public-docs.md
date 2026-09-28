# Public documents

**A public document (a user document) is written for developers outside the framework: people who use the NuGet
packages.** To decide: is the intended reader "someone building an application with Polhem" or "someone maintaining
Polhem"? The first is a public document and is multilingual; the second is a maintainer document and is English only,
with no translation in the repository. The reasons are in
[adr-047](../../docs/adr/adr-047-documents-split-by-reader.md).

## What is public

| Scope | Content |
|-------|---------|
| Repository root | `README.md` / `README.zh-TW.md`, `CHANGELOG.md` / `CHANGELOG.zh-TW.md` |
| `docs/changelogs/` | The detailed notes of each version behind the root changelog, `<version>.md` / `<version>.zh-TW.md` |
| `docs/README.md` and `docs/<lang>/` | The language entry page and every `.md` under each language folder (architecture overview, API reference, database guides, glossary, development guidelines and constraints, and each language's `README.md` index) |
| **Every** `README.md` / `README.zh-TW.md` | Wherever it is: `src/*/`, `samples/*/`, `apps/*/`, `tools/*/` |
| XML documentation (`///`) in `src/**/*.cs` | Ships in the package's `.xml` file and appears in the consumer's IntelliSense, so it has the same readers as a README |

## What is not

| Scope | Nature |
|-------|--------|
| `local/` | Ignored by git. Each maintainer's own working documents: plans, drafts, notes, reviews that list unfixed security issues. **Nobody else can open them.** |
| `docs/repo-ops/` | Operational documents for this repository (CI, branch protection, gotchas). Not relevant to framework users |
| `docs/adr/` | The decision records. Written for the people who change the design; public documents may still link to them for the reasons behind a design |
| `CONTRIBUTING.md` | Written for contributors. English only, and may point into `.claude/` |
| `.claude/` (`CLAUDE.md`, `rules/`, `skills/`, `commands/`) and the `CLAUDE.md` files elsewhere | Engineering guidance for agents, not product documentation |

## Hard rules

### 1. No committed file points to `local/`, and none names a plan

Plans live in `local/plans/`, which git ignores. A contributor's clone, CI and a reader on GitHub cannot open them,
so every reference to a plan from a committed file is a dead pointer for everyone but its author. A plan also records
what someone intended at the time, not current behavior.

This holds **for every committed file**, not only public documents: `docs/repo-ops/`, `.claude/`, test comments and
source code included. It also holds **for every form of reference**: a markdown link, a path in backticks, or a file
name in prose.

```markdown
<!-- ❌ Not allowed: an ADR pointing to a plan -->
The implementation steps are in [the plan](../../local/plans/<topic>.md).

<!-- ❌ Not allowed: a bare path is a reference too -->
> Details: `local/plans/<topic>.md`.

<!-- ✅ Write the conclusion into the ADR itself, or point to another committed document -->
The scope and the exceptions are described under "Outcome and final scope" below.
```

Describing the convention itself (for example "plans go in `local/plans/`") names a directory, not a document,
and is fine.

The plans of the Bee.NET period stay readable in `jeff377/bee-library`, which is frozen and will be archived.
A full URL to a file there, pinned to a commit, is a pointer readers can follow and is allowed.

### 2. Where background belongs instead

Wanting to cite a plan means the content has not reached its proper place yet:

- A design decision and its reasons → the relevant **ADR**, the long-lived decision record.
- How something is used or behaves → the relevant public document under `docs/`.
- Where something is implemented → the **source path** itself (`src/Polhem.Db/DbAccess.cs`).
- A version change → `CHANGELOG.md`.
- Work that other maintainers need to see → a GitHub issue or pull request.

If the history only exists in a plan and is not worth promoting, **do not cite it**.

### 3. Public documents do not point to files under `.claude/`

Agent guidance is not product documentation, and a path under `~/.claude/` (a user's home directory) cannot be opened
by anyone else at all. Naming the directory to describe a convention is fine. Maintainer documents (`docs/repo-ops/`,
the ADRs, `CONTRIBUTING`) and the agent guidance itself may point to files under `.claude/`.

### 4. Keeping languages in sync

**Under `docs/`**: `docs/en/` is the source and every other language folder is a translation. The detailed
changelogs in `docs/changelogs/` pair by file name instead: `<name>.md` is the English source and `<name>.zh-TW.md`
next to it is a translation. The ADRs in `docs/adr/` are maintainer documents and have no translation. The
list of languages, each one's policy (strict / partial) and the folders that pair by file name are written only in the
header of `check-docs-i18n.sh`; they are not repeated here. That script enforces them, and the Docs Check workflow runs
it on every push.

- After changing a source document, update the matching translations before pushing and restamp them with
  `./check-docs-i18n.sh --stamp <translation path>`. Without the stamp the script reports the translation as stale,
  and a strict language turns CI red. The source and its translations may land in separate commits, but push them
  together: CI checks the HEAD after the push.
- **A stamp declares "this has been checked against the source".** The script does not and cannot verify the
  translation. Stamping without checking turns this check off.
- The language switch line is always generated by `./check-docs-i18n.sh --fix-switch`, never edited by hand.

**Outside `docs/`** (every `README.md` / `README.zh-TW.md`, the root changelog): no such mechanism exists. Change
both files.

## Checking

After changing documents, or when something may have been missed, run:

```bash
./check-public-docs.sh
./check-md-links.sh
```

`check-public-docs.sh` exits 1 when any of (1) to (3) prints a hit: (1) a committed file names a plan file,
(2) a committed file points to a file under `local/`, (3) a public document points to a file under `.claude/`.
The reasons for each check are in the script header. **Do not narrow the scope or the file types.**

**(4) is advisory and has known false positives; read each hit**:

| False positive | Example | Why it is not a violation |
|----------------|---------|---------------------------|
| `plan` is an API or type name | `Orchestrator.Plan(diff)`, `UpgradePlan`, `plan.Warnings` in `docs/*/database-schema-upgrade.md` | It names code, not a working document |
| Future work that has no document yet | "a separate plan" in adr-023 | It means "handled separately" and points to nothing readable |
| Describing the plan convention itself | adr-045 on why plans are kept out of the repository | It explains the rule, not a document to open |

To decide: **does the sentence send the reader to open a document they cannot open?** Only that is a violation.

The fix is always the same: **keep the substance, remove the pointer**; if further reading is needed, point to a
committed document.
