# Polhem.Definition.Spreadsheet

[繁體中文](README.zh-TW.md)

Reads and writes the Excel workbook that describes one form, and turns a workbook into the definition files of a
runnable form. It is a development-time library shared by [Polhem.Cli](../Polhem.Cli/README.md) (`dotnet polhem
forms`) and [DefineEditor](../DefineEditor/README.md); it is not published as a package, so an application never
carries an Excel dependency.

How a workbook is filled in and what the import does is described for its users in
[Modeling Forms in Excel](../../docs/en/guides/excel-form-modeling.md). This file is about the code.

## Pieces

| Type | Role |
|------|------|
| `FormWorkbookCatalog` | The single description of the workbook: sheets, columns in order, drop-down lists. Everything else reads it |
| `FormWorkbookWriter` / `FormWorkbookReader` | Fill the embedded template with a form, and read a filled workbook back |
| `FormSchemaConventions` | Add what a workbook leaves to convention: system fields, the ref fields of relations, table names, the category |
| `FormSchemaValidator` | The checks DefineEditor shows next to a form; the importer refuses a form with errors |
| `FormImporter` | Plan every file an import writes (the plan is the dry-run report), then write it |
| `FormExporter` | Gather a form, its zh-TW texts and its menu placement from a definition folder |
| `FormSync` | Plan bringing the TableSchemas, FormLayout and table registrations of a saved FormSchema up to date with it: an import of the form as the folder holds it, adding only. New visible master fields are also appended to a non-empty `ListFields`, and a new field of a form that has a zh-TW language resource gets a caption key there holding its own caption |

## The template

MiniExcel fills a template but cannot create drop-downs, so the layout lives in templates under `Templates/`, one per
`FormWorkbookLanguage` (`FormSchema.en.xlsx`, `FormSchema.zh-TW.xlsx`). They are generated from `FormWorkbookCatalog`
with ClosedXML in the test project and committed; every header, explanation and instruction line in the catalog carries
both languages, and only those texts differ between the templates. Do not edit them in Excel. After changing the
catalog, regenerate them:

```bash
POLHEM_REGENERATE_FORM_TEMPLATE=1 dotnet test tests/Polhem.Definition.Spreadsheet.UnitTests \
    --filter FullyQualifiedName~FormWorkbookTemplateTests
```

`FormWorkbookTemplateTests` fails while a template and the catalog disagree or the two templates differ in anything but
those texts, `FormWorkbookLanguageTests` fails when a text is in one language only, and `FormWorkbookCoverageTests` fails
when a serialized property of `FormSchema`, `FormTable`, `FormField` or `FormRule` has no column.

## Staying trim- and AOT-safe

DefineEditor is published as a single file, and the library is built with `IsAotCompatible`. MiniExcel reads object
properties by reflection and carries no trim annotations, so its typed paths fail under trimming and NativeAOT. Two
rules keep clear of them:

- Pass template values as `Dictionary<string, object>` and lists of them, never as anonymous types or other objects.
- Read rows through the non-generic `Query` and cast them to `IDictionary<string, object>`. Do not use `Query<T>`, and
  do not iterate the result with a typed `foreach`, which converts through `dynamic`.

The analyzer catches a `dynamic` conversion in this project; it cannot see a call to MiniExcel's reflective paths.

MiniExcel is pinned to the 1.x line: 2.0 splits the package in three and changes the API, so moving to it is a
migration of its own.
