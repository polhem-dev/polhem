using System.ComponentModel;
using Polhem.Tests.Shared;

namespace Polhem.Cli.UnitTests
{
    /// <summary>
    /// Tests for the <c>forms</c> command group driven through <see cref="Program.Main"/>: option parsing, and the
    /// file effects of <c>template</c>, <c>export</c> and <c>import</c> in a temporary folder.
    /// </summary>
    public sealed class FormsCommandTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"polhem-cli-forms-{Guid.NewGuid():N}");

        public FormsCommandTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            if (Directory.Exists(_root)) { Directory.Delete(_root, recursive: true); }
        }

        private static string NorthwindDefine => Path.Combine(RepoRoot.Find(), "apps", "Polhem.Northwind", "Define");

        [Theory]
        [InlineData(new[] { "forms", "bogus" }, "unknown 'forms' subcommand: 'bogus'")]
        [InlineData(new[] { "forms", "template" }, "missing required option: --out")]
        [InlineData(new[] { "forms", "template", "--out" }, "--out requires a value")]
        [InlineData(new[] { "forms", "export", "--define", "x" }, "missing required argument: <ProgId>")]
        [InlineData(new[] { "forms", "export", "Order" }, "missing required option: --define")]
        [InlineData(new[] { "forms", "import", "a.xlsx", "b.xlsx" }, "unknown option for 'import': 'b.xlsx'")]
        [InlineData(new[] { "forms", "import", "a.xlsx", "--bogus" }, "unknown option for 'import': '--bogus'")]
        [DisplayName("A malformed forms command line exits with Usage and names the problem on stderr")]
        public void Forms_MalformedCommandLine_ExitsWithUsage(string[] args, string expectedMessage)
        {
            var (exitCode, _, error) = ConsoleCapture.Run(args);

            Assert.Equal(ExitCodes.Usage, exitCode);
            Assert.Contains("error: " + expectedMessage, error, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("forms template writes an empty workbook to the given path")]
        public void Template_WritesWorkbook()
        {
            var output = Path.Combine(_root, "new", "Blank.xlsx");

            var (exitCode, _, _) = ConsoleCapture.Run("forms", "template", "--out", output);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.True(new FileInfo(output).Length > 0);
        }

        [Fact]
        [DisplayName("A form exported and imported into an empty folder produces its definition files")]
        public void ExportThenImport_WritesDefinitions()
        {
            var workbook = Path.Combine(_root, "Order.xlsx");
            var define = Path.Combine(_root, "Define");
            Assert.Equal(ExitCodes.Success,
                ConsoleCapture.Run("forms", "export", "Order", "--define", NorthwindDefine, "--out", workbook).ExitCode);

            var (exitCode, output, _) = ConsoleCapture.Run("forms", "import", workbook, "--define", define);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains("+ " + Path.Combine("FormSchema", "Order.FormSchema.xml"), output, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(define, "FormSchema", "Order.FormSchema.xml")));
            Assert.True(File.Exists(Path.Combine(define, "TableSchema", "company", "ft_order_detail.TableSchema.xml")));
            Assert.True(File.Exists(Path.Combine(define, "FormLayout", "Order.FormLayout.xml")));
            Assert.True(File.Exists(Path.Combine(define, "MenuSettings.xml")));
        }

        [Fact]
        [DisplayName("forms import --dry-run lists the files and writes none of them")]
        public void Import_DryRun_WritesNothing()
        {
            var workbook = Path.Combine(_root, "Order.xlsx");
            var define = Path.Combine(_root, "Define");
            ConsoleCapture.Run("forms", "export", "Order", "--define", NorthwindDefine, "--out", workbook);

            var (exitCode, output, _) = ConsoleCapture.Run("forms", "import", workbook, "--define", define, "--dry-run");

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains("Dry run", output, StringComparison.Ordinal);
            Assert.False(Directory.Exists(define));
        }

        [Fact]
        [DisplayName("forms import of a workbook with an error exits with Error and writes nothing")]
        public void Import_WorkbookWithoutProgId_Fails()
        {
            var workbook = Path.Combine(_root, "Blank.xlsx");
            var define = Path.Combine(_root, "Define");
            ConsoleCapture.Run("forms", "template", "--out", workbook);

            var (exitCode, _, error) = ConsoleCapture.Run("forms", "import", workbook, "--define", define);

            Assert.Equal(ExitCodes.Error, exitCode);
            Assert.Contains("ProgId", error, StringComparison.Ordinal);
            Assert.False(Directory.Exists(define));
        }
    }
}
