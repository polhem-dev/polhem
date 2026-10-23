# Modeling Forms in Excel

[繁體中文](../../zh-TW/guides/excel-form-modeling.md) · [← Docs Index](../README.md)

> Describe a form in an Excel workbook, import it, and get a form that runs: list, new, edit, delete, lookups,
> computed fields and validation rules, with no code

---

## Table of Contents

1. [Who This Is For](#1-who-this-is-for)
2. [The Round Trip](#2-the-round-trip)
3. [The Workbook](#3-the-workbook)
4. [What the Import Fills In](#4-what-the-import-fills-in)
5. [What the Import Writes](#5-what-the-import-writes)
6. [Importing Again](#6-importing-again)
7. [Changing a Form in DefineEditor](#7-changing-a-form-in-defineeditor)
8. [Limits](#8-limits)

---

## 1. Who This Is For

Early in a project, the people who know the business (analysts, key users) describe the forms, and developers turn
the description into definition files. A form workbook lets the first group write the description in a format the
framework reads directly. One workbook describes one form: its tables, its fields, its validation rules and where it
sits in the menu. Importing it produces every definition file the form needs, so the form can be opened and used at
once, and refined in DefineEditor afterwards.

The workbook is not a second source of truth that lives next to the definition files. Once a form is imported, the
FormSchema is the definition; export it to a new workbook whenever the analyst should edit it again.

## 2. The Round Trip

The commands are in `dotnet polhem` ([Polhem.Cli](../../../tools/Polhem.Cli/README.md)); DefineEditor offers the same
import and export from its menus, and its **Import Forms from Excel (Batch)** wizard imports several workbooks in a row,
upgrading each form's tables after it and stopping at the first one that fails
([DefineEditor](../../../tools/DefineEditor/README.md)).

```bash
# An empty workbook for a new form (--lang zh-TW for Traditional Chinese headers)
dotnet polhem forms template --out Quote.xlsx

# The workbook of an existing form, to change it
dotnet polhem forms export Quote --define ./Define --out Quote.xlsx

# See what an import would change, then import
dotnet polhem forms import Quote.xlsx --define ./Define --dry-run
dotnet polhem forms import Quote.xlsx --define ./Define
```

`--define` is the definition folder, the one that holds `FormSchema/`. The import writes definitions only; creating
the tables is the application's job. The Northwind demo, for example, builds every table registered in
`DbCategorySettings.xml` when its server starts (`NorthwindSchemaSeeder`), so there a restart is all it takes for the
form to appear in the menu and work. The `db upgrade` command builds them, and `--table` limits it to the form's tables
([Initializing a New Deployment](../database/database-settings-guide.md#7-initializing-a-new-deployment)); DefineEditor
offers to run it right after an import, through the database manager tool
([Database work](../../../tools/DefineEditor/README.md#database-work)).

## 3. The Workbook

| Sheet | What it holds |
|-------|---------------|
| `Instructions` or `說明` | How to fill in the workbook, and every column with its meaning |
| `Form` | The form itself: program id, display name, database category, list fields, menu placement |
| `Tables` | One row per table: the first row is the master table, the others are detail tables |
| `Fields` | One row per field, naming the table it belongs to |
| `Rules` | One row per validation rule, as described in [Expressions and Rules](../definitions/expression-rules.md) |

A workbook comes in English or Traditional Chinese: the headers, the explanations and the instructions sheet change,
and nothing else does. `forms template` and `forms export` write English unless `--lang zh-TW` is given; DefineEditor
writes in the language of its interface. The import reads a workbook of either language.

The instructions sheet of the workbook is the reference for the columns; this guide does not repeat them. Columns
that take a fixed set of values (data type, control type, number kind, yes/no) offer a drop-down. The columns most
forms never need are grouped and collapsed at the right of the Fields sheet.

Every display text has an English column and a zh-TW column, in both languages of the workbook. The English text goes into the definition, and the zh-TW
text into the language resource, as described in [Tenant Customization](../definitions/customization.md).

A cell holds a small list as `a=b; c=d`: a field's list items (`Draft=Draft; Sent=Sent`, or just `Draft; Sent` when
the value and the text are the same) and the field mappings of a relation or a lookup (`sys_id=ref_customer_id`).

The second row of each tabular sheet is hidden and holds the key of each column. The import finds columns by these
keys, not by their position, so you can reorder columns, reword the headers or insert columns of your own; a column
without a key is ignored. Do not delete the hidden row.

## 4. What the Import Fills In

The workbook only needs the business fields. The import adds what the framework's conventions decide, and only where
the workbook leaves it out:

- **The database category** defaults to `company`, where business data belongs.
- **The master table** is named after the program id. A workbook without a Tables sheet row gets one.
- **Database table names** default to `ft_` plus the table name in snake case: `QuoteLine` becomes `ft_quote_line`.
- **System fields**: every table gets `sys_no` and `sys_rowid`, and every detail table `sys_master_rowid`, hidden.
- **Relation fields**: a field with a related form (`customer_rowid` related to `Customer`) becomes a `Guid` and gets
  the two fields that show the related record, `ref_customer_id` and `ref_customer_name`, mapped from the related
  form's `sys_id` and `sys_name`. Their captions follow the relation field's: `Customer Code`, `客戶代號`.
- **Framework tables**: a form on a table the framework owns, such as `st_employee` or `st_department`, gets the
  framework's own TableSchema for it, with the form's columns added. Those tables have columns the framework reads that
  are not on the form (`st_employee.user_rowid`, which sign-in uses).

## 5. What the Import Writes

| File | When it already exists |
|------|------------------------|
| `FormSchema/<ProgId>.FormSchema.xml` | Replaced, unless it already describes the same form |
| `TableSchema/<category>/<table>.TableSchema.xml`, one per table | Depends on the TableSchema mode; see [Importing Again](#6-importing-again). An existing database is brought in line by the [schema upgrade](../database/database-schema-upgrade.md) |
| `FormLayout/<ProgId>.FormLayout.xml` | **Kept**; see [Importing Again](#6-importing-again) |
| `DbCategorySettings.xml` | The tables are added to the category when missing |
| `ProgramSettings.xml` | The program is added when missing |
| `MenuSettings.xml` | An entry is added to the folder named on the Form sheet when no entry opens the form yet; a missing folder is created |
| `Language/zh-TW/<ProgId>.Language.xml` and `Language/zh-TW/Menu.Language.xml` | The workbook's zh-TW texts are set; other keys stay |

The settings and language files are edited in place: the entries are added and the rest of each file, comments
included, stays as it was.

Before anything is written, the form is checked with the same validation DefineEditor runs. An error, located at the
workbook row it comes from (`Fields!12 (Quote.qty.UnitField)`), stops the import, and nothing is written. A warning,
such as a relation to a form the folder does not have yet, is shown and does not stop it.

A **String** field that becomes a table column needs a **Max length**: no database builds a text column without one, so
the import stops at the field rather than leaving the failure to the database upgrade. A column the existing
TableSchema already sizes needs none. Exporting a form whose lengths are kept only in its TableSchema writes them into
the workbook.

## 6. Importing Again

Change the workbook and import it again; the files that already describe what the workbook says stay untouched.

A TableSchema often holds more than the form can derive: column lengths, columns that are not on the form, indexes
added by hand. What the import does with an existing one is chosen with `--tables` (DefineEditor asks):

| Mode | Effect |
|------|--------|
| `merge` (default) | Adds the columns and indexes the form has and the file lacks. Nothing in the file is changed or removed, so a removed field or a changed data type is handled in DefineEditor. The one exception is a String column without a length, which takes the workbook's |
| `keep` | Leaves the file as it is |
| `overwrite` | Replaces the file with the TableSchema generated from the form |

The FormLayout is the exception. The import generates one only when the form has none, because a layout is usually
arranged by hand afterwards. To replace it with a freshly generated one, pass `--regenerate-layout`, or use
**Generate FormLayout** on the FormSchema in DefineEditor. When the form has a layout, DefineEditor asks whether to keep
it, add the fields it lacks, or regenerate it. A kept layout does not show a field added to the workbook later until it
is placed there.

## 7. Changing a Form in DefineEditor

A form can also be changed in DefineEditor without going back to the workbook. After adding fields or a detail table to
the FormSchema, right-click the schema node, or use the same commands in the **File** menu while the FormSchema is the
active tab:

| Command | Effect |
|---------|--------|
| **Sync TableSchema and FormLayout…** | Brings the form's TableSchemas and FormLayout up to date with the saved FormSchema, the way a `merge` import does, registers a new detail table in `DbCategorySettings.xml`, and appends each new visible field of the master table to the form's list fields (`ListFields`; an empty one already lists every field and stays empty). When the form has a zh-TW language resource, each new field that has no caption there gets one holding its own caption, ready to translate. It lists the files it changes and the fields and keys each gains before writing, and then offers to upgrade the form's tables |
| **Upgrade This Form's Tables…** / **Preview Upgrade of This Form's Tables** | Runs the database manager's `upgrade` for the form's tables only (`--table` for each) |

- A new field goes next to its neighbour on the form: in the TableSchema after the nearest earlier column, and in the
  FormLayout after the nearest earlier field, in that field's section or grid. The layout needs no rearranging
  afterwards. The layout half is `FormLayoutGenerator.Merge`, which an application can call as well.
- The sync only adds. A field removed from the form stays in the TableSchema and the FormLayout, and a column whose type
  or size changed keeps the TableSchema's; the preview lists both under the things to edit by hand.
- A field taken off the layout on purpose would come back, so when the layout lacks fields the sync asks whether to add
  them or keep the layout.
- The sync reads the saved FormSchema, so unsaved edits are saved first.
- A form's tables are its master table and its detail tables. The tables of the forms its relation fields point to are
  not included; they belong to those forms.

## 8. Limits

- One workbook describes one form.
- The workbook carries the definition, not the layout: sections, column counts and the order of fields on screen are
  arranged in DefineEditor.
- Business logic a definition cannot express (a custom business object or repository) is written in code and
  registered in `ProgramSettings.xml`, as for any form.
- The import does not touch a database. New tables are created by whatever the application uses to build its schema,
  and existing ones are changed through the [schema upgrade](../database/database-schema-upgrade.md).
