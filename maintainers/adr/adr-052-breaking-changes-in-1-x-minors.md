# ADR-052: Breaking changes are allowed in 1.x minor versions; strict semantic versioning starts at 2.0

## Status

**Accepted (2026-10-10)**

Replaces the compatibility rule that [ADR-046](adr-046-api-evolution-policies-for-1-0.md) states in its context ("a
change that breaks it waits for the next major version") and the statement in the 1.1.0 release notes that "from
1.1.0 on, 1.x follows semantic versioning without exceptions" ([ADR-048](adr-048-rename-base-to-core-in-1-1.md)).
The decisions of ADR-046 themselves stay; how they apply from 2.0 is under decision 5.

## Context

1.0.0 fixed a public API baseline and declared semantic versioning. Within the next four minor versions, four changes
broke it, and each needed its own ADR to say why it was an exception:

| Version | Date | Exception |
|---------|------|-----------|
| 1.1.0 | 2026-09-30 | `Polhem.Base` renamed to `Polhem.Core` ([ADR-048](adr-048-rename-base-to-core-in-1-1.md)) |
| 1.2.0 | 2026-10-03 | `Polhem.Api.AspNetCore` and `ApiServiceOptions` removed ([ADR-049](adr-049-jsonrpc-packages-in-1-2.md)) |
| 1.3.0 | 2026-10-05 | Payload wire format incompatible with 1.2 ([ADR-050](adr-050-jsonrpc-1-1-wire-break-in-1-3.md)) |
| 1.4.0 | 2026-10-08 | `Polhem.Api.Core.Messages.PayloadFormat` and `Gzip` removed ([ADR-051](adr-051-remove-jsonrpc-leftovers-in-1-x.md)) |

ADR-048 promised that it would be the only one. The three that followed show that the rule does not describe how the
framework is actually developed: the public surface is still being shaped, and keeping every intermediate shape
until 2.0 was judged worse each time.

Nothing enforces the rule either. `PublicAPI.Shipped.txt` records the surface, and the pre-commit hook prints a
notice when `PublicAPI.Unshipped.txt` changes, but neither fails on a removal or a binary-incompatible change. The
"follows Semantic Versioning" line at the top of `CHANGELOG.md` is therefore a guarantee with no mechanism behind it,
and it is no longer true.

The cost of breaking is low at this stage. On 2026-10-10 each Polhem package on NuGet showed between 40 and 65 total
downloads across all versions, consistent with CI and mirrors rather than adopters. The only known downstream
application is polhem-northwind, maintained in the same organization and pinned to an exact version.

The change that prompted this ADR is a composition root for `Polhem.Api.Client`: a client object that owns the
connection and the signed-in identity, replacing the per-connector access token, the Local/Remote constructor
overloads and the process-wide `ApiClientInfo`. Done additively, it would leave every connector with four legacy
constructors next to the new one, and `ApiClientInfo` as a compatibility shell, for as long as 1.x lasts.

## Decision

### 1. A 1.x minor version may make breaking API changes

During 1.x, a **minor** version may remove, rename or change public API, members and types included. A **patch**
version may not: it fixes defects and does not remove or change anything in `PublicAPI.Shipped.txt`.

Every breaking change is listed under "Breaking API changes" in `CHANGELOG.md` (and its translation), with the
migration step a consumer takes, and described in that version's detailed notes under `docs/en/changelogs/`.

A breaking change no longer needs an ADR of its own to justify the break. An ADR is still written when the change
carries a design decision worth recording, as for any other change.

### 2. Wire breaks follow the same rule, with the downstream client in step

A change to `wire-contracts/`, `wire-fixtures/` or the JSON-RPC error codes may also ship in a 1.x minor version. It
is listed under "Breaking wire changes", and the matching polhem-connector-js change is planned for the same release,
as `.claude/rules/serialization.md` already requires for any wire change. A 1.x server and a client from a different 1.x
minor version are not guaranteed to interoperate.

### 3. What does not change

- `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` are kept as they are, so every break shows in the diff of a
  pull request.
- The pre-commit hook's notice on `PublicAPI.Unshipped.txt` stays; the judgement it asks for becomes "is this break
  listed in the changelog with a migration step", not "is this break allowed".

### 4. Consumers pin to a minor version

`CHANGELOG.md` and the root `README.md` (both languages) state this policy and recommend that consumers reference
the packages with a range that stays within one minor version, for example `[1.5,1.6)`. A floating `1.*` reference
can break on any minor upgrade during 1.x.

### 5. From 2.0, semantic versioning is enforced by package validation

2.0.0 starts strict semantic versioning, and the same release turns on the .NET SDK's package validation for every
packable project: `EnablePackageValidation` with `PackageValidationBaselineVersion` set to the previous release.
`dotnet pack` then compares each package with its baseline and fails on an API or binary incompatibility, including
the binary breaks that `PublicAPI.Unshipped.txt` cannot catch, such as adding an optional parameter to an existing
public constructor.

- API that is going away is marked `[Obsolete]` in at least one minor version before the major version that removes it.
- The exemptions of ADR-046 apply within 2.x: decision 2 (the listed host-replaceable interfaces may gain members in
  a minor version) and decision 4 (exception types may change in a minor version). Package validation reports those
  changes, so each one is recorded in the project's `CompatibilitySuppressions.xml`, which makes the exemption visible
  in review.
- ADR-046 decisions 1 and 3 describe how the synchronous server path and the static configuration can still evolve
  additively; within 1.x they no longer bind, because a change to either may now simply break.

### 6. When 2.0 is released

2.0.0 is released at the first of:

- a consumer outside the polhem-dev organization is known to depend on the packages, or
- two consecutive minor versions ship with no entry under "Breaking API changes" or "Breaking wire changes".

Either is a signal that the cost of breaking has started to exceed the cost of keeping a shape. The maintainer may
release 2.0 earlier; the conditions make sure it is not deferred indefinitely.

## Consequences

- **A 1.x version number says nothing about compatibility.** Consumers learn it from the changelog, and the pinning
  recommendation is what protects them.
- **No more exception ADRs.** The four exceptions above become ordinary 1.x changes in hindsight; their ADRs stay as
  the record of why each change was made.
- **Changes can be made in their final shape.** The `Polhem.Api.Client` composition root can replace the connector
  constructors and `ApiClientInfo` in one release instead of carrying both paths.
- **Downstream applications move with each minor upgrade.** polhem-northwind updates its code when it moves to a
  minor version that breaks something it uses.
- **2.0 brings a build-time gate.** Until then, the absence of a mechanism is stated rather than hidden behind a
  guarantee.

## Alternatives considered

### Keep strict semantic versioning, with an ADR for each exception

The current practice. Rejected: four exceptions in four minor versions show the rule is not followed, and each
exception costs an ADR whose real content is "the old shape was not worth keeping". The changelog meanwhile keeps a
guarantee that readers, agents included, may rely on.

### Release each break as a new major version

Strictly correct under semantic versioning. Rejected: at the current pace it would produce a major version every few
days, which tells a consumer no more than this policy does, and it spends 2.0 as the milestone that marks a stable
surface.

### Stay additive within 1.x and remove in 2.0

Add the new shape next to the old one, mark the old one `[Obsolete]`, and remove it in 2.0. Rejected for 1.x: it
doubles the surface while it is still changing (for the client composition root, four legacy constructors on each
connector plus a compatibility `ApiClientInfo`), and `[Obsolete]` itself breaks the build of a consumer that treats
warnings as errors. The cost protects adopters who are not known to exist. From 2.0 this is exactly the policy
(decision 5).

## Implementation

When this ADR is accepted:

- `CHANGELOG.md` and `CHANGELOG.zh-TW.md`: replace the "versions follow Semantic Versioning" sentence with the 1.x
  policy and the pinning recommendation.
- `README.md` and `README.zh-TW.md`: add the same short statement where installation is described.
- ADR-046 and ADR-048: add a line to their Status sections pointing to this ADR.
- `maintainers/adr/README.md`: add this ADR to the index.
