using System.ComponentModel;
using Polhem.Definition;

namespace Polhem.Cli.UnitTests
{
    /// <summary>
    /// Tests for the <c>defines</c> command group driven through <see cref="Program.Main"/>: option parsing, the
    /// exit codes of usage errors, and the file effects of <c>materialize</c> and <c>list</c>.
    /// </summary>
    public sealed class DefinesCommandTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"polhem-cli-defines-{Guid.NewGuid():N}");

        public DefinesCommandTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            if (Directory.Exists(_root)) { Directory.Delete(_root, recursive: true); }
        }

        [Theory]
        [InlineData(new[] { "defines", "bogus" }, "unknown 'defines' subcommand: 'bogus'")]
        [InlineData(new[] { "defines", "materialize" }, "missing required option: --path")]
        [InlineData(new[] { "defines", "materialize", "--path" }, "--path requires a value")]
        [InlineData(new[] { "defines", "materialize", "--path", "x", "--filter" }, "--filter requires a value")]
        [InlineData(new[] { "defines", "materialize", "--bogus" }, "unknown option for 'materialize': '--bogus'")]
        [InlineData(new[] { "defines", "list", "--bogus" }, "unknown option for 'list': '--bogus'")]
        [DisplayName("A malformed defines command line exits with Usage and names the problem on stderr")]
        public void Defines_MalformedCommandLine_ExitsWithUsage(string[] args, string expectedMessage)
        {
            var (exitCode, _, error) = ConsoleCapture.Run(args);

            Assert.Equal(ExitCodes.Usage, exitCode);
            Assert.Contains("error: " + expectedMessage, error, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("defines list prints every embedded default, one relative path per line")]
        public void List_PrintsEveryEmbeddedDefault()
        {
            var (exitCode, output, _) = ConsoleCapture.Run("defines", "list");

            Assert.Equal(ExitCodes.Success, exitCode);
            var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(Defaults.ListEmbedded(), lines);
        }

        [Fact]
        [DisplayName("defines materialize with --filter writes only the defaults under that prefix")]
        public void Materialize_WithFilter_WritesOnlyMatchingFiles()
        {
            var expected = Defaults.ListEmbedded()
                .Where(p => p.StartsWith("TableSchema/", StringComparison.Ordinal))
                .ToList();
            Assert.NotEmpty(expected);

            var (exitCode, output, _) = ConsoleCapture.Run(
                "defines", "materialize", "--path", _root, "--filter", "TableSchema/");

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains($"Materialized {expected.Count} file(s)", output, StringComparison.Ordinal);
            var written = Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(_root, f).Replace(Path.DirectorySeparatorChar, '/'))
                .OrderBy(p => p, StringComparer.Ordinal);
            Assert.Equal(expected.OrderBy(p => p, StringComparer.Ordinal), written);
        }

        [Fact]
        [DisplayName("defines materialize keeps an existing file unless --overwrite is passed")]
        public void Materialize_ExistingFile_SkippedUnlessOverwrite()
        {
            ConsoleCapture.Run("defines", "materialize", "--path", _root, "--filter", "SystemSettings.xml");
            string target = Path.Combine(_root, "SystemSettings.xml");
            string original = File.ReadAllText(target);
            File.WriteAllText(target, "customised");

            var (skipExit, skipOutput, _) = ConsoleCapture.Run(
                "defines", "materialize", "--path", _root, "--filter", "SystemSettings.xml");

            Assert.Equal(ExitCodes.Success, skipExit);
            Assert.Contains("Skipped 1 existing file(s)", skipOutput, StringComparison.Ordinal);
            Assert.Equal("customised", File.ReadAllText(target));

            var (overwriteExit, _, _) = ConsoleCapture.Run(
                "defines", "materialize", "--path", _root, "--filter", "SystemSettings.xml", "--overwrite");

            Assert.Equal(ExitCodes.Success, overwriteExit);
            Assert.Equal(original, File.ReadAllText(target));
        }
    }
}
