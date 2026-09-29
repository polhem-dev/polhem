# ADR-047: Documents are split by reader: user documents are multilingual, maintainer documents are English only

## Status

**Accepted (2026-09-29)**

Supersedes item 2 of [ADR-045](adr-045-language-policy-and-local-plans.md) ("Public documents and ADRs are
bilingual").

## Context

[ADR-045](adr-045-language-policy-and-local-plans.md) made everything maintained together English, and made public
documents and ADRs bilingual. Three things about the result did not fit a framework whose user documentation will
keep growing:

- **Two readers share one folder.** `docs/` holds the documents for developers who build applications with Polhem,
  the ADRs, the maintainer documents under `docs/repo-ops/`, and the state file of a maintenance command. A reader
  cannot tell from the path which documents are written for them.
- **Two rules for translations.** The user documents pair by language folder (`docs/en/`, `docs/zh-TW/`), while the
  ADRs and the detailed changelogs pair by file name (`<name>.md` next to `<name>.zh-TW.md`). The ADR folder alone
  holds every decision twice, the two languages interleaved.
- **ADRs are translated, and hardly any project does that.** An ADR records the reasons behind a design; its readers
  are the people who change that design. Keeping every ADR in two languages doubles the cost of each decision record
  for readers who already read the English source code, XML documentation and maintainer documents.

The users of the framework are a different group. They include readers who rely on documentation in their own
language, and the user documents are where a second language pays for itself.

## Decision

1. **User documents are multilingual.** They are written for developers who build applications with Polhem: the
   documents under `docs/`, every `README`, the root changelog and the detailed changelogs. English is the source
   language. Translations follow the language folder layout (`docs/<language>/`), which is the only pairing rule
   under `docs/`. Single documents outside `docs/` (a `README`, the root changelog) keep the `.zh-TW.md` suffix.
2. **Maintainer documents are English only.** They are written for people who maintain Polhem: the ADRs, the
   operational documents and gotchas, `CONTRIBUTING`, and the agent guidance. The repository keeps no translation of
   them.
3. **Maintainer documents move out of `docs/`** into `maintainers/` at the repository root, the ADRs included, so
   that `docs/` holds only user documents. Tool state does not belong in either and moves next to the tool.
4. **A maintainer who wants a maintainer document in another language keeps that translation outside the
   repository.** It is a personal reading aid, so it follows the same rule as plans in ADR-045: nothing committed
   points to it.

## Consequences

- The Traditional Chinese translations of the ADRs and of `CONTRIBUTING` are removed from the repository, and
  `check-docs-i18n.sh` no longer checks ADR translations.
- The move to `maintainers/` changes the path of every ADR. Links in source comments, XML documentation, the agent
  guidance and the user documents are updated in the same change. Links from outside the repository to the old paths
  break; no redirect is kept.
- The user documents may still link to ADRs for the reasons behind a design. A reader of a translated user document
  follows that link to an English ADR.
- The detailed changelogs follow the language folder layout like the other user documents, so the file name pairing
  rule disappears from `docs/`.
- The rest of ADR-045 still holds: English for everything maintained together, plans out of the repository, lasting
  decisions promoted to ADRs, and the agent guidance committed.
