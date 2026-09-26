# Single authoritative source: documents point, they do not copy

**When information has a single authoritative source, documents only point to it; they do not keep a copy.**

To decide: **does this content have an authoritative source such that "if that changes, this must change too"?**
If so, only point to it.

The reason: the copied part **drifts every time, and no mechanism will notice**. The compiler does not read
documents, tests do not run them, CI does not check them. This is **a structural problem, not a discipline
problem**: "remember to change both" does not work.

## Common content and its authoritative source

| Content | Authoritative source |
|---------|----------------------|
| Version numbers ("this project's version: x.y.z") | The repository's version file (`Version.props` / `Directory.Build.props` / `package.json`…) |
| Enum members, API signatures | The XML docs / type declarations in the source code |
| Container names, environment variable defaults | The header of that script |
| Behavior and flags of an executable | The file itself (`--help`) |
| Remedy steps for a build gate | The text of the error message |
| Inventory counts such as numbers of projects / places / lines | Do not write them (they always drift and are useless to readers) |

## Judging an existing copy

When you find something that looks like a copy, **judge each one; do not blindly clear them all**:

**Does this passage say "what the state is now", or "what was measured at the time"?**

- The first is a **copy** → change it into a pointer. **Do not update it to the new current value**; it will just
  drift again.
- The second is a **record** → keep it (for example an ADR's "before this decision v4.18.0 had 37 → after it v4.19.0
  had 185").

## Exceptions

If some place truly must keep a copy (for example a generator's template, or a specification summary that must be
readable offline) → **the exception must be listed explicitly and covered by some check** (a test, a script, CI);
otherwise it is the next thing to drift.

> Two real cases: the version in `.claude/CLAUDE.md` stayed at 4.13.0 while the actual version was 4.19.0, and was
> only caught by a documentation sweep six minor versions later; a publishable project that did not inherit the
> shared version file kept its own version at 4.8.0 for twelve whole minor versions.
> **Neither time did any mechanism notice.**
