# ADR-045: English for everything maintained together; plans stay out of the repository

**English** | [繁體中文](adr-045-language-policy-and-local-plans.zh-TW.md)

## Status

**Accepted (2026-09-26)**

## Context

Polhem continues the Bee.NET framework (`jeff377/bee-library`) under a new name, in an organization meant to be
maintained by more than one person. Bee.NET was maintained by a single person, and three of its habits stand in the
way of that:

- **Language.** Comments, test descriptions, commit messages, the agent guidance under `.claude/` and the maintainer
  documents were written in Chinese, which a wider group of contributors cannot read. The users of the framework, on
  the other hand, include readers who rely on documentation in Traditional Chinese.
- **Plans in the repository.** Plans were committed under `docs/plans/` and archived there when done. A plan records
  what one person intended at the time. Once work moves on, it no longer describes current behavior, and public
  documents that linked to plans passed that outdated picture on to readers. A rule forbade those links, and a script
  checked for them, but the plans themselves kept accumulating.
- **Rules outside the repository.** Coding style, scanning, pull request and release rules lived in the maintainer's
  user-level agent settings. Other contributors, and their agents, could not see them.

Polhem.OAuth2, which went through the same move first, adopted the same language policy in its ADR-002.

## Decision

1. **Everything maintained together is written in English**: source code, XML documentation (which also ships to
   IntelliSense), comments, test method names and `[DisplayName]` text, commit messages, the maintainer documents in
   `docs/repo-ops/`, and the agent guidance in `.claude/` and every `CLAUDE.md`. This overrides any personal setting
   that asks for another language.
2. **Public documents and ADRs are bilingual**, English and Traditional Chinese, so that every contributor can read
   why the code is the way it is and users keep documentation in both languages.
3. **Plans and other personal working documents are not committed.** They live in `local/` at the repository root,
   which git ignores: plans in `local/plans/`, and review findings that list unfixed security issues in
   `local/internal/`. No committed file points to a file under `local/` or names a plan file;
   `check-public-docs.sh` reports both.
4. **What lasts is promoted.** A decision of lasting value becomes an ADR. Work that other maintainers need to see
   goes into a GitHub issue or pull request.
5. **The agent guidance is part of the repository.** The rules that used to live in user-level settings are committed
   under `.claude/rules/`, so every contributor's agent follows the same rules. The repository does not declare
   personal agent plugins.

## Consequences

- Translation happens in stages, so parts of the repository still contain Chinese for a while. New content is written
  in English regardless of the language around it.
- This ADR is written in both languages from the start. The earlier ADRs are still in Chinese only and get English
  versions later. No automated check covers the two language versions of an ADR yet; a change to one updates the
  other in the same commit.
- The history before the initial import stays in `jeff377/bee-library`, which is frozen and will be archived.
  References to its commits use full commit URLs, and references to its archived plans use URLs pinned to the
  commit the import started from.
- A plan cannot be shared through the repository. A session in a git worktree cannot see `local/` either, so work
  that depends on a plan is handed to a session in the main working tree.
- A contributor whose personal settings ask for another language gets English in this repository, because
  the repository's agent guidance states the policy explicitly.
