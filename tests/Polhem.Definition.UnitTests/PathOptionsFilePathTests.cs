using System.ComponentModel;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// File path composition tests for <see cref="PathOptions"/>. They work directly on a PathOptions instance
    /// and do not touch process-wide statics such as <c>DefinePathInfo</c>, so they can run in parallel with other test classes.
    /// </summary>
    /// <remarks>
    /// The earlier <c>DefinePathInfoTests</c> switched the global state of <c>DefinePathInfo.CurrentOptions</c>
    /// in try/finally and needed <c>[Collection("Initialize")]</c> to avoid races. They were rewritten as pure
    /// PathOptions instance tests: <c>DefinePathInfo</c> was only a thin facade (every method delegated directly
    /// to PathOptions), so testing PathOptions gives the same coverage. The facade itself has since been removed.
    /// </remarks>
    public class PathOptionsFilePathTests
    {
        private const string TestRoot = "/tmp/polhem-define-tests";

        [Fact]
        [DisplayName("GetSystemSettingsFilePath returns SystemSettings.xml under the definition root")]
        public void GetSystemSettingsFilePath_ValidDefinePath_ReturnsExpectedPath()
        {
            var paths = new PathOptions { DefinePath = TestRoot };
            Assert.Equal(Path.Combine(TestRoot, "SystemSettings.xml"), paths.GetSystemSettingsFilePath());
        }

        [Fact]
        [DisplayName("GetDatabaseSettingsFilePath returns DatabaseSettings.xml under the definition root")]
        public void GetDatabaseSettingsFilePath_ValidDefinePath_ReturnsExpectedPath()
        {
            var paths = new PathOptions { DefinePath = TestRoot };
            Assert.Equal(Path.Combine(TestRoot, "DatabaseSettings.xml"), paths.GetDatabaseSettingsFilePath());
        }

        [Fact]
        [DisplayName("GetProgramSettingsFilePath returns ProgramSettings.xml under the definition root")]
        public void GetProgramSettingsFilePath_ValidDefinePath_ReturnsExpectedPath()
        {
            var paths = new PathOptions { DefinePath = TestRoot };
            Assert.Equal(Path.Combine(TestRoot, "ProgramSettings.xml"), paths.GetProgramSettingsFilePath());
        }

        [Fact]
        [DisplayName("GetDbCategorySettingsFilePath returns DbCategorySettings.xml under the definition root")]
        public void GetDbCategorySettingsFilePath_ValidDefinePath_ReturnsExpectedPath()
        {
            var paths = new PathOptions { DefinePath = TestRoot };
            Assert.Equal(Path.Combine(TestRoot, "DbCategorySettings.xml"), paths.GetDbCategorySettingsFilePath());
        }

        [Fact]
        [DisplayName("GetTableSchemaFilePath composes TableSchema/<categoryId>/<table>.TableSchema.xml")]
        public void GetTableSchemaFilePath_ValidInput_ReturnsExpectedPath()
        {
            var paths = new PathOptions { DefinePath = TestRoot };
            var expected = Path.Combine(TestRoot, "TableSchema", "common", "Employee.TableSchema.xml");
            Assert.Equal(expected, paths.GetTableSchemaFilePath("common", "Employee"));
        }

        [Fact]
        [DisplayName("GetFormSchemaFilePath composes FormSchema/<progId>.FormSchema.xml")]
        public void GetFormSchemaFilePath_ValidInput_ReturnsExpectedPath()
        {
            var paths = new PathOptions { DefinePath = TestRoot };
            var expected = Path.Combine(TestRoot, "FormSchema", "Employee.FormSchema.xml");
            Assert.Equal(expected, paths.GetFormSchemaFilePath("Employee"));
        }

        [Fact]
        [DisplayName("GetFormLayoutFilePath composes FormLayout/<layoutId>.FormLayout.xml")]
        public void GetFormLayoutFilePath_ValidInput_ReturnsExpectedPath()
        {
            var paths = new PathOptions { DefinePath = TestRoot };
            var expected = Path.Combine(TestRoot, "FormLayout", "EmployeeDefault.FormLayout.xml");
            Assert.Equal(expected, paths.GetFormLayoutFilePath("EmployeeDefault"));
        }

        [Fact]
        [DisplayName("GetLanguageFilePath composes Language/<lang>/<namespace>.Language.xml")]
        public void GetLanguageFilePath_ValidInput_ReturnsExpectedPath()
        {
            var paths = new PathOptions { DefinePath = TestRoot };
            var expected = Path.Combine(TestRoot, "Language", "zh-TW", "Customer.Language.xml");
            Assert.Equal(expected, paths.GetLanguageFilePath("zh-TW", "Customer"));
        }
    }
}
