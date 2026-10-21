using Polhem.Core.Data;
using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Tests.Shared;

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

        private const string HandTunedTable = """
            <?xml version="1.0" encoding="utf-8"?>
            <!-- Hand-tuned: lengths, an extra column and an index the form does not know about. -->
            <TableSchema xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" TableName="ft_quote_line" DisplayName="Quote Lines">
              <Fields>
                <DbField FieldName="sys_no" Caption="Sequence" DbType="AutoIncrement" />
                <DbField FieldName="sys_rowid" Caption="Row ID" DbType="Guid" />
                <DbField FieldName="legacy_code" Caption="Legacy Code" DbType="String" Length="12" />
              </Fields>
              <Indexes>
                <DbTableIndex Name="ix_{0}_legacy" Unique="false">
                  <IndexFields>
                    <IndexField FieldName="legacy_code" />
                  </IndexFields>
                </DbTableIndex>
              </Indexes>
            </TableSchema>
            """;

        private string QuoteLineTablePath => PathOf("TableSchema", "company", "ft_quote_line.TableSchema.xml");

        [Fact]
        [DisplayName("Merging into an existing TableSchema adds the form's columns and keeps the hand-made ones")]
        public void Import_MergeTableSchema_AddsAndKeeps()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(QuoteLineTablePath)!);
            File.WriteAllText(QuoteLineTablePath, HandTunedTable);

            var plan = FormImporter.Plan(Quote(), Options());
            FormImporter.Apply(plan);

            Assert.Equal(PlannedFileAction.Update, plan.Files.Single(f => f.Path == QuoteLineTablePath).Action);
            var xml = File.ReadAllText(QuoteLineTablePath);
            Assert.Contains("Hand-tuned", xml, StringComparison.Ordinal);
            Assert.Contains("FieldName=\"legacy_code\" Caption=\"Legacy Code\" DbType=\"String\" Length=\"12\"", xml, StringComparison.Ordinal);
            Assert.Contains("ix_{0}_legacy", xml, StringComparison.Ordinal);
            var table = XmlCodec.DeserializeFromFile<TableSchema>(QuoteLineTablePath)!;
            Assert.Contains(table.Fields!, f => f.FieldName == "qty");
            Assert.Contains(table.Fields!, f => f.FieldName == "sys_master_rowid");
        }

        [Theory]
        [InlineData(TableSchemaImportMode.Keep, PlannedFileAction.Skip)]
        [InlineData(TableSchemaImportMode.Overwrite, PlannedFileAction.Overwrite)]
        [DisplayName("Keep leaves an existing TableSchema alone and overwrite replaces it")]
        public void Import_TableSchemaMode_DecidesAction(TableSchemaImportMode mode, PlannedFileAction expected)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(QuoteLineTablePath)!);
            File.WriteAllText(QuoteLineTablePath, HandTunedTable);

            var plan = FormImporter.Plan(Quote(), new FormImportOptions { DefinePath = _define, TableSchemaMode = mode });

            Assert.Equal(expected, plan.Files.Single(f => f.Path == QuoteLineTablePath).Action);
        }

        private static FormWorkbookContent QuoteWithNotes()
        {
            var content = Quote();
            content.Schema.Tables!["Quote"].Fields!.Add("notes", "Notes", FieldDbType.String);
            return content;
        }

        [Fact]
        [DisplayName("A String column without a max length stops the import at its field, since no database builds it")]
        public void Import_UnsizedStringColumn_IsError()
        {
            var plan = FormImporter.Plan(QuoteWithNotes(), Options());

            Assert.True(plan.HasErrors);
            Assert.Empty(plan.Files);
            var issue = Assert.Single(plan.Issues, i => i.Severity == IssueSeverity.Error);
            Assert.Contains("Quote.notes", issue.Location, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("An unsized String column is reported even when the existing TableSchema already matches the form")]
        public void Import_UnsizedStringColumnAlreadyWritten_IsError()
        {
            var path = PathOf("TableSchema", "company", "ft_quote.TableSchema.xml");
            var content = QuoteWithNotes();
            var generated = FormImporter.Plan(Quote(), Options());
            Assert.False(generated.HasErrors);
            FormImporter.Apply(generated);
            var unsized = XmlCodec.DeserializeFromFile<TableSchema>(path)!;
            unsized.Fields!.Add("notes", "Notes", FieldDbType.String);
            File.WriteAllText(path, XmlCodec.Serialize(unsized));

            var plan = FormImporter.Plan(content, new FormImportOptions { DefinePath = _define, TableSchemaMode = TableSchemaImportMode.Overwrite });

            Assert.True(plan.HasErrors);
        }

        [Fact]
        [DisplayName("Merging gives an existing unsized String column the workbook's length, and keeps a sized one")]
        public void Import_MergeIntoUnsizedColumn_TakesFormLength()
        {
            var path = PathOf("TableSchema", "company", "ft_quote.TableSchema.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, """
                <?xml version="1.0" encoding="utf-8"?>
                <TableSchema TableName="ft_quote" DisplayName="Quote">
                  <Fields>
                    <DbField FieldName="sys_id" Caption="Quote No" DbType="String" />
                    <DbField FieldName="legacy" Caption="Legacy" DbType="String" Length="8" />
                  </Fields>
                </TableSchema>
                """);

            var plan = FormImporter.Plan(Quote(), Options());
            FormImporter.Apply(plan);

            Assert.False(plan.HasErrors);
            var table = XmlCodec.DeserializeFromFile<TableSchema>(path)!;
            Assert.Equal(20, table.Fields!["sys_id"].Length);
            Assert.Equal(8, table.Fields["legacy"].Length);
        }

        [Fact]
        [DisplayName("A String column the existing TableSchema already sizes imports, since the merge keeps its length")]
        public void Import_UnsizedStringColumnSizedInExistingTable_Imports()
        {
            var path = PathOf("TableSchema", "company", "ft_quote.TableSchema.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, """
                <?xml version="1.0" encoding="utf-8"?>
                <TableSchema TableName="ft_quote" DisplayName="Quote">
                  <Fields>
                    <DbField FieldName="notes" Caption="Notes" DbType="String" Length="200" />
                  </Fields>
                </TableSchema>
                """);

            var plan = FormImporter.Plan(QuoteWithNotes(), Options());

            Assert.False(plan.HasErrors);
        }

        [Fact]
        [DisplayName("Exporting a form whose lengths live only in its TableSchema shows them in the workbook, so it imports again")]
        public void Export_LengthOnlyInTableSchema_FillsMaxLength()
        {
            FormImporter.Apply(FormImporter.Plan(Quote(), Options()));
            var schemaPath = PathOf("FormSchema", "Quote.FormSchema.xml");
            var schema = XmlCodec.DeserializeFromFile<FormSchema>(schemaPath)!;
            schema.Tables!["Quote"].Fields!["sys_id"].MaxLength = 0;
            File.WriteAllText(schemaPath, XmlCodec.Serialize(schema));

            var exported = FormExporter.Load(_define, "Quote");

            Assert.Equal(20, exported.Schema.Tables!["Quote"].Fields!["sys_id"].MaxLength);
        }

        public static TheoryData<string> NorthwindForms()
        {
            const string suffix = ".FormSchema.xml";
            var data = new TheoryData<string>();
            foreach (var file in Directory.EnumerateFiles(Path.Combine(NorthwindDefine, "FormSchema"), "*" + suffix).Order(StringComparer.Ordinal))
                data.Add(Path.GetFileName(file)[..^suffix.Length]);
            return data;
        }

        private static string NorthwindDefine => Path.Combine(RepoRoot.Find(), "apps", "Polhem.Northwind", "Define");

        [Theory]
        [MemberData(nameof(NorthwindForms))]
        [DisplayName("A Northwind form exported and imported back into a copy of its folder writes nothing")]
        public void ExportThenImport_Northwind_WritesNothing(string progId)
        {
            CopyDirectory(NorthwindDefine, _define);
            using var stream = new MemoryStream();
            FormWorkbookWriter.Write(FormExporter.Load(_define, progId), stream);
            stream.Position = 0;
            var read = FormWorkbookReader.Read(stream);

            var plan = FormImporter.Plan(read.Content!, Options());

            Assert.False(plan.HasErrors);
            Assert.DoesNotContain(plan.Files, f => f.Writes);
        }

        private static void CopyDirectory(string source, string target)
        {
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(target, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, overwrite: true);
            }
        }

        [Fact]
        [DisplayName("A framework table created by an import starts from the framework's TableSchema and adds the form's columns")]
        public void Import_FrameworkTable_StartsFromFrameworkDefault()
        {
            var schema = new FormSchema("Employee", "Employee") { CategoryId = "company" };
            var table = schema.Tables!.Add("Employee", "Employee");
            table.DbTableName = "st_employee";
            table.Fields!.Add("sys_id", "Employee Code", FieldDbType.String);
            table.Fields.Add("sys_name", "Employee Name", FieldDbType.String);
            table.Fields!.Add(new FormField("title", "Title", FieldDbType.String) { MaxLength = 50 });

            FormImporter.Apply(FormImporter.Plan(new FormWorkbookContent(schema), Options()));

            var created = XmlCodec.DeserializeFromFile<TableSchema>(PathOf("TableSchema", "company", "st_employee.TableSchema.xml"))!;
            // `user_rowid` is not on the form; sign-in reads it, so it must come from the framework's TableSchema.
            Assert.Contains(created.Fields!, f => f.FieldName == "user_rowid");
            Assert.Contains(created.Fields!, f => f.FieldName == "title");
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
