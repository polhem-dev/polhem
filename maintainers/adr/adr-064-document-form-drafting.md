# ADR-064: A form can be drafted from paper documents through a conversation with an AI model

## Status

**Accepted (2026-11-14)**

Builds on [ADR-056](adr-056-form-workbooks.md): the draft ends in the same import as a form workbook.

## Context

ADR-056 lets an analyst describe a form in an Excel workbook and import it. The source people most often have in hand
is not a workbook but the company's existing paper forms: a PDF, a Word document, a photo taken with a phone. Turning
one into a workbook is manual work, and most of it is reading: which boxes are fields, which repeat as lines, which
offer a fixed set of choices, which pick an employee, a department or a customer that already has a form.

An AI model reads such a document well. What it does not know is the target: the ProgIds the definition folder
already has, the framework's naming conventions, the field types and lengths the database will accept, and what the
person wants to call things. So the feature is a conversation: the model drafts the form from the documents, matches
the fields that pick a record to existing forms, and asks the person for what is missing, while the person sees the
form take shape.

Three constraints shaped it. The model must be replaceable: vendors and their prices change faster than a framework
release. Documents are company data and leave the machine only when the person agrees. And nothing the model says is
written to disk without passing the checks a workbook import passes.

## Decision

### 1. The model edits a draft through tools; code turns the draft into definitions

The model never writes XML. It changes a `FormDraft` (tables, fields, choices, relations, menu placement) through
tools the session declares: `set_form_info`, `upsert_table`, `upsert_field`, `set_relation`, their removals, the
catalog lookups `search_progids` and `get_progid_fields`, `get_draft`, `validate_draft` and `mark_ready`.
`FormDraftConverter` turns the draft into a `FormSchema`, the zh-TW texts and the menu placement, deterministically.
Types, enum values and naming are decided by code, so a field the model describes oddly becomes a warning or an error
in the draft, not a malformed definition.

A draft that is not finished still converts, with the open items as warnings and a placeholder ProgId. That is what
lets the person see the form while it is being drafted.

The model proposes; the code and the person decide. `mark_ready` succeeds only when the draft converts without error,
the host's validation finds no error, the ProgId is set, the master table has fields, and every relation the model
was unsure of (below `FormDraftingOptions.RelationConfirmationThreshold`) has been confirmed. Any later change to the
draft clears the ready state.

### 2. The feature knows `IChatClient` only; each vendor is a package of its own

`Polhem.Definition.Drafting` depends on `Microsoft.Extensions.AI.Abstractions`, Microsoft's vendor-neutral chat
abstraction, and on no vendor SDK. `DraftingDependencyGateTests` fails when one reaches its closure.

A provider package supplies the `IChatClient` for one vendor. The first is `Polhem.AI.Claude`, over the official
`Anthropic` SDK; it references no Polhem project (`ClaudeDependencyGateTests`). The application joins the two: in
DefineEditor that is one class, `DraftingChatClients`, and a user setting names the provider. A second vendor is a
new package and a new case there; the feature does not change.

A shared `Polhem.AI` core is not created. With one feature and one provider there is nothing to share yet; it is
created when a second AI feature has code in common with this one.

### 3. The session runs the tool loop itself

`FormDraftSession` hands the model the tool declarations only (`AsDeclarationOnly`), so a
`FunctionInvokingChatClient` an application wraps around the client cannot execute them, and the session runs the loop.
That is where the guarantees of decision 1 live: each tool input is checked before it changes the draft, an invalid
input goes back to the model as an error result instead of ending the conversation, `DraftChanged` is raised after
each change, and `mark_ready` applies its checks. A turn that fails or is cancelled is removed whole: the draft, the
ready state and the history return to what they were before it. The history of completed turns only grows, and the
model's own content, its reasoning included, is sent back as it came, which every provider accepts and some require.

### 4. The package is in `src/`, and the host supplies what lives in `tools/`

ADR-056 put the workbook code under `tools/` because no application should carry an Excel dependency. This feature is
published under `src/` instead: drafting from documents is useful to any definition tool, not only DefineEditor, and
the package carries no dependency an application would mind beyond the chat abstraction and the OpenXml reader for
Word documents. It is a design-time package: the foundation assemblies and the front-end layer cannot reference it,
which their `POLHEM9001` allowlists already enforce.

The conventions (`FormSchemaConventions`), the validation (`FormSchemaValidator`) and the importer are under `tools/`,
and `src/` cannot reference `tools/`. So the host supplies them through `IFormDraftingHost`: the catalog of existing
forms, the fields of one form, and `Validate`. DefineEditor's host applies the conventions, runs the import's dry run
(`FormImporter.Plan`) and parses every expression with the framework's evaluator, and reports what fails.

### 5. A finished draft is imported like a workbook

`FormDraftResult` maps onto the workbook importer's model (`FormWorkbookContent`), and from there DefineEditor runs
the same code as an Excel import: the choice of modes, the list of files and actions to confirm, the write, and the
offer to upgrade the form's tables. The live preview generates its FormLayout the way the import will
(`FormLayoutGenerator.Generate`, or `Merge` over the existing layout when the ProgId exists), so the preview is the
layout the import writes; `FormDraftingTests` compares the two.

### 6. Documents leave the machine only after the person agrees

Before anything is sent, DefineEditor lists the files, says they go to the Anthropic API and may hold personal data,
and names the credential and so the organization that pays. Nothing is sent until the person confirms. The documents
travel once, in the first message; the rest of the conversation refers to them.

Size limits are checked before reading, and a document over the limit is refused with its name, not cut short.
A Word document is sent as Markdown (paragraphs and tables, merged cells kept aligned) rather than as a file, so
any model that reads text can take it.

### 7. Credentials come from the vendor's own tooling, never from Polhem

Neither package nor DefineEditor stores a credential. `Polhem.AI.Claude` leaves it to the Anthropic SDK's resolution
chain: environment variables first (an API key, a token, a named profile, workload identity federation), then the
profile `ant auth login` saved, which holds short-lived tokens the SDK refreshes, scoped to a Claude Console
organization and workspace. Signing in is the documented way; an API key is the fallback for machines where the CLI cannot run.

The claude.ai subscription sign-in is not used. That usage is for Anthropic's own products. A Max, Ultra or Team
subscriber can bind the plan's monthly API credits to a Console organization instead, and sign in to that.

Because an environment key silently wins over a signed-in profile, `ClaudeCredentials.Describe()` reports which
source the SDK will use, with warnings, and DefineEditor shows it in the consent dialog and in the conversation window.
It never returns the secret. The C# SDK treats an empty `ANTHROPIC_API_KEY` as unset, unlike the CLI and the other
SDKs, and `Describe()` warns about that too. An expired sign-in is reported as `ClaudeFailureKind.NotAuthenticated`,
and the editor asks the person to run `ant auth login` again rather than showing the raw error.

### 8. What is specific to Claude stays in the provider package

`ClaudeChatClient` wraps the SDK's `IChatClient` adapter and adds what the adapter does not carry or carries wrongly:

- **Strict tools.** Tool inputs are validated by the server against their schema. Strict mode rejects a nullable enum
  written as a type union and limits the union-typed parameters across all tools, so `ToolSchemaRewriter` drops the
  null branch of optional properties and writes a required nullable enum as `anyOf`. The feature's schemas stay
  vendor-neutral.
- **Fallbacks.** A request the model's safety classifiers decline is answered by a fallback model. The adapter drops the
  block that marks the switch, and the API then rejects the next request; the client keeps the block and removes the
  declined attempt's thinking and tool calls, as the API asks.
- **Effort and thinking.** Adaptive thinking, and an effort that is always sent, because the default differs between
  models.
- **Caching.** Automatic caching plus a breakpoint at the end of the first user message, which holds the documents.
  The history only grows, so a later request within the cache lifetime reads the documents from the cache.
- **Failures.** A refusal, a response that reaches the output limit and a rejected credential become a
  `ClaudeChatException` with a `ClaudeFailureKind`, so the host can tell the person what to do.

Eager input streaming of tool calls is deliberately off: it turns off the server-side validation that strict tools
provide, and the session runs the tools only after the whole response has arrived anyway.

### 9. Measured before release

On 2026-11-14 the feature was run on eight synthetic forms against the Northwind definition folder: printed and
handwritten, PDF, Word and photographed (skewed, shadowed), with detail tables and relations to Employee, Department,
Customer and other forms. The thresholds agreed beforehand were field recall 90%, precision 85%, relations 90%,
types and lengths 90%, at most four replies from the person, and US$1 a form on average.

The first run met every threshold: recall, precision and relations 100% after correcting two annotations, types and
lengths 98.8%, 1.5 replies, US$0.25 and about 80 seconds a form. Its one real defect was expressions: two drafts
passed `mark_ready` with value expressions the runtime cannot evaluate (a sum over a detail table, a date subtraction
taken as a number), and every save of such a form would fail. That led to the expression check in DefineEditor's host
(decision 4) and to a parameter description that states what an expression can read; with both, no draft had an
invalid expression. Effort `medium` scored the same and cost about 14% less than the same prompt at `high`, in one
run; `high` was kept, because real documents are dirtier than synthetic ones.

## Consequences

- A paper form becomes a runnable form in one conversation, previewed as it is drafted, and lands through the same
  checks, confirmation and database upgrade as a workbook.
- Swapping or adding a model vendor is a new provider package and a line in the host. Nothing else references a vendor.
- Using the feature costs API credit and sends company documents to the vendor. The consent dialog makes both
  explicit every time.
- Limits of the first version:
  - Arranging the layout (fields side by side, sections) is done afterwards in the FormLayout editor; the draft only
    orders fields.
  - A value expression reads fields of its own row, so a master field that sums a detail table cannot be expressed;
    the model leaves such fields out.
  - The workbook import does not parse expressions; only the drafting host does.
  - The live preview is the desktop one; the web previews are not wired to the drafting window.
  - The evaluation used synthetic documents, eight of them, one run per variant.
