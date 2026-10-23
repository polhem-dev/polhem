using Polhem.Core.Data;
using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Tests.Shared;

namespace Polhem.Definition.Spreadsheet.UnitTests
{
    /// <summary>
    /// The files <see cref="FormSync"/> plans after a FormSchema changes, in an isolated temporary definition folder.
    /// </summary>
    public sealed class FormSyncTests : IDisposable
    {
        private readonly string _define = Path.Combine(Path.GetTempPath(), "polhem-sync-" + Guid.NewGuid().ToString("N"));

        public FormSyncTests()
        {
            Directory.CreateDirectory(Path.Combine(_define, "FormSchema"));
            File.WriteAllText(Path.Combine(_define, "FormSchema", "Customer.FormSchema.xml"),
                XmlCodec.Serialize(new FormSchema("Customer", "Customer")));
            FormImporter.Apply(FormImporter.Plan(FormSchemaConventionsTests.MinimalQuote(), new FormImportOptions { DefinePath = _define }));
        }

        public void Dispose()
        {
            if (Directory.Exists(_define)) Directory.Delete(_define, recursive: true);
        }

        private string PathOf(params string[] parts) => Path.Combine([_define, .. parts]);

        private string SchemaPath => PathOf("FormSchema", "Quote.FormSchema.xml");

        private string QuoteTablePath => PathOf("TableSchema", "company", "ft_quote.TableSchema.xml");

        private string LayoutPath => PathOf("FormLayout", "Quote.FormLayout.xml");

        private void EditSchema(Action<FormSchema> edit)
        {
            var schema = XmlCodec.DeserializeFromFile<FormSchema>(SchemaPath)!;
            edit(schema);
            File.WriteAllText(SchemaPath, XmlCodec.Serialize(schema));
        }

        private static List<string> FieldNames(TableSchema table) => table.Fields!.Select(f => f.FieldName).ToList();

        [Fact]
        [DisplayName("Syncing a form that has not changed since it was imported writes nothing")]
        public void Plan_UnchangedForm_WritesNothing()
        {
            var plan = FormSync.Plan(_define, "Quote");

            Assert.False(plan.HasErrors);
            Assert.DoesNotContain(plan.Files, f => f.Writes);
            Assert.Empty(plan.Issues);
        }

        [Fact]
        [DisplayName("A field added to the form goes into the TableSchema and the FormLayout next to its neighbour on the form")]
        public void Plan_NewField_IsPlacedNextToItsNeighbour()
        {
            EditSchema(s =>
            {
                var master = s.Tables!["Quote"];
                master.Fields!.Insert(master.Fields.IndexOf(master.Fields["quote_date"]) + 1, new FormField("valid_until", "Valid Until", FieldDbType.Date));
            });

            var plan = FormSync.Plan(_define, "Quote");
            FormImporter.Apply(plan);

            Assert.False(plan.HasErrors);
            Assert.Equal(PlannedFileAction.Update, plan.Files.Single(f => f.Path == QuoteTablePath).Action);
            var columns = FieldNames(XmlCodec.DeserializeFromFile<TableSchema>(QuoteTablePath)!);
            Assert.Equal(columns.IndexOf("quote_date") + 1, columns.IndexOf("valid_until"));

            var layoutFile = plan.Files.Single(f => f.Path == LayoutPath);
            Assert.Equal(PlannedFileAction.Update, layoutFile.Action);
            Assert.Contains("Quote.valid_until", layoutFile.Description, StringComparison.Ordinal);
            var fields = XmlCodec.DeserializeFromFile<FormLayout>(LayoutPath)!.Sections![0].Fields!.Select(f => f.FieldName).ToList();
            Assert.Equal(fields.IndexOf("quote_date") + 1, fields.IndexOf("valid_until"));
        }

        [Fact]
        [DisplayName("A visible field added to the master table is appended to the form's ListFields")]
        public void Plan_NewField_IsAddedToListFields()
        {
            EditSchema(s =>
            {
                s.ListFields = "sys_id,quote_date";
                s.Tables!["Quote"].Fields!.Add(new FormField("grade", "Grade", FieldDbType.String) { MaxLength = 10 });
                s.Tables!["Quote"].Fields!.Add(new FormField("internal_note", "Internal Note", FieldDbType.String) { MaxLength = 40, Visible = false });
            });

            var plan = FormSync.Plan(_define, "Quote");
            FormImporter.Apply(plan);

            var schemaFile = plan.Files.Single(f => f.Path == SchemaPath);
            Assert.Equal(PlannedFileAction.Update, schemaFile.Action);
            Assert.Equal("sys_id,quote_date,grade", XmlCodec.DeserializeFromFile<FormSchema>(SchemaPath)!.ListFields);
        }

        [Fact]
        [DisplayName("A field the ListFields already names is not added again, and a synced form is not touched twice")]
        public void Plan_ListedField_IsNotAddedAgain()
        {
            EditSchema(s =>
            {
                s.Tables!["Quote"].Fields!.Add(new FormField("grade", "Grade", FieldDbType.String) { MaxLength = 10 });
                s.ListFields = "grade,sys_id";
            });
            var listFields = XmlCodec.DeserializeFromFile<FormSchema>(SchemaPath)!.ListFields;

            FormImporter.Apply(FormSync.Plan(_define, "Quote"));

            Assert.Equal(listFields, XmlCodec.DeserializeFromFile<FormSchema>(SchemaPath)!.ListFields);
            Assert.DoesNotContain(FormSync.Plan(_define, "Quote").Files, f => f.Writes);
        }

        [Fact]
        [DisplayName("An empty ListFields, which lists every field, stays empty when a field is added")]
        public void Plan_EmptyListFields_StaysEmpty()
        {
            EditSchema(s =>
            {
                s.ListFields = string.Empty;
                s.Tables!["Quote"].Fields!.Add(new FormField("grade", "Grade", FieldDbType.String) { MaxLength = 10 });
            });

            FormImporter.Apply(FormSync.Plan(_define, "Quote"));

            Assert.Equal(string.Empty, XmlCodec.DeserializeFromFile<FormSchema>(SchemaPath)!.ListFields);
        }

        private string LanguagePath => PathOf("Language", "zh-TW", "Quote.Language.xml");

        [Fact]
        [DisplayName("A field added to a translated form gets a zh-TW caption key holding its own caption")]
        public void Plan_NewField_AddsZhTwCaptionKey()
        {
            EditSchema(s => s.Tables!["Quote"].Fields!.Add(new FormField("grade", "Grade", FieldDbType.String) { MaxLength = 10 }));

            var plan = FormSync.Plan(_define, "Quote");
            FormImporter.Apply(plan);

            Assert.False(plan.HasErrors);
            Assert.Equal(PlannedFileAction.Update, plan.Files.Single(f => f.Path == LanguagePath).Action);
            var items = XmlCodec.DeserializeFromFile<LanguageResource>(LanguagePath)!.Items;
            Assert.Equal("Grade", items["Field.grade.Caption"].Value);
            Assert.Equal("客戶", items["Field.customer_rowid.Caption"].Value);
        }

        [Fact]
        [DisplayName("A zh-TW caption already given for a new field is kept, and a form without zh-TW texts gets no language file")]
        public void Plan_NewField_KeepsTranslationAndCreatesNoLanguageFile()
        {
            var language = XmlCodec.DeserializeFromFile<LanguageResource>(LanguagePath)!;
            language.Items.Add(new LanguageItem { Key = "Field.grade.Caption", Value = "客戶等級" });
            File.WriteAllText(LanguagePath, XmlCodec.Serialize(language));
            EditSchema(s => s.Tables!["Quote"].Fields!.Add(new FormField("grade", "Grade", FieldDbType.String) { MaxLength = 10 }));

            FormImporter.Apply(FormSync.Plan(_define, "Quote"));

            Assert.Equal("客戶等級", XmlCodec.DeserializeFromFile<LanguageResource>(LanguagePath)!.Items["Field.grade.Caption"].Value);

            File.Delete(LanguagePath);
            EditSchema(s => s.Tables!["Quote"].Fields!.Add(new FormField("rank", "Rank", FieldDbType.String) { MaxLength = 10 }));
            var plan = FormSync.Plan(_define, "Quote");

            Assert.DoesNotContain(plan.Files, f => f.Path == LanguagePath && f.Writes);
        }

        [Fact]
        [DisplayName("Syncing with the layout kept leaves the FormLayout alone and still updates the TableSchema")]
        public void Plan_KeepLayout_SkipsLayout()
        {
            EditSchema(s => s.Tables!["Quote"].Fields!.Add("remark", "Remark", FieldDbType.Text));

            var plan = FormSync.Plan(_define, "Quote", FormLayoutImportMode.Keep);

            Assert.Equal(PlannedFileAction.Skip, plan.Files.Single(f => f.Path == LayoutPath).Action);
            Assert.Equal(PlannedFileAction.Update, plan.Files.Single(f => f.Path == QuoteTablePath).Action);
        }

        [Fact]
        [DisplayName("A detail table added to the form gets a TableSchema, a registration and a grid")]
        public void Plan_NewDetailTable_CreatesAndRegisters()
        {
            EditSchema(s =>
            {
                var payments = s.Tables!.Add("QuotePayment", "Quote Payments");
                payments.Fields!.Add("amount", "Amount", FieldDbType.Decimal);
            });

            var plan = FormSync.Plan(_define, "Quote");
            FormImporter.Apply(plan);

            Assert.False(plan.HasErrors);
            Assert.True(File.Exists(PathOf("TableSchema", "company", "ft_quote_payment.TableSchema.xml")));
            Assert.Contains("ft_quote_payment", File.ReadAllText(PathOf("DbCategorySettings.xml")), StringComparison.Ordinal);
            Assert.Contains(XmlCodec.DeserializeFromFile<FormLayout>(LayoutPath)!.Details!, g => g.TableName == "QuotePayment");
        }

        [Fact]
        [DisplayName("A column whose type changed on the form is kept in the TableSchema and reported")]
        public void Plan_ChangedType_WarnsAndKeeps()
        {
            EditSchema(s => s.Tables!["Quote"].Fields!["quote_date"].DbType = FieldDbType.DateTime);

            var plan = FormSync.Plan(_define, "Quote");

            Assert.False(plan.HasErrors);
            var warning = Assert.Single(plan.Issues, i => i.Location == "Quote.quote_date");
            Assert.Equal(IssueSeverity.Warning, warning.Severity);
            Assert.DoesNotContain(plan.Files, f => f.Path == QuoteTablePath && f.Writes);
        }

        [Fact]
        [DisplayName("A field removed from the form stays in the TableSchema and the FormLayout, and the layout one is reported")]
        public void Plan_RemovedField_WarnsAndKeeps()
        {
            EditSchema(s => s.Tables!["Quote"].Fields!.Remove(s.Tables["Quote"].Fields!["quote_date"]));

            var plan = FormSync.Plan(_define, "Quote");

            Assert.False(plan.HasErrors);
            Assert.Contains(plan.Issues, i => i.Severity == IssueSeverity.Warning && i.Location == "Quote.quote_date");
            Assert.DoesNotContain(plan.Files, f => f.Writes);
        }

        [Fact]
        [DisplayName("The tables of a form are its master and detail tables, not the tables its relations point to")]
        public void TableNames_ListsMasterAndDetails()
        {
            var schema = XmlCodec.DeserializeFromFile<FormSchema>(SchemaPath)!;

            Assert.Equal(["ft_quote", "ft_quote_line"], FormSync.TableNames(schema));
        }

        public static TheoryData<string> NorthwindForms() => FormImporterTests.NorthwindForms();

        [Theory]
        [MemberData(nameof(NorthwindForms))]
        [DisplayName("Syncing a Northwind form in a copy of its folder writes nothing and warns about nothing")]
        public void Plan_Northwind_WritesNothing(string progId)
        {
            var northwind = Path.Combine(RepoRoot.Find(), "apps", "Polhem.Northwind", "Define");
            foreach (var file in Directory.EnumerateFiles(northwind, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(_define, Path.GetRelativePath(northwind, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, overwrite: true);
            }

            var plan = FormSync.Plan(_define, progId);

            Assert.Empty(plan.Issues);
            Assert.DoesNotContain(plan.Files, f => f.Writes);
        }
    }
}
