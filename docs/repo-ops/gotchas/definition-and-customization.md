# Pitfall log: the definition layer and the customization override layer

The matching hard rules are in `.claude/rules/definition.md`; the design rules for definition types are in
`src/Polhem.Definition/CLAUDE.md`.

## "How many kinds of customization scope are there" can be counted two ways, and each document used its own → the same omission hit three times

**Symptom**: when you want to know which definitions the customization override layer actually serves, the documents
give three answers that do not agree with each other, and each one sounds certain. The three places actually hit (all
found one after another between 2026-08-12 and 08-13):

| Document | What it said at the time | What it left out |
|------|---------|---------|
| The 2026-08-06 revision table in `docs/adr/adr-016-*.md` | "currently **five kinds**" | `MenuSettings` was not in the table at all |
| The "which one to use" table in `docs/en/customization.md` (and its `docs/zh-TW/` translation) | five rows | same as above |
| §7 of `docs/en/definition-files-overview.md` (and its translation) | "**four types**, three granularities" | `PluginSettings`, which is the only granularity that "adds" |

The three places **did not leave out the same item**, so cross-checking them does not catch it either: the first two
left out `MenuSettings`, the third left out `PluginSettings`.

**The root cause is not that someone was careless. "Customization scope" can inherently be counted two ways, and the
three places each used their own while each writing down a single number**:

- Counted by **definition file** → **five**: `Language` / `FormLayout` / `ProgramSettings` / `PluginSettings` /
  `MenuSettings`. **This number is stable**, because it is the number of methods of `ICustomizeDefineReader`.
- Counted by **"what you want to change"** → the number depends on how finely you cut: the two bindings of
  `ProgramItem` (`BusinessObject` / `Repository`) each count as a separate item, and the text and the option sets of
  `Language` are two granularities that count as two items.
  So the same mechanism can be described as five, six or seven items, and **none of them is wrong**.

As a result, "five kinds" in the ADR meant intents, and elsewhere it may have meant files. Both sides said five with
different contents, and **looking consistent made the missing item even harder to spot**.

**Fix**:

1. **To find out the customization scope, read the source code, not the documents.** The authoritative sources are
   the XML doc of `src/Polhem.Definition/Customization/CustomizeOverlay.cs` (which lists each granularity and its
   reason) and `src/Polhem.Definition/CustomizeOnlyPathOptions.cs` (which states "the override layer serves only
   those five types"). These two are more precise than any document.
2. **When a document writes a number, it must say whether it counts files or intents.** Writing only "five kinds"
   without the way of counting is exactly why the next missing item will go unnoticed.
3. **When adding a sixth kind of definition to the override layer, three places must be updated together** (the
   current-state table in ADR-016, the "which one to use" table in both languages of `customization`, and the §7 table
   in both languages of `definition-files-overview`), not just the one closest to you.

**Fixed**: [`7160afd2`](https://github.com/jeff377/bee-library/commit/7160afd2) (added `PluginSettings`), [`c4014ee5`](https://github.com/jeff377/bee-library/commit/c4014ee5) (added `MenuSettings`).
**Remaining caveat**: the three tables still each use their own way of counting; they now just say which one.
**No mechanism keeps them in sync with `ICustomizeDefineReader`**; when a sixth kind is added, it still depends on
someone remembering.

## The granularity of the customization override layer is not "the finer the better"; `PluginSettings` is the only one that adds

It is easy to assume the granularity is a result of implementation convenience. In fact the dividing line is **the
nature of the thing** (the full reasons are in the XML doc of `CustomizeOverlay`):

| Nature | Granularity | Which |
|------|------|------|
| A bag of independent values | Layered per key | The text of `Language` |
| A whole that only holds together as a combination | Replaced as a whole file / whole set | `FormLayout`, `MenuSettings`, the option sets of `Language` |
| A set of independent bindings | Per progId, then per property | `ProgramSettings` |
| **A chain executed in order** | **Added per progId** | **`PluginSettings`** |

`PluginSettings` is a category of its own because it is neither of the others: a plugin is by nature "add a step",
not "replace a step", so the packaged chain runs first and the customized chain runs after it, and the two layers do
not exclude each other. **That is also why it is the one most easily left out of documents**: it does not fit the
"pick one" mental model, and whoever writes the table misses it when counting "how many granularities there are".

⚠️ **A related point**: `PluginSettings` is also **the only writable customized definition** (the `LocalOnly`
maintenance API); everything else in the customization layer is read-only. So when "the customization layer is
read-only" is quoted, add the exception, otherwise it contradicts the existence of the maintenance API.

## The `FormSchema` hub diagram draws three layers, but the "database" box is not only `TableSchema`

**Symptom**: the diagram in §2 of `definition-files-overview` drew the downstream of `FormSchema` as
`FormLayout` / `TableSchema` / rules, while **the "towards the database" item right below it talked about "generating
SQL from `FormSchema` at runtime"**. The diagram showed structural definitions and the text talked about runtime
access; the two are not the same thing, and readers could not match them up.

**Root cause**: `FormSchema` has **two** derivations towards the database, at completely different times:

- **Scaffold time**: a `TableSchema` is produced from `FormSchema` (after that it is an independent definition file
  and no longer follows it)
- **Runtime**: every request builds SQL on the spot from `FormSchema` (no ORM, no entity classes)

Putting both in the same box and writing only the first one makes the most distinctive half of the framework
(building SQL at runtime) disappear from the diagram.

**Fixed**: [`7160afd2`](https://github.com/jeff377/bee-library/commit/7160afd2); the box was changed to `TableSchema ＋ 執行期 SQL` (in the English source today: `+ runtime SQL`), and the caption row was changed to "where it is stored · how it goes in and out".

⚠️ **One place that was re-checked and needs no change**: the overall architecture diagram in §11 of
`docs/en/architecture-overview.md` (and its translation) also has the two nodes `FormSchema → FormLayout / TableSchema`, which at first glance
looks like the same problem. **It is not**: that is a **layered** architecture diagram, and SQL generation hangs off
the Repository layer below it ("FormSchema-driven (CRUD SQL auto-generated)"), which is the correct place.
It is recorded here so that the next person who sees those two nodes does not reopen the same check.
