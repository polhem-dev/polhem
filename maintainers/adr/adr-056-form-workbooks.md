# ADR-056: Forms can be described in an Excel workbook and imported as definitions

## Status

**Accepted (2026-10-20)**

## Context

A FormSchema already drives the TableSchema, the FormLayout and the validation of a form, so a form that needs no
custom business logic is pure definition. Writing that definition still takes a developer: the XML, the naming
conventions (`ft_` tables, system fields, the `ref_*` fields of a relation) and the registrations in
`DbCategorySettings.xml`, `ProgramSettings.xml` and `MenuSettings.xml` that the Northwind README lists as seven manual
steps. Early in a project the people who know the forms are analysts, who work in spreadsheets. The goal is to let
them describe a form in a format the framework reads directly, and get a form that runs, as a quick first model.

## Decision

### 1. A development-time library under `tools/`, not a package

`tools/Polhem.Definition.Spreadsheet` holds the workbook code. `Polhem.Cli` (`dotnet polhem forms template | export |
import`) and DefineEditor reference it; it is not published. Modeling happens at design time, so no application should
carry an Excel dependency, and nothing under `src/` changes: the dependency rules of
[ADR-038](adr-038-definition-dependency-boundary.md) are not touched. `FormSchemaValidator` moves from DefineEditor into
the library, so the editor and the importer apply the same checks.

### 2. One workbook describes one form

The workbook has a Form sheet (key/value rows) and Tables, Fields and Rules sheets (one row per item). Columns are
found by a hidden key row, not by position, so a person can reorder, reword or add columns. Display texts have an
English column, which goes into the definition, and a zh-TW column, which goes into the language resource.

### 3. MiniExcel fills a template generated from a catalog

`FormWorkbookCatalog` is the single description of the sheets, columns and drop-down lists. The reader and the writer
move values through it, and the template is generated from it.

The library choice was measured on 2026-10-20 (`net10.0`):

| Library | Packages it adds | DLLs | Drop-downs |
|---------|------------------|------|------------|
| MiniExcel 1.46.0 | none | about 0.4 MB | no API (the request was closed as out of scope, issue #845, 2026-07-11) |
| DocumentFormat.OpenXml 3.5.1 | two, both Microsoft | about 7 MB | yes, verbose |
| ClosedXML 0.105.1 | seven, `SixLabors.Fonts` among them (its 2.x line is not Apache-licensed) | about 10 MB | yes |

MiniExcel keeps the data validation of a template when it fills it (measured: drop-downs, frozen panes, the hidden key
row and the hidden Lists sheet all survive), and the layout of a form workbook never changes; only the rows do. So the
layout is drawn once into `Templates/FormSchema.xlsx`, and MiniExcel fills it. ClosedXML draws that template from the
catalog in the test project only. The template is binary and cannot be reviewed in a diff, so
`FormWorkbookTemplateTests` compares its content with what the catalog lays out and fails until it is regenerated.

MiniExcel is pinned to 1.x: 2.0 splits the package and changes the API.

### 4. MiniExcel is used only through dictionaries

DefineEditor is published as a single file. MiniExcel reads object properties by reflection and has no trim
annotations. Measured on 2026-10-20 for osx-arm64, the flow of the importer (load the template from a resource, fill
it, read the rows back) runs under JIT, as a single-file build and under NativeAOT, as long as values go in as
dictionaries and rows come out through the non-generic `Query`. A typed `foreach` over the result converts through
`dynamic` and failed under NativeAOT. The library builds with `IsAotCompatible`, which turns such a conversion in its
own code into a build error; its README states the two rules for the part the analyzer cannot see.

### 5. The import applies the conventions, validates, then writes every file

The workbook lists business fields only. `FormSchemaConventions` adds what is missing and nothing else: the `company`
category, the master table named after the ProgId, `ft_` table names, `sys_no` / `sys_rowid` / `sys_master_rowid`,
and for a relation field its `Guid` type and the `ref_*` fields mapped from `sys_id` and `sys_name`. Applying it to a
form that already follows the conventions changes nothing, which is what lets an exported form be imported again.

`FormImporter.Plan` validates and lists every file with its action before anything is written; an error stops the
whole import. A FormSchema is left untouched when it already describes the same form, so an unchanged workbook does
not rewrite it and its comments survive.

### 5a. An existing TableSchema is merged by default

A TableSchema is not fully derivable from its form. Exporting the nine Northwind forms and importing them into an
empty folder (2026-10-20) rebuilt every FormSchema exactly, but none of the ten TableSchema files: the hand-written ones
carry column lengths the forms leave out, columns that are not on any form (`dept_rowid` and `user_rowid` of
`st_employee`, `parent_rowid` of `st_department`, which the organization features use) and indexes of their own.
Replacing an existing TableSchema from the form would drop all of that.

So the import takes a mode (`TableSchemaImportMode`, `--tables`): `Merge`, the default, adds the columns and indexes
the form has and the file lacks, at the XML level, and changes or removes nothing; `Keep` leaves the file; `Overwrite`
replaces it. With `Merge`, importing the exported Northwind workbooks back into a copy of the Northwind folder writes
no file at all, which `FormImporterTests.ExportThenImport_Northwind_WritesNothing` checks for each form.

When the TableSchema of a framework table (`st_employee`, `st_department`, `st_audit_rule`) is created or
overwritten, the framework's embedded TableSchema for it (`Defaults`) is the base and the form's columns are merged in.
Running Northwind on a folder rebuilt from its workbooks showed why: generated from the form alone, `st_employee` lacked
`user_rowid`, and sign-in failed with "no such column". With the framework's TableSchema as the base, the rebuilt folder
signs in, lists, reads back relations and saves (2026-10-20).

### 6. The layout is generated once

An existing FormLayout is kept unless the import is asked to regenerate it. A layout is the part people arrange by
hand after the first draft, and the workbook does not describe arrangement.

### 7. Shared settings files are edited, not re-serialized

`DbCategorySettings.xml`, `ProgramSettings.xml`, `MenuSettings.xml` and the language files are edited at the XML level:
entries are added, and the rest of the document, comments included, stays. Round-tripping them through their
definition types would drop the comments that explain the entries.

## Consequences

- An analyst can describe a form, and a developer can import it and run it the same day. The Northwind demo was used to
  check it end to end on 2026-10-20: a master-detail form with two relations, a computed field and two rules, imported
  from a workbook, listed, created, validated, reloaded and deleted in the browser head.
- `FormWorkbookCoverageTests` fails when a serialized property of `FormSchema`, `FormTable`, `FormField` or `FormRule`
  has no column, so a new property cannot be lost on the way through Excel unnoticed.
- The import writes definitions only. Creating tables stays the application's job, as before.
- Out of scope: several forms per workbook, layout in the workbook, and generating business objects.
