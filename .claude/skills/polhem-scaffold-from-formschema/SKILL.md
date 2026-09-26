---
name: polhem-scaffold-from-formschema
description: "Derive / generate the three kinds of \"sidecar\" definition files for a polhem FormSchema XML: FormLayout, TableSchema and bilingual LanguageResource. Covers the throw-away xUnit fact idiom (call the framework's public generators directly and serialize their output as is), the FormSchemaLocalizer sub-key convention (Schema.DisplayName / Table.X.DisplayName / Field.X.Caption), Chinese-to-English field translation guidance (sys_/ref_/audit_ system prefixes + common ERP terms), and target conflict detection and handling. Use when the user asks to \"generate the layout / language / tableschema from X.FormSchema\", \"add i18n for X\", \"scaffold the sidecar definitions for a form\", \"convert a FormSchema to a layout / translate the language / derive the tableschema\", or similar."
---

# polhem FormSchema → sidecar definition scaffold

polhem uses a **FormSchema-driven** design: one FormSchema drives the UI (FormLayout),
the database (TableSchema) and the multilingual interface (LanguageResource) at the same time. This skill fixes the
procedure for producing the three sidecar kinds, so conventions such as sub-key naming / CategoryId / namespace do not
become pitfalls.

> Reference samples (read them alongside the code):
> - `tests/Define/FormSchema/Employee.FormSchema.xml` (input)
> - `tests/Define/FormLayout/Employee.FormLayout.xml`
> - `tests/Define/TableSchema/company/st_employee.TableSchema.xml`
> - `tests/Define/Language/{zh-TW,en-US}/Employee.Language.xml`

> `st_employee` is not a typo. The `st_` prefix means **owned by the framework**; it does not mean the table lives in
> the common database. Like `st_department`, it is in company scope (see `rules/database.md`).

## The three outputs and their framework entry points

**Not every new form needs all three.** First confirm which kinds actually need to be written to disk:

| Output | When it must be written | Framework entry point |
|------|-------------|---------------|
| **FormLayout** | **Always.** The layout is produced and saved at design time, and the runtime always reads it; **if the file is missing, opening the form fails** (there is no longer any runtime auto-generation). | `FormLayoutGenerator.Generate(schema, layoutId)` (`Polhem.Definition.Layouts`); `layoutId` defaults to `ProgId` |
| **TableSchema** | **Always.** The seeder uses it to create tables, and the folder name must equal the CategoryId | `TableSchemaGenerator.Generate(formTable)` or `formTable.GenerateDbTable()`; produce one file for **each** FormTable in the schema |
| **LanguageResource** | When a multilingual interface is needed | No generator: build it by hand + the `FormSchemaLocalizer` sub-key constants. Bilingual: zh-TW copies the Chinese captions from the schema, en-US is derived from the translation glossary |

> **Division of labor with `polhem-add-form`**: adding a form to an existing app goes through `polhem-add-form`
> (5 pure definition changes, **including the FormLayout**). This skill is the technique for producing that FormLayout
> (and the TableSchema / i18n). Generate the raw output, then hand-tune the layout; do not hand-write it.
>
> Once the files are on disk, the layout can also be generated / edited with `tools/DefineEditor`.

**By default produce only the kinds that are actually needed**; if the user explicitly asks for all three (or only one
kind), do that.

## Procedure: throw-away xUnit fact

No CLI, no new console project. Reuse the proven idiom:

1. Write a `[Fact]` in `tests/Polhem.Definition.UnitTests/Scaffolding/_Scaffold{ProgId}FixtureFiles.cs`
2. In the test, deserialize the FormSchema → call the framework generators + build the LanguageResource by hand →
   `XmlCodec.SerializeToFile`
3. Run `dotnet test --filter "FullyQualifiedName~Scaffold{ProgId}"`
4. **Delete the test file immediately after the run** (it must not be committed; otherwise every run overwrites the
   fixtures)

Why: the framework's serialization format = the real round-trip format, so there is no drift; it validates that the
schema structure is legal (a generator exception means the schema is wrong); zero new csproj / publish cost.

### Complete copy-paste template

Replace every `{ProgId}` with the actual ProgId (e.g. `Employee`, `Department`), and fill in
`{Schema/Table/Field translations}`:

```csharp
using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Scaffolding
{
    /// <summary>
    /// Throw-away one-shot generator. Materializes FormLayout / TableSchema / Language
    /// fixture files for {ProgId} under tests/Define/. Delete this file after the
    /// fixtures are committed — every re-run overwrites the same target paths.
    /// </summary>
    public class Scaffold{ProgId}FixtureFiles
    {
        [Fact]
        [DisplayName("OneShot: generate FormLayout / TableSchema / Language fixtures from {ProgId}.FormSchema (run manually)")]
        public void Generate_{ProgId}SidecarFiles()
        {
            // bin/<config>/net10.0 → repo root (5 levels of ..)
            string baseDir = AppContext.BaseDirectory;
            string repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
            string definePath = Path.Combine(repoRoot, "tests", "Define");
            Assert.True(Directory.Exists(definePath), $"tests/Define not found: {definePath}");

            // 1. Deserialize the FormSchema (the only input)
            string schemaPath = Path.Combine(definePath, "FormSchema", "{ProgId}.FormSchema.xml");
            var schema = XmlCodec.DeserializeFromFile<FormSchema>(schemaPath)!;

            // 2. FormLayout (design-time generator; the runtime only reads the saved file)
            var layout = FormLayoutGenerator.Generate(schema, "{ProgId}");
            XmlCodec.SerializeToFile(layout,
                Path.Combine(definePath, "FormLayout", "{ProgId}.FormLayout.xml"));

            // 3. TableSchema (one per FormTable)
            foreach (var table in schema.Tables!)
            {
                var tableSchema = table.GenerateDbTable();
                XmlCodec.SerializeToFile(tableSchema,
                    Path.Combine(definePath, "TableSchema", schema.CategoryId,
                        $"{table.DbTableName}.TableSchema.xml"));
            }

            // 4. Language zh-TW (copy the Chinese captions from the schema)
            var zh = BuildResource("zh-TW",
                schemaDisplayName: "{Chinese schema name}",
                tableDisplayNames: new (string, string)[]
                {
                    ("{TableName}", "{Chinese table name}"),
                    // one entry per table
                },
                fieldCaptions: new (string, string)[]
                {
                    ("sys_no", "流水號"),
                    // … one per FormField.Caption in the FormSchema
                });
            XmlCodec.SerializeToFile(zh,
                Path.Combine(definePath, "Language", "zh-TW", "{ProgId}.Language.xml"));

            // 5. Language en-US (derived from the translation glossary)
            var en = BuildResource("en-US",
                schemaDisplayName: "{English schema name}",
                tableDisplayNames: new (string, string)[]
                {
                    ("{TableName}", "{English table name}"),
                },
                fieldCaptions: new (string, string)[]
                {
                    ("sys_no", "Sequence No."),
                    // … one per zh-TW entry
                });
            XmlCodec.SerializeToFile(en,
                Path.Combine(definePath, "Language", "en-US", "{ProgId}.Language.xml"));

            Assert.True(File.Exists(Path.Combine(definePath, "FormLayout", "{ProgId}.FormLayout.xml")));
        }

        private static LanguageResource BuildResource(
            string lang,
            string schemaDisplayName,
            (string TableName, string DisplayName)[] tableDisplayNames,
            (string FieldName, string Caption)[] fieldCaptions)
        {
            var resource = new LanguageResource
            {
                Namespace = "{ProgId}",
                Lang = lang,
            };
            resource.Items.Add(FormSchemaLocalizer.SchemaDisplayNameKey, schemaDisplayName);
            foreach (var (tableName, displayName) in tableDisplayNames)
            {
                resource.Items.Add(
                    string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        FormSchemaLocalizer.TableDisplayNameKeyFormat, tableName),
                    displayName);
            }
            foreach (var (fieldName, caption) in fieldCaptions)
            {
                resource.Items.Add(
                    string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        FormSchemaLocalizer.FieldCaptionKeyFormat, fieldName),
                    caption);
            }
            return resource;
        }
    }
}
```

(`流水號` is the zh-TW caption for `sys_no`.)

### How to run

```bash
dotnet test tests/Polhem.Definition.UnitTests/Polhem.Definition.UnitTests.csproj \
    --configuration Debug \
    --filter "FullyQualifiedName~Scaffold{ProgId}" \
    --nologo --verbosity minimal
```

Once it passes, **`rm` the test file immediately**, then run `git status` to confirm the three outputs and the deleted
test file are in the right state.

## Path conventions

| Situation | DefinePath |
|------|-----------|
| Demonstration inside polhem (`tests/Define/`) | `{repoRoot}/tests/Define` (this is what the template uses) |
| A real ERP project | That project's `Define/`, `app/Define/` or similar, per that repository's own convention |

The template's `repoRoot = baseDir + "../" * 5` fits `tests/<Project>.UnitTests/bin/<config>/net10.0/`. In other
repositories whose test projects sit at a different depth, adjust the number of `..`.

## i18n key convention (aligned with FormSchemaLocalizer)

| Scope | Key pattern | Constant |
|------|---------|------|
| Display name of the whole schema | `Schema.DisplayName` | `FormSchemaLocalizer.SchemaDisplayNameKey` |
| Display name of each table | `Table.{TableName}.DisplayName` | `FormSchemaLocalizer.TableDisplayNameKeyFormat` |
| Caption of each field | `Field.{FieldName}.Caption` | `FormSchemaLocalizer.FieldCaptionKeyFormat` |

**namespace = ProgId** (always: `FormSchemaLocalizer.Localize` uses `schema.ProgId` directly as the lookup namespace).

**Do not add extra sub-keys "for completeness".** `FormSchemaLocalizer` only looks up the three kinds above; extra keys
are never used by the framework and only add maintenance. Translating enums / lists is a separate topic (see
`LanguageEnum`) and outside the default scaffold scope.

## Chinese → English glossary

**Rule: do not translate word for word; follow common ERP terminology.** Business captions are concise (no "The ..."),
and system prefixes have fixed translations.

### System prefixes (fixed translations)

| Chinese / FieldName | English | Notes |
|-----------------|---------|------|
| `sys_no` 流水號 | `Sequence No.` | DB auto-increment PK; **not** `System Number` |
| `sys_rowid` 唯一識別 | `Row Id` | Globally unique Guid |
| `sys_id` 編號 | `{Entity} No.` | Business key; per entity, e.g. `Employee No.` / `Department No.` |
| `sys_name` 名稱 | `{Entity} Name` or `Name` | Same as above |
| `sys_insert_time` 寫入時間 | `Insert Time` or `Created At` |  |
| `sys_update_time` 更新時間 | `Update Time` or `Updated At` |  |
| `sys_insert_user` 寫入者 | `Created By` |  |
| `sys_update_user` 更新者 | `Updated By` |  |
| `ref_xxx_id` / `ref_xxx_name` | `{Xxx} No.` / `{Xxx} Name` | Display fields brought in by a RelationField; no `Ref.` prefix needed, since the business user never sees the relation |

### Common ERP terms (frequent in business forms)

| Chinese | English |
|------|---------|
| 員工 | Employee |
| 部門 | Department |
| 主管 / 直屬主管 | Supervisor |
| 部門主管 | Department Manager |
| 客戶 | Customer |
| 供應商 | Supplier / Vendor |
| 產品 / 品項 | Product / Item |
| 訂單 | Order |
| 採購單 | Purchase Order |
| 銷貨單 | Sales Order |
| 庫存 | Inventory / Stock |
| 出貨 | Shipment |
| 倉庫 | Warehouse |
| 公司 | Company |
| 角色 | Role |
| 權限 | Permission |
| 使用者 / 用戶 | User |
| 帳號 | Account |
| 密碼 | Password |
| 電子郵件 | Email |
| 備註 | Note / Remarks |
| 描述 | Description |
| 狀態 | Status |
| 類別 | Category |
| 金額 / 單價 / 總價 | Amount / Unit Price / Total |
| 數量 | Quantity |
| 日期 / 時間 | Date / Time |
| 起始 / 結束 | Start / End |
| 起日 / 迄日 | Start Date / End Date |
| 專案 | Project |
| 任務 | Task |

### Caption style

- No articles (not `The Employee Name`)
- Use `No.` (with the period) for numbers; not `Number` / `Id` (unless it really is a GUID/UUID rowid)
- No sentence case (not `Employee no.`); ERP UI convention is Title Case
- Prefer abbreviations for long words: `Department No.`, not `Department Number`

## Conflict handling (skip existing files by default)

Before running, **dry-run: list every target path** and check whether it exists:

```bash
# Example: check before scaffolding Customer
ls tests/Define/FormLayout/Customer.FormLayout.xml \
   tests/Define/TableSchema/company/ft_customer.TableSchema.xml \
   tests/Define/Language/zh-TW/Customer.Language.xml \
   tests/Define/Language/en-US/Customer.Language.xml 2>/dev/null
```

**Default rule: an existing target is always skipped, never overwritten.**

Why: the three sidecar kinds this skill derives are "reasonable defaults" produced by the framework generators + the
translation glossary. **They are not an authoritative source.** The user may have hand-tuned them since:

- FormLayout: adjusted LayoutColumn widths, changed a ControlType, split fields into Sections
- TableSchema: added indexes, changed a String field's Length, added extra DbFields (e.g. audit fields)
- Language: reworded, added LanguageEnum translations

Skipping by default = re-running the scaffold on an existing entity is safe (it only fills in new fields / new
entities and does not touch hand-tuned files).

### Overriding the default

Overwrite only when the user explicitly asks to "regenerate / overwrite":

| User intent | Handling |
|-----------|---------|
| "Regenerate the layout" / "overwrite X.Language.xml" | Overwrite that file (still **only the file named**; other existing files stay skipped) |
| "Re-scaffold everything" | Overwrite all (confirm once more, because the blast radius is large) |
| "Show me what the framework's raw output looks like" | Produce `{file}.new.xml` for diffing; the user merges by hand or `rm`s the `.new`; the original is untouched |

When the user **has not said so** in the conversation, report "N targets skipped (already exist), M targets added"
after the run, without asking. Skipping is the expected behavior; there is no need to interrupt every time.

## Final checklist

After the run, confirm each item:

- [ ] The FormLayout file is produced at `{DefinePath}/FormLayout/{ProgId}.FormLayout.xml`
- [ ] A TableSchema is produced for every FormTable in the schema (in per-CategoryId folders)
- [ ] One zh-TW and one en-US Language file each
- [ ] The Language XML has `Namespace="{ProgId}"`, `Lang="{lang}"`
- [ ] The Language Items contain `Schema.DisplayName` + `Table.X.DisplayName` for each table + `Field.X.Caption` for
      each field
- [ ] zh-TW and en-US have **the same Items.Count** and keys match one to one
- [ ] The FormSchema `CategoryId` is not empty (otherwise `TableSchemaGenerator` throws `InvalidOperationException`)
- [ ] The throw-away test file is deleted (`Scaffold{ProgId}FixtureFiles.cs` must not appear in `git status`)
- [ ] The three output kinds show as added / modified in `git status` (per the conflict policy)

## Out of scope

- PermissionModels entries (form ↔ permission model mapping; a separate plan)
- Generating DataSet / DataTable C# code
- Repository / BO code (belongs to the `polhem-add-bo-method` skill)
- BlazorPage / FormPage UI code
- LanguageEnum / ListItems translation (a separate topic; handle it when `LangEnumName` in the schema triggers it)
- Cross-repository installation (this skill is a polhem project skill and is by default only available when this
  repository is open)
