# Code style

## Naming

| Element | Rule | Example |
|---------|------|---------|
| Interface | `I` prefix + PascalCase | `IKeyObject` |
| Class / property / method | PascalCase | `SessionInfo`, `ValidateAccess` |
| Attribute | PascalCase + `Attribute` suffix | `ApiAccessControlAttribute` |
| Private field | `_camelCase` | `_accessToken` |
| Parameter | camelCase | `accessToken` |
| Extension method class | `<TypeName>Extensions` | `StringExtensions`, `ExceptionExtensions` |
| Noun-style static utility | `<Domain>Utilities` or a plain noun | `StringUtilities`, `ValueUtilities`, `Gzip`, `XmlCodec` |

> **The `*Func` suffix is deprecated** (removed everywhere on 2026-05-01). Do not add new ones.

## Static utility classes and extension methods

Place each new method according to what it is. **Do not create grab-bag shared classes**:

| Path | When | Naming / location |
|------|------|-------------------|
| **A. Use the BCL directly** | The BCL already has an equivalent | No wrapper; the caller writes it inline |
| **B. Extension method** | The first parameter is a BCL or domain type and the call reads as "subject does action" | `<TypeName>Extensions`, in the same namespace as the target type |
| **C. Noun-style static utility** | A pure set of functions with no natural domain home | `<Domain>Utilities` or a plain noun |
| **D. Move into a domain class** | The method is really part of an existing responsibility of a domain object | A static or instance method on that object |

### Details

- **Do not extend `object`** (it pollutes IntelliSense for **every** type) → take path A inline or a path C
  noun-style static.
- **Do not write an extension with the same name as a BCL instance method.** The BCL instance method always wins
  (C# member resolution), so the extension is permanently hidden. When `string.StartsWith` / `Contains` / `IndexOf`
  would collide, take path C.
- **The framework encapsulates culture and case defaults**; callers pass a parameter only to override them.
  **Do not scatter `string.Format(CultureInfo.InvariantCulture, ...)` across callers.** The default splits in two:
  - **Identifier-like strings** (collection keys, field names, ProgIds, table names, file names, type names) →
    always `Ordinal` / `OrdinalIgnoreCase`. Culture-independent, fastest, and immune to the **Turkish-I** problem
    (under a Turkish culture, `CurrentCultureIgnoreCase` judges `ID` ≠ `id`).
    **Never use `CurrentCultureIgnoreCase` on a key.**
  - **Display text or sorting shown to people** → only then `InvariantCulture` or an explicit `CultureInfo`.
  - To decide: is this string used to **compare identity** or to **present to a person**? Identity comparison is
    always Ordinal.
- **Keep public framework APIs that have zero callers** (such as the `ValueUtilities.Cxxx` family). Delete only
  pure BCL wrappers with zero callers.
- **Remove pure facades**: do not keep a one-line delegating wrapper; expose the internal container and let callers
  use it directly.
- **Do not create classes for a hypothetical future**: find a home for each item one by one, and move only the
  minimum needed now.

### Name collisions: avoid both layers

**Layer one: the BCL (CA1724, enforced by the analyzer).** A noun-style utility must not share its name with the
last segment of any BCL namespace. Under `TreatWarningsAsErrors=true` the build fails. Pitfalls: `Http`, `Json`,
`Xml`, `Linq`, `Threading`, `Diagnostics`. Fix: add a `Utilities` suffix or a domain prefix.
**Avoid `*Helper`** (outdated in .NET and collides easily).

**Layer two: UI frameworks (no analyzer tells you).** Public types in the definition and model layers are consumed
by **every UI head**, so their names must avoid type names common in UI frameworks. The collision only surfaces on
the consumer side, as `CS0104` (ambiguous reference) or as "needs a `using` alias". And it surfaces exactly in the
code that "builds UI from definitions", which is the one place that has `using` for both namespaces. So the
collision is close to certain, not accidental.

- Pitfalls: `MenuItem` (taken by WPF / WinForms / Avalonia / DevExpress), `Control`, `Panel`, `Grid`, `Column`,
  `Style`, `Binding`, `Command`, `Page`, `Window`, `Border`, `Label`, `Image`, `Menu`
- Fix: add a domain prefix (`Column` → `LayoutColumn`) or use a synonym that does not collide
  (`MenuItem` → `MenuEntry`)
- To decide: **if you paste this type name into a UI project, will it need a `using` alias to compile?**
  If so, rename it.

> Renaming a public definition type later is **a breaking change** (the XML element name follows the type name),
> so avoid collisions when designing it.
> Existing examples: `LayoutColumn` / `LayoutGrid` / `LayoutField`.

### Path D shadowing check

Before moving a method into its owning class as a `private static`, confirm that no member of the enclosing type
collides with a type name the method body uses (C# member lookup: members of the enclosing type take precedence
over types in the namespace). On a collision **take path C instead**; a collision is often the sign that path D does
not apply.

## File organization

Each package is split into folders by feature (`Attributes/`, `Exception/`, `<Feature>/`). Interfaces may sit next
to the feature they belong to; they do not have to be gathered in `Interface/`.

### One type per file

**Each class, interface and enum has its own `.cs` file, and the file name is the type name.** Unless there is a
specific reason, do not put several types in one file.

- **Specific reasons that allow sharing a file**: tightly coupled nested/private helper types that are never used on
  their own; the arity overloads of a single generic type; a group of tiny marker `record`s/`enum`s that
  semantically form one set; a `<TypeName>Extensions` that carries displaced members (see below).
  **When in doubt, split.**
- **Not a specific reason**: a single `enum` that merely "goes with" the main type in the file (such as
  `LoginEvent` for `LoginAuditEntry`). Split it out.

#### Exception: a `<TypeName>Extensions` that carries displaced members shares the target type's file

When extension methods **should have been members of the type itself but a platform limit forced them outside**,
they share the target type's file. Only side by side can a reader see "why this is an extension and not a member".
The typical case is convenience `Add` overloads on a collection: a `KeyCollectionBase<T>` subclass may expose only
one public instance `Add`, otherwise the reflection-only `XmlSerializer` on iOS (which resolves it with
`Type.GetMethod("Add")`) throws `AmbiguousMatchException`.

> **General-purpose extension method classes still get their own file.** Do not stretch this exception.

#### Splitting a large type with partial

When a single type is too large (signal: **> 500 lines**) and its responsibilities fall into groups, split it into
files by responsibility with `partial class`. This **does not violate one type per file** (it is still one type),
but follow these rules:

- **Name files `<TypeName>.<Concern>.cs`** (`DbAccess.Async.cs`); `Part1` / `Part2` are forbidden.
- **Split along responsibility boundaries**: follow `#region`s if they exist; otherwise identify the natural
  grouping of members first, then split.
- **Do not put other types in a split file.**
- **Over the line means split; the only exception is "there is no seam".** There is no "it is highly cohesive, so
  don't split" way out. **A file is the unit in which AI reads code**: a 650-line file enters the context whole every
  time, and the extra lines cost money and are noise.
- **"Cohesive in use" is not a reason not to split**: callers using a set of members as one group does not mean
  their implementations are related. Judge by **the implementation**: does this half of the code carry its own
  domain baggage (its own constants, formats, edge cases, external specifications)? If it does, split.
- **The only case for not splitting: the type has no seam at all.** The typical case is a **pure property bag**.
  Splitting `FormField` (about 500 lines, `[XmlAttribute]` throughout) would force you to invent categories that do
  not exist in the domain, and what the reader wants is exactly one complete list.
  **"I can't think of how to split it" does not count.** You must be able to say "the members of this type are
  essentially one list".
- **This is pure organizational refactoring with zero behavior change**: after splitting, build and test should be
  green.

> Examples: `DbAccess` (`.Async`), `GridControl` (`.Columns` / `.Cells`),
> `FormBusinessObject` (`.Audit` / `.Permission`), `CacheDefineAccess` (`.Settings` / `.Schemas`).
>
> **`ValueUtilities` once blocked a split with "high cohesion"; that way out was removed on 2026-08-12.**
> At 501 lines it was judged "don't split": every member is `Cxxx(object) → T`, very cohesive as seen from use.
> After it grew to 637 lines, `.Temporal` (224 lines) was split out and the main file went back to 413 lines.
> The date half carries its own domain baggage (the SQL minimum date, culture-dependent parsing of `DateOnly`,
> the invariant round-trip format) and shares nothing with number parsing.
> **This paragraph stays because "cohesive in use" is easily mistaken for a reason not to split.**
> (bee-library, 2026-08-12)

### Folders match namespaces

The folder structure must map to the namespace (IDE0130): `src/Polhem.Db/Schema/` → `namespace Polhem.Db.Schema`.

**The only exception**: a folder that gathers many subclasses of the same parent class may use the folder as a
logical grouping without a sub-namespace (for example the `*Settings` under `src/Polhem.Definition/Settings/` stay
in `Polhem.Definition.Settings`).

> This cannot be enforced by `.editorconfig` (IDE0130 is a global rule and cannot make an exception for one folder),
> so the prompt guards it.

## Document language

Maintainer documents (`.claude/`, `docs/repo-ops/`, the ADRs, `CONTRIBUTING`) are a single `xxx.md` written in
**English**, with no translation in the repository. User documents are multilingual (see the Language section of
`.claude/CLAUDE.md`).

A bilingual single document (such as `README.md` / `README.zh-TW.md`) **must have both files changed together**:
the default file name is the English version, the Chinese version carries the `.zh-TW` suffix, and both have a
language switch link at the top. Under `docs/<lang>/`, the rules for that folder in `public-docs.md` apply; the
suffix rule applies only to single documents such as a README.

## Language features

Enable **Nullable Reference Types**, **Implicit Usings** (new projects), **Deterministic Builds**,
and `TreatWarningsAsErrors=true`.

**Warnings must be fixed, not kept.** For nullable warnings (CS8600–CS8670), choose whatever best expresses the
intent in context (`!`, a null check, or refactoring to non-nullable). When a test deliberately passes `null` to
check a boundary, use `null!` to show it is intended.
**Do not suppress broadly with `#pragma warning disable`**; suppressing a single line requires an explanatory
comment.

## Comments

By default **do not write comments**. Clearly named identifiers already say WHAT; a comment is only worth having
when the WHY is not obvious. **If a reader would not be confused after the comment is removed, it should not be
written.**

- **Write WHY, not WHAT**: explain intent, constraints and trade-offs; do not repeat what the code already says.
- **Structured prefixes**: `WARNING:` / `IMPORTANT:` (changing this breaks functionality or security), `NOTE:`
  (a non-obvious design consideration), `HACK:` (technical debt, with the reason), `TODO:` (preferably with an
  issue link).
- **Move long background out to `docs/adr/`**; the source keeps only the conclusion and cites the ADR number.
- **Cross-file constraints** are written on both sides; where possible, replace the comment with a compile-time
  mechanism (a source generator, a test assertion).
- **Never comment out old code** (S125, see `sonarcloud.md`). Use `git log` to keep history.

```csharp
// ✅ WHY: explains a non-obvious constraint
// PostgreSQL's default `statement_timeout` is 30 seconds, after which the server aborts the statement.
// Matching it here makes the client see a `TimeoutException` first instead of an `NpgsqlException`.
client.Timeout = TimeSpan.FromSeconds(30);

// ❌ Says nothing
// Set the timeout to 30 seconds.
client.Timeout = TimeSpan.FromSeconds(30);
```

### Comment language and avoiding S125 false positives

**In-body comments are always English** (the same as XML docs).

English comments are easily flagged by SonarCloud **S125 (commented-out code)**: its heuristic parser hits on
"English identifiers + a trailing `;`". To avoid it: write **complete English sentences** (ending with `.`, avoiding
an "identifier identifier `;`" shape), **wrap identifiers in backticks**, and break multi-line comments into natural
prose paragraphs. If it still hits → mark it *False Positive* in the SonarCloud UI; **do not delete the comment just
to pass the scan**.

```csharp
// ✅ Natural prose + identifiers in backticks
// MySQL 8.0+ in strict mode does not accept `CURRENT_TIMESTAMP` as the default
// value for a DATE column. The parenthesised expression form is required.

// ⚠️ Easily misjudged: trailing semicolon + consecutive English identifiers
// CURRENT_TIMESTAMP is rejected as DATE default in strict mode;
```

### XML documentation comments (public API)

Always **English** (they ship with the NuGet package and appear in the consumer's IntelliSense). Use the standard
`<summary>` / `<param>` / `<returns>`. **Put API-level warnings and preconditions in `<remarks>`**, so callers see
them in IntelliSense (for example: "This method must be called within an active transaction.").

#### Refer to this solution's types with `<see cref>`, not `<c>`

Under `GenerateDocumentationFile` + `TreatWarningsAsErrors`, **a `<see cref>` pointing to a type that does not exist
fails the build with `CS1574`**; `<c>Foo</c>` in prose has no protection at all. So when prose mentions a type or
member **inside this solution**, always use `<see cref>` and let the compiler guard it.

Keep `<c>` for what the compiler cannot guard: types from external packages, SQL fragments and setting values,
deliberate mentions of removed types, **file names** (`Foo.xml` / `Bar.razor`), and **upward cross-assembly
references** (a lower assembly mentioning a type from a higher one; the dependency points the wrong way, so cref
cannot resolve it anyway). These leftovers are scanned by `./check-xmldoc-refs.sh` at the repository root.
**That script also checks the reverse**: any type declared in the same project that is still written with `<c>` is
reported.

**For a type in a different namespace, use the fully qualified name; do not add a `using` just for the cref.**
A cref does not count as a "use", so adding one triggers `IDE0005`.
`<see cref="Polhem.Api.Client.Providers.LocalApiProvider"/>` is guarded by the compiler just the same.

> Example: the doc of `CacheInfo.Initialize` said "Called by `<c>CacheBootstrapper</c>`". That type disappeared
> when the static facade was removed, and **no mechanism noticed**. It survived until the repository-wide inventory
> on 2026-08-15. Written as `<see cref>`, it would have failed to compile that same day.

#### Do not write inventory counts of code artifacts

XML docs **do not state counts of methods, types or implementations** ("all four methods", "the nine framework
repositories", "reads the four customizable types"). **If you list, list names** (`Select, Count, and Delete`):
a reader can see when a name is wrong, but not when a number is wrong.

The reason is the same as in `single-source.md`: inventory counts always drift, and **no mechanism will notice**.
The compiler does not read prose, and an analyzer cannot do it either (the claim hides in natural language, and
there is no mechanical link between the number and the things it counts). The only effective measure is not to
create this source at all.

> In the 2026-08-15 inventory, **8** of the 10 grade-A factual errors were inventory counts that had drifted.

#### A claim in absolute terms must name the mechanism that enforces it

When prose (XML docs, `README`, comments, test `DisplayName`s) makes a **guarantee** such as "always / never /
otherwise the build fails", it must say right there **what enforces it**: a test name, an analyzer ID, a gate script.
If you cannot name one, describe the current state instead ("the current callers all …"); do not use the tone of a
guarantee.

This has the same root as inventory counts (the claim hides in natural language, and no mechanism notices when it
stops holding), but **the consequence is worse**: a wrong number only means outdated information, but **a wrong
guarantee makes readers stop checking**, and the readers include agents.

> In the bee-library health check on 2026-09-04, "claims a guarantee that does not actually exist" appeared **at
> least eight times** (five times in the previous round; **the number is rising**). The most memorable: a comment in
> `FormBusinessObject.Save` said "once the master passes the whole record persists with it", yet when the master
> is not in the payload, **no master has ever passed**. The place where that guarantee sat was exactly the security
> hole that allowed cross-record writes at the time. `AuditLogWriterService`'s "records are never silently lost" is
> the same: it only holds on the queue-saturation path.

**This is a standing code review check, not a question for health checks only.** When you see absolute language,
ask: where is the thing that enforces it?

## Serialization

JSON uses **System.Text.Json**; high-performance scenarios use **MessagePack**; **do not use `Newtonsoft.Json`**.

## Indentation and formatting

UTF-8 (without BOM), indentation of **4 spaces** (**2 spaces** for XML / csproj / props),
line endings **LF** (enforced by `.gitattributes`).
