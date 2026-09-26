# Project skills

The skills in this folder are Polhem's **engineering conventions** (committed with the repository, like
`.claude/rules/`). Each skill has its own folder with a `SKILL.md`. Claude decides when to load one from the
`description` in the `SKILL.md` frontmatter, and a user can also invoke one with `/<skill-name>`.

> **The authoritative source is the frontmatter `description` of each `SKILL.md`**: that is the full description the
> model uses to trigger the skill. This README is a one-line index for **people**; it does not copy the full
> descriptions, so that the two cannot drift. To learn exactly what a skill does and when it triggers, open its
> `SKILL.md`.

## Developing with the Polhem framework

| Skill | Purpose in one line |
|-------|---------------------|
| **polhem-jsonrpc-backend** | Build a JSON-RPC back-end server (ASP.NET Core) and client calls from scratch: bootstrap, `Define/` XML, a custom BO, demo sign-in |
| **polhem-app-scaffold** | Wiring conventions for a standalone Polhem back-end app or demo (database scope, auth, seeder) |
| **polhem-add-form** | Add a CRUD form to an app that is already wired up (definition changes only, no UI code) |
| **polhem-add-bo-method** | Add a public BO method (across contract, wire, BO, Repository and client) |
| **polhem-add-cache-object** | Add a framework cache object (definition cache or database-dependent cache) |
| **polhem-scaffold-from-formschema** | Generate TableSchema and bilingual LanguageResource from a FormSchema; FormLayout only for a custom layout |
| **polhem-serialization** | Design guide for objects serialized three ways (XML for persistence, JSON/MessagePack on the wire) |
| **polhem-framework-review** | Method for a full framework review (read-only review by aspect, graded refactoring plan) |
| **polhem-sample-add** | Add a new sample project under `samples/` |
| **polhem-load-test** | Run load tests with `tools/Polhem.LoadTests` and read the results (prerequisites, Local / Remote, when the numbers cannot be trusted) |

> **`polhem-jsonrpc-backend` is the single authoritative source for the host bootstrap, the empty controller, the
> sign-in trio and client calls** (templates in its `references/`). `polhem-app-scaffold` builds on it and only adds
> **database scope, company context and the seeder**, pointing to it instead of repeating it.
>
> Choosing between them: for a working **JSON-RPC server and client round trip**, use the former; when you also need
> **per-company databases and a seeder**, use the latter. They also differ in the BO: the former uses a minimal
> `BusinessObject`, the latter a `FormBusinessObject`.

## General workflow (not tied to the framework)

| Skill | Purpose in one line |
|-------|---------------------|
| **demo-smoke** | Run an end-to-end smoke test of a demo under `samples/` or `apps/` |

## Adding a skill

1. **First decide whether it belongs here.** This folder only holds **engineering conventions tied to Polhem**.
   Anything unrelated to this repository (writing, personal workflow, other technology stacks) belongs in the
   user-level `~/.claude/skills/`. Putting it here and excluding it with `.gitignore` is **wrong**: then it can only
   be invoked in this repository.
2. Create `.claude/skills/<name>/SKILL.md`, with at least `name` and `description` in the frontmatter.
3. The `description` must say clearly **what it does and what the user says when it should trigger**; that decides
   whether the model loads it at the right time.
4. Add a one-line entry to the matching table in this README.
5. **Commit it.** This folder has no `.gitignore` exceptions.
