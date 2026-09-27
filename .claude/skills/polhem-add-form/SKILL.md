---
name: polhem-add-form
description: The multi-file procedure and pitfalls for "adding a form" to a Polhem app that is already wired up. A working CRUD form is pure definition changes (FormSchema + FormLayout + TableSchema + a DbCategorySettings TableItem + a MenuSettings MenuEntry, plus a ProgramSettings ProgramItem when a custom BO or Repository is bound), with no UI / CRUD code. Covers the file checklist (the symptom when each one is missing), company scope for business tables, the rule that the TableSchema folder must equal the CategoryId, FormSchema conventions (lookup via RelationProgId+RelationFieldMappings+ref_* RelationField, master-detail via sys_master_rowid, DropDownEdit+ListItems, computed fields via ValueExpression + FormField.ReadOnly, sys_name optional), and when a custom BO is actually needed. Use it when the user wants to "add a form / master file / document", "add a new ProgId / screen", "build CRUD for a table", "how do I define a lookup / detail / dropdown / read-only field in a Polhem form" and similar; trigger it proactively even if the user does not literally say "add a form".
---

# Adding a form to a Polhem app

On a Polhem backend that is already wired up (see `polhem-app-scaffold`), adding a **working CRUD form** is
**pure definition changes**: no UI code and no CRUD code.

> **The FormLayout must exist as a file.** It is produced at design time (`FormLayoutGenerator.Generate`); at run time
> it is always read from the definition file. **A missing file is not generated automatically, and opening the form
> fails outright.** Do not hand-write it: use `polhem-scaffold-from-formschema` to produce the original and then edit it.
> (This changed on 2026-08-20; before that the framework generated one on the fly at run time, so old notes saying
> "no need to write it" are obsolete.)

> **Reference implementation**: `apps/Polhem.Northwind/Define/` (a plain master file, Product with two lookups, and the
> master-detail Order). Copying from it is the fastest way.

## When to use

- Adding a new form to an existing Polhem app (master file, master file with lookups, master-detail document)
- Wanting to know how FormSchema expresses lookups / details / dropdowns / read-only fields

## When not to use

- The backend host is not wired up yet → do **`polhem-app-scaffold`** first
- The form needs business logic the framework cannot express as definitions (document numbers, state machines,
  cross-row totals) → add the form with this skill, and put the **business code** through **`polhem-add-bo-method`**
  or override `GetNewData` / `DoBeforeSave` directly (see "When a custom BO is needed")
- Deriving layout/language/tableschema sidecars from an existing FormSchema → **`polhem-scaffold-from-formschema`**

## The changes (symptom when one is missing)

| File | Purpose | Symptom when missing |
|------|------|-----------|
| `Define/FormSchema/<ProgId>.FormSchema.xml` | Form fields + list columns + lookups | The form does not open |
| `Define/FormLayout/<ProgId>.FormLayout.xml` | Screen layout (sections + field placement + detail grid) | **Opening the form fails** at run time; a host that wires the definition analyzers also gets `POLHEM2005` (`MissingFormLayout`) at build time |
| `Define/TableSchema/<categoryId>/<table>.TableSchema.xml` | DB table structure + indexes | The seeder cannot create the table / CRUD fails; analyzers report `POLHEM2002` (`MissingTableSchema`) |
| `Define/DbCategorySettings.xml`: a `<TableItem>` in the matching category | Registers the table → only then does the seeder create it and the router know table→db | The table is not created; analyzers report `POLHEM2001` (`TableNotRegisteredInCategory`) |
| `Define/MenuSettings.xml`: a `<MenuEntry Id="..." ProgId="<ProgId>" />` in a `<MenuFolder>` | Puts the form on the client's navigation menu | The form works but does not appear in the menu |
| `Define/ProgramSettings.xml`: a `<ProgramItem ProgId="<ProgId>" />` | The server-side type registry: binds a custom `BusinessObject` / `Repository` to the progId | **Required only when you bind a BO or Repository.** Without an entry the progId resolves to the framework's `FormBusinessObject` and `DataFormRepository`. Northwind still lists every form: the entry supplies the progId's canonical spelling and puts the form in the startup check that warns about forms without a `PermissionModelId` |

`ProgramSettings` is not the menu: it is a flat `<Items>` list, server-side only (remote `GetDefine` refuses it), and
the old nested `<Categories><ProgramCategory>` layout is rejected on load by `ProgramSettingsFormat.EnsureCurrentFormat`.
The menu layout (folders, order, captions, `Visible`) is `MenuSettings`; see `docs/en/definition-files-overview.md`
§ 4 and § 4b.

> **Business tables all use `company` scope**: FormSchema `CategoryId="company"`, TableSchema under
> `TableSchema/company/`, registered under the company category in DbCategorySettings. CategoryId is a DB scope selector
> (common/company/log), not a free-form label; putting business data in common is wrong (see `polhem-app-scaffold`
> Part 1 and `.claude/rules/database.md`). **The TableSchema folder name must equal the CategoryId.**

After adding, **restart the server (to create the table) + restart the front end**, and you get full
list / new / edit / delete + the `uk_` uniqueness check.

## FormSchema conventions

### Keys and system fields (every table)
`sys_no` (AutoIncrement, Visible=false) / `sys_rowid` (Guid, Visible=false) / `sys_id` (String business code) /
`sys_name` (name).
**`sys_name` is optional**: only "source tables referenced by a lookup" need it (as the lookup display fallback);
document types (such as Order) have no natural name and can leave it out.

### TableSchema index conventions
`pk_{0}` (sys_no, PrimaryKey) / `rx_{0}` (sys_rowid, Unique) / `uk_{0}` (sys_id, Unique) / one `fk_{0}_<col>` per
relation column.

### Lookup (cross-table relation, zero code)
The relation column (Guid) carries `RelationProgId` + `RelationFieldMappings`, writing the target's fields back into
this table's `ref_*` display columns; the `ref_*` columns are marked `Type="RelationField"`. The framework automatically
renders a ButtonEdit popup, writes back the display values, and recomputes them with a server-side JOIN on reload.

```xml
<FormField FieldName="customer_rowid" Caption="Customer" DbType="Guid" RelationProgId="Customer">
  <RelationFieldMappings>
    <FieldMapping SourceField="sys_id" DestinationField="ref_customer_id" />
    <FieldMapping SourceField="sys_name" DestinationField="ref_customer_name" />
  </RelationFieldMappings>
</FormField>
<FormField FieldName="ref_customer_id" Caption="Customer Code" DbType="String" Type="RelationField" />
<FormField FieldName="ref_customer_name" Caption="Customer Name" DbType="String" Type="RelationField" />
```
- The source table's FormSchema needs `LookupFields="sys_id,sys_name"` (composite display "code - name").
- Business tables can point to framework tables (such as Order.employee → `st_employee`), and vice versa.

### Master-detail (documents)
The master table has `FormTable.TableName == ProgId` (a framework invariant). The detail is a second `FormTable` whose
fields include `sys_master_rowid` (Guid, Visible=false) pointing to the master. A lookup in the detail (one target chosen
per row) is written the same way as above; the framework renders the popup in the InCell grid. The whole record is saved
and reloaded at once.

### Fixed-option dropdown
`ControlType="DropDownEdit"` + `<ListItems><ListItem Value=".." Text=".."/></ListItems>`; the default value uses
`DefaultValue="..."`.

### Computed / read-only fields
`FormField.ReadOnly="true"`: mark computed fields or server-derived fields as read-only. A per-row computation is
declared with `ValueExpression` (Northwind's `Order.FormSchema.xml`:
`ValueExpression="quantity * unit_price * (1 - discount)"` on `amount`); a value only the BO can compute is still
marked read-only here. Read-only fields render as read-only both as master fields and as detail InCell cells. When
the FormLayout is generated this is carried over to `LayoutField.ReadOnly`, so you do not need to mark it again in the
layout.

**The system timestamp fields `sys_insert_time` / `sys_update_time` are always marked `ReadOnly="true"`.** The framework
stamps them on save, and `FormBusinessObject.NormalizeDateTimes` overwrites whatever value the screen sends; leaving
the mark off only lets the user edit a value that can never be saved.
`ReadOnly` is only copied over at the moment the FormLayout is **generated**; it is not merged back at run time. In a
FormLayout that has already been generated, add `ReadOnly="true"` to the matching `LayoutField` by hand.

### List columns
`ListFields="sys_id,sys_name,ref_xxx_name,..."` controls the columns of the list view (showing `ref_*` is friendlier than
`*_rowid`).

## When a custom BO is needed

The default `FormBusinessObject` handles CRUD (pure definitions), and more is declarative than it used to be:
per-row computed values use `FormField.ValueExpression`, and required-field / condition checks use `<FormRule>` in the
FormSchema's `<Rules>` (both in Northwind's `Order.FormSchema.xml`). Write business code **only** for what those cannot
express: document number generation, state machines / valid transitions, cross-row aggregates such as a master total.
How: `ProgramItem.BusinessObject="Ns.OrderBO, Asm"` in `ProgramSettings` → override `GetNewData` / `DoBeforeSave`
(call the base first so the declarative rules still run), and extract pure rules into a DB-free helper (readable and
testable). See the reference implementation `OrderBO` (with `OrderRules` / `OrderDataSet`) / `polhem-add-bo-method`.

## Seed data (optional)

To preload data: `Polhem.Northwind.Server/SeedData/<Table>.json`, with relation columns filled with the target `sys_id`
(the seeder resolves it to `sys_rowid`), plus a `SeedTable` entry in `NorthwindSchemaSeeder`'s seed list, placed after
every table it references. Table creation is data-driven; the seed list is not. A new table that only needs CRUD does not need seed data (the user creates
records in the UI).

## Completion check

- [ ] FormSchema, FormLayout, TableSchema, DbCategorySettings `TableItem` and MenuSettings `MenuEntry` all changed;
      ProgramSettings `ProgramItem` added if a BO or Repository is bound
- [ ] Business table has `CategoryId="company"`, TableSchema is in the `company/` folder
- [ ] Lookup source table has `LookupFields`; `ref_*` columns are marked `Type="RelationField"`
- [ ] Restart server + front end: the form appears in the menu, CRUD works, the lookup popup works
