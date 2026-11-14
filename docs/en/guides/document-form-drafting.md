# Drafting Forms from Documents

[繁體中文](../../zh-TW/guides/document-form-drafting.md) · [← Docs Index](../README.md)

> Give DefineEditor a paper form (a PDF, a Word document or photos), answer a few questions from Claude while the
> form takes shape beside the conversation, and import it like a form workbook

---

## Table of Contents

1. [What It Does](#1-what-it-does)
2. [Before You Start: the Credential](#2-before-you-start-the-credential)
3. [Drafting a Form](#3-drafting-a-form)
4. [Creating the Definitions](#4-creating-the-definitions)
5. [After the Import](#5-after-the-import)
6. [Cost and Time](#6-cost-and-time)
7. [Limits](#7-limits)

---

## 1. What It Does

The forms a company already uses are usually on paper, or in a PDF or Word file. **File → Import Form from
Documents...** in DefineEditor sends such a form to Claude, an AI model, which drafts the Polhem form from it:

- the master table and any detail tables (lines that repeat),
- the fields, with an English caption, a zh-TW caption, a data type and a length,
- drop-down choices where the document offers a fixed set,
- relations: a field that picks an employee, a department, a customer or another document is matched to the form your
  definition folder already has for it,
- where the form goes in the menu.

What the document does not say (the ProgId, field names, lengths, a relation Claude is unsure of), Claude asks in the
conversation, suggesting an answer each time. Nothing is written until you choose **Create Definitions**, and that runs
the same import as [an Excel workbook](excel-form-modeling.md), with the same checks and the same confirmation.

Claude only proposes. DefineEditor turns the draft into definitions with the framework's own rules, and a draft can be
created only when the import would succeed.

## 2. Before You Start: the Credential

Drafting calls the Anthropic API, which is paid by API credit, not by a claude.ai subscription. DefineEditor keeps no
credential of its own: the Anthropic SDK finds the one on your computer.

1. **Give the API credit.** On a Max, Ultra or Team plan, bind the subscription's monthly API credits to a Claude Console
   organization: claude.ai → Settings → Billing → API credits. Otherwise, add credit to an organization in the Claude
   Console.
2. **Sign in.** Install the `ant` command-line tool and run:

   ```bash
   ant auth login
   ```

   Choose the organization from step 1. The sign-in is saved in your user profile and renews itself; when it expires,
   DefineEditor says so, and you run the same command again.
3. **Or use an API key.** Where the command-line tool cannot run, create a key in the Console and set
   `ANTHROPIC_API_KEY` before starting DefineEditor.

An environment variable wins over a sign-in, so a key left in your shell profile is used even after you sign in. The
conversation window shows the credential in use at the top, and the dialog before sending warns about a leftover or
empty key. Signing in to claude.ai, as Claude's own apps do, gives no API access.

## 3. Drafting a Form

1. Open the definition folder, then **File → Import Form from Documents...**.
2. Choose the documents of one form. Several files are sent together, for example the front and back of a printed
   form photographed separately. PDFs, Word documents and PNG or JPEG images are accepted; a file over the size limit
   is refused before anything is sent.
3. **Agree to send them.** The dialog lists the files, says that their content goes to the Anthropic API, and names
   the credential and so the organization that pays. Company documents may hold personal data: send them only if you
   may share them with Anthropic.

The conversation window opens, and Claude's first draft appears in it when Claude has read the documents.

| Part | What it shows |
|------|---------------|
| Left | The conversation. Type your answer and press Enter (Shift+Enter for a new line); **Stop** cancels the current reply and leaves the draft as it was before your message |
| **Form** tab | The form as it will look, redrawn after each change. The buttons above it switch to view mode and to phone width. Clicking a field opens it in the **Fields** tab |
| **Fields** tab | Every field of every table: name, captions, type, length, required, relation, and the text of the document it came from. Edit a cell to change the draft directly; Claude is told what you changed with your next message |
| **Questions** tab | What is still open: missing values, relations to confirm, validation errors. An error keeps the form from being created |

Answer Claude's questions in your own words: "use LeaveRequest", "the reason can be 200 characters", "put it under
Human Resources". When everything is settled, Claude marks the draft ready and **Create Definitions** is enabled.

A few things worth knowing:

- **Relations are matched to your forms.** Claude searches the folder's forms and their fields. When it is not sure (a
  "received by" box could be an employee or free text), it asks before relating the field.
- **The conventions are filled in for you**, as in an Excel import: system fields, `ft_` table names, the `company`
  database category, and the `ref_*_id` / `ref_*_name` fields that show the related record. The preview already shows
  them.
- **Computed fields are checked.** A field whose value is computed (an amount from quantity and price, the days of a
  leave from its dates) is parsed by the framework's evaluator before the draft can be ready, so an expression that
  would fail on every save is caught here. An expression reads the fields of its own row.

## 4. Creating the Definitions

**Create Definitions** hands the form to the workbook import, so everything in
[What the Import Writes](excel-form-modeling.md#5-what-the-import-writes) and
[Importing Again](excel-form-modeling.md#6-importing-again) applies: you choose what happens to an existing
TableSchema and FormLayout, review the list of files and actions, and confirm. The conversation window closes once
the files are written, and DefineEditor offers to upgrade the database for the form's tables
([Database work](../../../tools/DefineEditor/README.md#database-work)).

When the ProgId already exists, the **Form** tab shows the existing layout with the new fields added, which is what
the import writes when you choose to add the missing fields to the layout.

## 5. After the Import

- **Arrange the layout.** The draft decides the order of the fields, not their arrangement. Put fields side by side,
  group them into sections and adjust widths in the FormLayout editor, with its live preview.
- **A running server needs no restart.** The framework's definition caches compare each file's write time when they
  are read (at most once a second), so a running server reads the new FormSchema, FormLayout and settings on its next
  request. Measured on 2026-11-14 with the framework's definition access (`CacheDefineAccess`) over a copy of the
  Northwind definition folder: a new form, its program entry and its menu entry were all read about a second after
  they were written.
- **The tables must exist.** Accept the upgrade DefineEditor offers, or run `db upgrade` yourself
  ([Initializing a New Deployment](../database/database-settings-guide.md#7-initializing-a-new-deployment)).
- **Sign in again to see the menu entry.** A client fetches the menu when it signs in, so one that was already open does
  not show the new entry until it signs in again.

## 6. Cost and Time

Each message leads to one or more paid requests to the API, charged to the organization of the credential. The
documents are cached after the first request, so a reply sent while the cache lasts (a few minutes) costs far less than
the first request.

As a guide, measured on 2026-11-14 with eight synthetic forms (printed, handwritten and photographed, some with detail
tables and relations): about US$0.25 and 80 seconds per form, with one or two replies from the person. Real documents
vary with their length and with how much has to be asked. The Console shows what each organization has spent.

## 7. Limits

- One conversation drafts one form.
- Only Claude is supported today. The drafting itself is not tied to a vendor
  ([Polhem.Definition.Drafting](../../../src/Polhem.Definition.Drafting/README.md)); another model needs a provider
  package like [Polhem.AI.Claude](../../../src/Polhem.AI.Claude/README.md).
- A master field that totals a detail table cannot be expressed, because an expression reads its own row only; Claude
  leaves such fields out.
- The **Form** tab previews the desktop form. The web previews of the FormLayout editor are not available in the
  conversation window.
- The Excel import does not parse expressions; only drafting does.
- Business logic a definition cannot express (a custom business object or repository) is written in code, as for any
  form.
