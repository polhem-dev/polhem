# Polhem.Definition.Drafting

[繁體中文](README.zh-TW.md)

Drafts a Polhem form from a paper document (a PDF, a Word document or photos) through a conversation with an AI model.
The model reads the documents, drafts the tables and fields, matches the fields that pick a record to forms the
definition folder already has, and asks for what is missing. The result is a `FormSchema` with its zh-TW texts and
menu placement, ready for an import such as the one in DefineEditor.

It is a design-time package, for a definition tool. It depends on `Microsoft.Extensions.AI.Abstractions` and never on
a model vendor's SDK: the application passes in an `IChatClient`, from a provider package such as
[Polhem.AI.Claude](../Polhem.AI.Claude/README.md) or any other implementation.

## Key Public APIs

| Type | Purpose |
|------|---------|
| `DocumentSource` | Reads PDFs, images and Word documents into the content of the first message. A Word document becomes Markdown text (paragraphs and tables); the others are sent as they are. `SupportedExtensions` lists the extensions; `MaxDocumentBytes` and `MaxImageBytes` are the limits |
| `FormDraftSession` | The conversation: `StartAsync` sends the documents, `SendAsync` sends the person's reply, `ApplyUserEdit` records an edit the person made directly. Raises `DraftChanged` and `AssistantMessage`; `IsReady` turns true when the draft passes every check |
| `FormDraft` | The draft the model edits: tables, fields, choices, relations and the menu placement. Each field keeps the text of the document it came from |
| `FormDraftConverter` | Turns a draft into a `FormDraftResult`, deterministically. An unfinished draft converts too, with its open items as issues and `PlaceholderProgId` until the ProgId is set |
| `FormDraftResult` | The `FormSchema`, the zh-TW texts keyed as `FormSchemaLocalizer` reads them, the menu placement, the source text of each field, and the issues |
| `IFormDraftingHost` | What the application supplies: the catalog of existing forms, the fields of one form, and the validation of a result |
| `FormDraftingOptions` | Chat options passed to every request, the tool rounds allowed in one turn, and the confidence below which the person must confirm a relation |

## How a session works

```csharp
var documents = DocumentSource.FromFiles(paths);   // checks every size before reading anything
var session = new FormDraftSession(chatClient, host);
session.DraftChanged += (_, _) => Preview(FormDraftConverter.Convert(session.Draft));
session.AssistantMessage += (_, e) => Show(e.Text);

await session.StartAsync(documents, cancellationToken);
await session.SendAsync("Use LeaveRequest as the ProgId.", cancellationToken);

if (session.IsReady)
    Import(FormDraftConverter.Convert(session.Draft));
```

- **The model never writes a definition.** It changes the draft through tools the session declares, and the converter
  decides types, enum values and names. The tools are declared only, so a `FunctionInvokingChatClient` around the
  client cannot run them; the session runs each call, checks its input, and returns an invalid one to the model as an
  error instead of ending the conversation.
- **Ready means checked.** The model's `mark_ready` succeeds only when the draft converts without error, the host's
  `Validate` reports no error, the ProgId is set, the master table has fields, and every relation below
  `RelationConfirmationThreshold` has been confirmed. Any later change clears `IsReady`.
- **A failed or cancelled turn leaves nothing behind.** The draft, `IsReady` and the history return to what they were
  before the turn. One turn runs at a time; calling `SendAsync` or `ApplyUserEdit` during one throws
  `InvalidOperationException`.
- **The conventions are the host's.** The draft gives a relation field only the related ProgId. System fields, `ft_`
  table names, the `company` category and the `ref_*` fields of a relation are added when the host applies the
  framework's form conventions, as the workbook import does.

## The host

`IFormDraftingHost` exists because the checks an import applies live in the tool, not in a package. A host returns
the forms of its definition folder (`GetCatalog`, `GetFields`) so the model can match relations, and validates a
result (`Validate`) the way its import would. DefineEditor's host applies the conventions, runs the import's dry run
and parses every value expression, so a draft is ready only when its import would succeed.

## Data and cost

Every call sends the documents and the conversation to whatever the `IChatClient` talks to, and a hosted model charges
for it. Ask the person before calling `StartAsync`, and say where the documents go. The package itself stores nothing
and sends nothing except through the client it is given.

## Background

[ADR-064](../../maintainers/adr/adr-064-document-form-drafting.md) records why the model edits a draft instead of
writing XML, why the feature knows only `IChatClient`, and how the result reaches the workbook import of
[ADR-056](../../maintainers/adr/adr-056-form-workbooks.md). Using it from DefineEditor is described in
[Drafting Forms from Documents](../../docs/en/guides/document-form-drafting.md).

## Dependencies

`Polhem.Definition` · Microsoft.Extensions.AI.Abstractions · DocumentFormat.OpenXml
