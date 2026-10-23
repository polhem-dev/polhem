using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.DefineEditor.Services;
using Polhem.Definition.Forms;
using Polhem.Definition.Spreadsheet;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The window-free steps of syncing a FormSchema and upgrading its tables, in an isolated temporary definition folder.
    /// </summary>
    public sealed class FormSchemaSyncTests : IDisposable
    {
        private readonly string _define = Path.Combine(Path.GetTempPath(), "polhem-editor-sync-" + Guid.NewGuid().ToString("N"));

        public FormSchemaSyncTests()
        {
            Directory.CreateDirectory(Path.Combine(_define, "FormSchema"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_define)) Directory.Delete(_define, recursive: true);
        }

        private static FormSchema Quote()
        {
            var schema = new FormSchema("Quote", "Quote") { CategoryId = "company" };
            var master = schema.Tables!.Add("Quote", "Quote");
            master.DbTableName = "ft_quote";
            master.Fields!.Add("sys_rowid", "Row ID", FieldDbType.Guid).Visible = false;
            master.Fields!.Add(new FormField("sys_id", "Quote No", FieldDbType.String) { MaxLength = 20 });
            master.Fields!.Add("quote_date", "Quote Date", FieldDbType.Date);
            var lines = schema.Tables.Add("QuoteLine", "Quote Lines");
            lines.DbTableName = "ft_quote_line";
            lines.Fields!.Add("qty", "Quantity", FieldDbType.Integer);
            return schema;
        }

        private void Import(FormSchema schema)
            => FormImporter.Apply(FormImporter.Plan(new FormWorkbookContent(schema), new FormImportOptions { DefinePath = _define }));

        [Fact]
        [DisplayName("The added names of a TableSchema are its new columns, in the order they appear")]
        public void AddedNames_TableSchema_ListsNewColumns()
        {
            const string before = """<TableSchema TableName="ft_quote"><Fields><DbField FieldName="sys_id" /></Fields></TableSchema>""";
            const string after = """<TableSchema TableName="ft_quote"><Fields><DbField FieldName="remark" /><DbField FieldName="sys_id" /><DbField FieldName="valid_until" /></Fields></TableSchema>""";

            Assert.Equal(["remark", "valid_until"], FormSchemaSync.AddedNames(before, after));
        }

        [Fact]
        [DisplayName("A detail column is told apart from a master field of the same name by its grid's table")]
        public void AddedNames_LayoutGrid_QualifiesColumns()
        {
            const string before = """<FormLayout><Sections><LayoutSection><Fields><LayoutField FieldName="amount" /></Fields></LayoutSection></Sections></FormLayout>""";
            const string after = """<FormLayout><Sections><LayoutSection><Fields><LayoutField FieldName="amount" /></Fields></LayoutSection></Sections><Details><LayoutGrid TableName="QuoteLine"><Columns><LayoutColumn FieldName="amount" /></Columns></LayoutGrid></Details></FormLayout>""";

            Assert.Equal(["QuoteLine", "QuoteLine.amount"], FormSchemaSync.AddedNames(before, after));
        }

        [Fact]
        [DisplayName("The preview of a sync lists each written file with the fields it gains")]
        public void DescribeWrites_ListsFilesAndAddedFields()
        {
            var schema = Quote();
            Import(schema);
            schema.Tables!["Quote"].Fields!.Add("remark", "Remark", FieldDbType.Text);
            File.WriteAllText(Path.Combine(_define, "FormSchema", "Quote.FormSchema.xml"), XmlCodec.Serialize(schema));

            var text = FormSchemaSync.DescribeWrites(FormSync.Plan(_define, "Quote"), _define);

            Assert.Contains(Path.Combine("TableSchema", "company", "ft_quote.TableSchema.xml"), text, StringComparison.Ordinal);
            Assert.Contains("+ remark", text, StringComparison.Ordinal);
            Assert.Contains(Path.Combine("FormLayout", "Quote.FormLayout.xml"), text, StringComparison.Ordinal);
            Assert.DoesNotContain("ft_quote_line", text, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A table the DbCategorySettings does not register is reported before an upgrade")]
        public void UnregisteredTables_NewDetailTable_IsReported()
        {
            var schema = Quote();
            Import(schema);
            schema.Tables!.Add("QuotePayment", "Payments").DbTableName = "ft_quote_payment";

            Assert.Equal(["ft_quote_payment"], FormSchemaSync.UnregisteredTables(_define, schema));
        }

        [Fact]
        [DisplayName("Every table of an imported form is registered, so none is reported")]
        public void UnregisteredTables_ImportedForm_IsEmpty()
        {
            var schema = Quote();
            Import(schema);

            Assert.Empty(FormSchemaSync.UnregisteredTables(_define, schema));
        }
    }
}
