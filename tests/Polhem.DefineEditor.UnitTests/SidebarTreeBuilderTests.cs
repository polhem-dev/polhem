using System.ComponentModel;
using Polhem.Definition;
using Polhem.DefineEditor.Models;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The Programs and Databases views of the sidebar: which define files each node lists, and what happens when a
    /// file the view starts from is missing.
    /// </summary>
    public sealed class SidebarTreeBuilderTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-sidebar-trees-" + Guid.NewGuid().ToString("N"));

        public SidebarTreeBuilderTests()
        {
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private string Write(string relativePath, string content = "<x/>")
        {
            var path = Path.Combine(_directory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        private void WriteProgramSettings(params string[] items) => Write("ProgramSettings.xml",
            "<ProgramSettings><Items>" + string.Concat(items) + "</Items></ProgramSettings>");

        private void WriteOrderFormSchema() => Write(Path.Combine("FormSchema", "Order.FormSchema.xml"),
            """
            <FormSchema ProgId="Order" DisplayName="Order" CategoryId="company">
              <Tables>
                <FormTable TableName="Order" DbTableName="ft_order" />
                <FormTable TableName="OrderDetail" DbTableName="ft_order_detail" />
                <FormTable TableName="ft_note" />
              </Tables>
            </FormSchema>
            """);

        [Fact]
        [DisplayName("Programs lists each registered ProgId with its FormSchema, FormLayout, tables and languages")]
        public void ProgramTree_RegisteredProgId_ListsItsDefineFiles()
        {
            WriteProgramSettings("<ProgramItem ProgId=\"Order\" DisplayName=\"Orders\" />");
            var formSchema = Path.Combine(_directory, "FormSchema", "Order.FormSchema.xml");
            WriteOrderFormSchema();
            var formLayout = Write(Path.Combine("FormLayout", "Order.FormLayout.xml"));
            var master = Write(Path.Combine("TableSchema", "company", "ft_order.TableSchema.xml"));
            var detail = Write(Path.Combine("TableSchema", "company", "ft_order_detail.TableSchema.xml"));
            var note = Write(Path.Combine("TableSchema", "company", "ft_note.TableSchema.xml"));
            var english = Write(Path.Combine("Language", "en-US", "Order.Language.xml"));
            var chinese = Write(Path.Combine("Language", "zh-TW", "Order.Language.xml"));

            var program = Assert.Single(ProgramTreeBuilder.Build(_directory));

            Assert.Equal("Order - Orders", program.Name);
            Assert.Equal(DefineNodeKind.Group, program.Kind);
            Assert.Equal(
                [
                    ("FormSchema", DefineType.FormSchema, formSchema),
                    ("FormLayout", DefineType.FormLayout, formLayout),
                    ("ft_order", DefineType.TableSchema, master),
                    ("ft_order_detail", DefineType.TableSchema, detail),
                    ("ft_note", DefineType.TableSchema, note),
                    ("Language (en-US)", DefineType.Language, english),
                    ("Language (zh-TW)", DefineType.Language, chinese),
                ],
                program.Children.Select(c => (c.Name, c.DefineType!.Value, c.FilePath!)));
            Assert.All(program.Children, c => Assert.Equal(DefineNodeKind.DefineFile, c.Kind));
        }

        [Fact]
        [DisplayName("Programs leaves out the define files of a ProgId that do not exist")]
        public void ProgramTree_MissingFiles_AreNotListed()
        {
            WriteProgramSettings("<ProgramItem ProgId=\"Order\" />");
            WriteOrderFormSchema();
            Write(Path.Combine("TableSchema", "company", "ft_order.TableSchema.xml"));

            var program = Assert.Single(ProgramTreeBuilder.Build(_directory));

            Assert.Equal("Order", program.Name);
            Assert.Equal(["FormSchema", "ft_order"], program.Children.Select(c => c.Name));
        }

        [Fact]
        [DisplayName("Programs lists only the ProgIds ProgramSettings registers, in its order")]
        public void ProgramTree_UnregisteredFormSchema_IsNotListed()
        {
            WriteProgramSettings("<ProgramItem ProgId=\"Supplier\" />", "<ProgramItem ProgId=\"Customer\" />");
            WriteOrderFormSchema();

            var programs = ProgramTreeBuilder.Build(_directory);

            Assert.Equal(["Supplier", "Customer"], programs.Select(p => p.Name));
            Assert.All(programs, p => Assert.Empty(p.Children));
        }

        [Fact]
        [DisplayName("Programs is empty when ProgramSettings.xml is missing or cannot be read")]
        public void ProgramTree_NoReadableProgramSettings_ReturnsEmpty()
        {
            Assert.Empty(ProgramTreeBuilder.Build(_directory));

            Write("ProgramSettings.xml", "not xml");
            Assert.Empty(ProgramTreeBuilder.Build(_directory));
        }

        [Fact]
        [DisplayName("Table Schemas lists each category by its id with the tables that have a TableSchema file")]
        public void DbCategoryTree_Categories_ListTablesWithTableSchema()
        {
            Write("DbCategorySettings.xml",
                """
                <DbCategorySettings>
                  <Categories>
                    <DbCategory Id="common" DisplayName="Common">
                      <Tables>
                        <TableItem TableName="st_user" DisplayName="Users" />
                        <TableItem TableName="st_missing" DisplayName="No schema" />
                      </Tables>
                    </DbCategory>
                    <DbCategory Id="log" />
                  </Categories>
                </DbCategorySettings>
                """);
            var user = Write(Path.Combine("TableSchema", "common", "st_user.TableSchema.xml"));

            var categories = DbCategoryTreeBuilder.Build(_directory);

            Assert.Equal(["common", "log"], categories.Select(c => c.Name));
            var table = Assert.Single(categories[0].Children);
            Assert.Equal("st_user - Users", table.Name);
            Assert.Equal(DefineNodeKind.DefineFile, table.Kind);
            Assert.Equal(DefineType.TableSchema, table.DefineType);
            Assert.Equal(user, table.FilePath);
            Assert.Empty(categories[1].Children);
        }

        [Fact]
        [DisplayName("Table Schemas is empty when DbCategorySettings.xml is missing")]
        public void DbCategoryTree_NoDbCategorySettings_ReturnsEmpty()
        {
            Assert.Empty(DbCategoryTreeBuilder.Build(_directory));
        }
    }
}
