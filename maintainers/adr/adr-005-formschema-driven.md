# ADR-005: FormSchema definition-driven architecture

## Status

Accepted

## Context

Enterprise application systems (ERP, sales and inventory, and so on) usually contain a large number of forms, and
each form involves several aspects: UI layout, database structure, validation rules and more. The common ways to
develop them:

1. **Code-First**: write the code first (entity / ViewModel), then derive the database and the UI
2. **Database-First**: design the database first, then generate the code
3. **Definition-Driven**: use a definition (schema) as the hub that drives every aspect at once

## Decision

Use `FormSchema` as the Single Source of Truth, driving the UI (FormLayout), the database (TableSchema) and the
business logic at the same time.

## Rationale

- **Less duplicated definition**: in traditional development, the same "employee name" field has to be defined once
  each in the entity, the ViewModel, the DB migration and the UI form. With FormSchema it is defined once, and the
  rest is derived automatically.
- **NoCode / LowCode support**: FormSchema is stored as XML, so non-programmers can also change form definitions
  through tools, without recompiling.
- **Guaranteed consistency**: the fields shown in the UI, the database columns and the validation rules all come from
  the same definition, so the definitions of different layers cannot disagree.
- **Fast development**: adding a form needs no hand-written CRUD code; the FormSchema-driven Repository generates
  the SQL automatically.
- **Progressive complexity**: simple forms use NoCode (pure definition), moderately complex ones use LowCode
  (definition + a little code), and highly customized ones use AnyCode (fully custom BO + Repository).

## Trade-offs

- **Learning curve**: developers need to understand how FormSchema, FormLayout and TableSchema relate and the rules
  by which they are derived.
- **Limited flexibility**: highly dynamic UIs or features that are not forms (such as dashboards and reports) are not
  a good fit for being driven by FormSchema.
- **Harder debugging**: a problem may come from a definition file rather than the code, so both the XML definition
  and the program logic have to be checked.
- **Read-only at runtime**: FormSchema cannot be changed once loaded at startup; adding fields dynamically requires
  reloading the definition.

## Consequences

- `Polhem.Definition/Forms/FormSchema.cs`: the definition hub, containing all fields, tables and relations
- `Polhem.Definition/Database/TableSchema.cs`: the projection onto the database dimension, derived from FormSchema
- `Polhem.Definition/Layouts/FormLayout.cs`: the projection onto the UI dimension
- The `IFormCommandBuilder` implementation of each provider under `Polhem.Db/Providers/` (such as
  `SqlServer/SqlFormCommandBuilder.cs`): generates SQL automatically from FormSchema
- `Polhem.Db/Dml/SelectCommandBuilder.cs`: assembles SELECT / FROM / WHERE / ORDER BY
- The architecture is described in detail in `docs/en/architecture-overview.md`
- The concrete pattern of the data access layer (FormMap) is described in `docs/formmap.zh-TW.md`
  — **that document was removed together with the name, as described under "Implementation evolution" below**.

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing with
the current code:

### 2026-08-13: the name "FormMap" has been dropped

**FormMap**, mentioned above, was the name given at the time to the approach of "dynamically generating SQL at
runtime from the definition, one `FormSchema` at a time". The name never corresponded to any type, interface or
namespace in `src/` (what actually does this is the `FormCommandBuilder` family in `Polhem.Db`), and it was never
taken up by the code or by external articles.

On 2026-08-13 it was decided to **drop its status as a pattern name**: the mechanism and the content of the document
are unchanged; they just no longer claim to be a named pattern. The entry has been removed from the `terminology`
table, and the document now lives at [`en/formschema-data-access.md`](../../docs/en/formschema-data-access.md), titled
"FormSchema-Driven Database Access".

The text above keeps its original wording, to preserve the context of the decision at the time.

### 2026-09-27: what checks the consistency

"Guaranteed consistency" under "Rationale" names no mechanism. What checks it now are the definition analyzers in
`src/Polhem.Analyzers/`, which report at build time, among others: POLHEM2001 (a FormSchema table is not registered
under its category in DbCategorySettings), POLHEM2002 (a FormSchema table has no TableSchema), POLHEM2005 (a FormSchema
has no FormLayout) and POLHEM2006 (a persisted FormSchema field is missing from the TableSchema). They read the
definition files a project supplies as `AdditionalFiles` (by default the package's `buildTransitive` targets supply
`Define\**\*.xml`), so a definition file changed outside a build is not checked by them.
