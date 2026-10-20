using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.Spreadsheet.UnitTests
{
    /// <summary>
    /// The files <see cref="FormImporter"/> plans and writes, in an isolated temporary definition folder.
    /// </summary>
    public sealed class FormImporterTests : IDisposable
    {
        private readonly string _define = Path.Combine(Path.GetTempPath(), "polhem-import-" + Guid.NewGuid().ToString("N"));

        public FormImporterTests()
        {
            Directory.CreateDirectory(Path.Combine(_define, "FormSchema"));
            File.WriteAllText(Path.Combine(_define, "FormSchema", "Customer.FormSchema.xml"),
                XmlCodec.Serialize(new FormSchema("Customer", "Customer")));
        }

        public void Dispose()
        {
            if (Directory.Exists(_define)) Directory.Delete(_define, recursive: true);
        }

        private FormImportOptions Options(bool regenerateLayout = false)
            => new() { DefinePath = _define, RegenerateLayout = regenerateLayout };

        private static FormWorkbookContent Quote()
        {
            var content = FormSchemaConventionsTests.MinimalQuote();
            content.ZhTw["Schema.DisplayName"] = "報價單";
            content.Menu.FolderId = "sales";
            content.Menu.FolderCaption = "Sales";
            content.Menu.FolderCaptionZhTw = "銷售";
            content.Menu.EntryCaption = "Quotes";
            return content;
        }

        private string PathOf(params string[] parts) => Path.Combine([_define, .. parts]);

        [Fact]
        [DisplayName("Importing into an empty folder creates every file a runnable form needs")]
        public void Import_EmptyFolder_CreatesAllFiles()
        {
            var plan = FormImporter.Plan(Quote(), Options());
            FormImporter.Apply(plan);

            Assert.False(plan.HasErrors);
            Assert.All(plan.Files, f => Assert.Equal(PlannedFileAction.Create, f.Action));
            var schema = XmlCodec.DeserializeFromFile<FormSchema>(PathOf("FormSchema", "Quote.FormSchema.xml"))!;
            Assert.Equal("ft_quote_line", schema.Tables!["QuoteLine"].DbTableName);
            Assert.NotNull(XmlCodec.DeserializeFromFile<TableSchema>(PathOf("TableSchema", "company", "ft_quote.TableSchema.xml")));
            Assert.NotNull(XmlCodec.DeserializeFromFile<TableSchema>(PathOf("TableSchema", "company", "ft_quote_line.TableSchema.xml")));
            Assert.NotNull(XmlCodec.DeserializeFromFile<FormLayout>(PathOf("FormLayout", "Quote.FormLayout.xml")));

            var categories = File.ReadAllText(PathOf("DbCategorySettings.xml"));
            Assert.Contains("TableName=\"ft_quote_line\"", categories, StringComparison.Ordinal);
            var menu = File.ReadAllText(PathOf("MenuSettings.xml"));
            Assert.Contains("<MenuFolder Id=\"sales\" Caption=\"Sales\"", menu, StringComparison.Ordinal);
            Assert.Contains("Id=\"quote\" Caption=\"Quotes\"", menu, StringComparison.Ordinal);
            Assert.Contains("ProgId=\"Quote\" DisplayName=\"Quotes\"", File.ReadAllText(PathOf("ProgramSettings.xml")), StringComparison.Ordinal);
            Assert.Contains("Key=\"Field.ref_customer_id.Caption\" Value=\"客戶代號\"",
                File.ReadAllText(PathOf("Language", "zh-TW", "Quote.Language.xml")), StringComparison.Ordinal);
            var menuTexts = File.ReadAllText(PathOf("Language", "zh-TW", "Menu.Language.xml"));
            Assert.Contains("Key=\"Entry.quote.Caption\" Value=\"報價單\"", menuTexts, StringComparison.Ordinal);
            Assert.Contains("Key=\"Folder.sales.Caption\" Value=\"銷售\"", menuTexts, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Importing the same workbook again writes nothing and keeps the layout")]
        public void Import_Again_ChangesNothing()
        {
            FormImporter.Apply(FormImporter.Plan(Quote(), Options()));

            var again = FormImporter.Plan(Quote(), Options());

            var layout = Assert.Single(again.Files, f => f.Path.EndsWith(".FormLayout.xml", StringComparison.Ordinal));
            Assert.Equal(PlannedFileAction.Skip, layout.Action);
            Assert.All(again.Files.Where(f => f != layout), f => Assert.Equal(PlannedFileAction.Unchanged, f.Action));
            Assert.DoesNotContain(again.Files, f => f.Writes);
        }

        [Fact]
        [DisplayName("Asking to regenerate the layout replaces an existing one")]
        public void Import_RegenerateLayout_OverwritesLayout()
        {
            FormImporter.Apply(FormImporter.Plan(Quote(), Options()));
            File.WriteAllText(PathOf("FormLayout", "Quote.FormLayout.xml"),
                XmlCodec.Serialize(new FormLayout { LayoutId = "Quote", Caption = "Hand tuned" }));

            var plan = FormImporter.Plan(Quote(), Options(regenerateLayout: true));

            var layout = Assert.Single(plan.Files, f => f.Path.EndsWith(".FormLayout.xml", StringComparison.Ordinal));
            Assert.Equal(PlannedFileAction.Overwrite, layout.Action);
        }

        [Fact]
        [DisplayName("Registering in an existing settings file keeps its comments and its other entries")]
        public void Import_ExistingMenu_KeepsCommentsAndUsesFolder()
        {
            const string existing = """
                <?xml version="1.0" encoding="utf-8"?>
                <!-- The application's navigation menu. -->
                <MenuSettings xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
                  <Items>
                    <MenuFolder Id="sales" Caption="Sales" Order="10">
                      <Items>
                        <!-- Keep orders first. -->
                        <MenuEntry Id="order" Caption="Orders" Order="10" ProgId="Order" />
                      </Items>
                    </MenuFolder>
                  </Items>
                </MenuSettings>
                """;
            File.WriteAllText(PathOf("MenuSettings.xml"), existing);

            FormImporter.Apply(FormImporter.Plan(Quote(), Options()));

            var menu = File.ReadAllText(PathOf("MenuSettings.xml"));
            Assert.Contains("<!-- The application's navigation menu. -->", menu, StringComparison.Ordinal);
            Assert.Contains("<!-- Keep orders first. -->", menu, StringComparison.Ordinal);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(menu, "<MenuFolder ", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)));
            Assert.Contains("Id=\"quote\" Caption=\"Quotes\" Order=\"20\" ProgId=\"Quote\"", menu, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("An entry added to an existing settings file sits on its own line, indented like its siblings")]
        public void Import_ExistingSettings_IndentsAddedEntry()
        {
            const string existing = """
                <?xml version="1.0" encoding="utf-8"?>
                <ProgramSettings xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
                  <Items>
                    <ProgramItem ProgId="Order" DisplayName="Orders" />
                  </Items>
                </ProgramSettings>
                """;
            File.WriteAllText(PathOf("ProgramSettings.xml"), existing);

            FormImporter.Apply(FormImporter.Plan(Quote(), Options()));

            var lines = File.ReadAllLines(PathOf("ProgramSettings.xml"));
            Assert.Contains("    <ProgramItem ProgId=\"Order\" DisplayName=\"Orders\" />", lines);
            Assert.Contains("    <ProgramItem ProgId=\"Quote\" DisplayName=\"Quotes\" />", lines);
            Assert.Equal("</ProgramSettings>", lines[^1]);
        }

        [Fact]
        [DisplayName("A form with a validation error writes no file at all")]
        public void Import_ValidationError_WritesNothing()
        {
            var content = Quote();
            content.Schema.Tables!["QuoteLine"].Fields!["qty"].NumberKind = NumberKind.Quantity;

            var plan = FormImporter.Plan(content, Options());

            Assert.True(plan.HasErrors);
            Assert.Empty(plan.Files);
            Assert.Throws<InvalidOperationException>(() => FormImporter.Apply(plan));
            Assert.False(File.Exists(PathOf("FormSchema", "Quote.FormSchema.xml")));
        }

        [Fact]
        [DisplayName("A validation issue about a field read from a workbook points at the workbook row")]
        public void Import_IssueOfWorkbookField_PointsAtRow()
        {
            using var workbook = new FilledWorkbook().Form("ProgId", "Quote")
                .Field(("FieldName", "qty"), ("DbType", "Decimal"), ("NumberKind", "Quantity"));
            var content = workbook.Read().Content!;

            var plan = FormImporter.Plan(content, Options());

            var issue = Assert.Single(plan.Issues, i => i.Severity == IssueSeverity.Error);
            Assert.StartsWith($"Fields!{FormWorkbookCatalog.FirstDataRow} (Quote.qty.UnitField)", issue.Location, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A relation to a form missing from the folder is a warning, not an error")]
        public void Import_UnknownRelation_IsWarning()
        {
            var content = Quote();
            content.Schema.Tables!["Quote"].Fields!["customer_rowid"].RelationProgId = "Client";

            var plan = FormImporter.Plan(content, Options());

            Assert.False(plan.HasErrors);
            Assert.Contains(plan.Issues, i => i.Severity == IssueSeverity.Warning && i.Message.Contains("'Client'", StringComparison.Ordinal));
        }
    }
}
