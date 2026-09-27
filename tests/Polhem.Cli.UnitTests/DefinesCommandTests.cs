using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Settings;

namespace Polhem.Cli.UnitTests
{
    /// <summary>
    /// Tests for the <c>defines</c> command group driven through <see cref="Program.Main"/>: option parsing, the
    /// exit codes of usage errors, and the file effects of <c>materialize</c>, <c>list</c> and <c>split-menu</c>.
    /// </summary>
    public sealed class DefinesCommandTests : IDisposable
    {
        private const string LegacyProgramSettings = """
            <?xml version="1.0" encoding="utf-8"?>
            <ProgramSettings>
              <Categories>
                <ProgramCategory Id="master-data" DisplayName="Master data">
                  <Items>
                    <ProgramItem ProgId="Customer" DisplayName="Customer" />
                  </Items>
                </ProgramCategory>
              </Categories>
            </ProgramSettings>
            """;

        private readonly string _root = Path.Combine(Path.GetTempPath(), $"polhem-cli-defines-{Guid.NewGuid():N}");

        public DefinesCommandTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            if (Directory.Exists(_root)) { Directory.Delete(_root, recursive: true); }
        }

        private string ProgramSettingsPath => Path.Combine(_root, "ProgramSettings.xml");

        private string MenuSettingsPath => Path.Combine(_root, "MenuSettings.xml");

        [Theory]
        [InlineData(new[] { "defines", "bogus" }, "unknown 'defines' subcommand: 'bogus'")]
        [InlineData(new[] { "defines", "materialize" }, "missing required option: --path")]
        [InlineData(new[] { "defines", "materialize", "--path" }, "--path requires a value")]
        [InlineData(new[] { "defines", "materialize", "--path", "x", "--filter" }, "--filter requires a value")]
        [InlineData(new[] { "defines", "materialize", "--bogus" }, "unknown option for 'materialize': '--bogus'")]
        [InlineData(new[] { "defines", "list", "--bogus" }, "unknown option for 'list': '--bogus'")]
        [InlineData(new[] { "defines", "split-menu" }, "missing required option: --path")]
        [InlineData(new[] { "defines", "split-menu", "--bogus" }, "unknown option for 'split-menu': '--bogus'")]
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

        [Fact]
        [DisplayName("defines split-menu exits with Usage when the directory has no ProgramSettings.xml")]
        public void SplitMenu_NoProgramSettings_ExitsWithUsage()
        {
            var (exitCode, _, error) = ConsoleCapture.Run("defines", "split-menu", "--path", _root);

            Assert.Equal(ExitCodes.Usage, exitCode);
            Assert.Contains("ProgramSettings.xml not found", error, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("defines split-menu refuses to replace an existing MenuSettings.xml and leaves both files untouched")]
        public void SplitMenu_ExistingMenuWithoutOverwrite_RefusesAndWritesNothing()
        {
            File.WriteAllText(ProgramSettingsPath, LegacyProgramSettings);
            File.WriteAllText(MenuSettingsPath, "hand-authored menu");

            var (exitCode, _, error) = ConsoleCapture.Run("defines", "split-menu", "--path", _root);

            Assert.Equal(ExitCodes.Usage, exitCode);
            Assert.Contains("already exists", error, StringComparison.Ordinal);
            Assert.Equal(LegacyProgramSettings, File.ReadAllText(ProgramSettingsPath));
            Assert.Equal("hand-authored menu", File.ReadAllText(MenuSettingsPath));
        }

        [Fact]
        [DisplayName("defines split-menu with --overwrite replaces an existing MenuSettings.xml and flattens the registry")]
        public void SplitMenu_ExistingMenuWithOverwrite_ReplacesIt()
        {
            File.WriteAllText(ProgramSettingsPath, LegacyProgramSettings);
            File.WriteAllText(MenuSettingsPath, "hand-authored menu");

            var (exitCode, _, _) = ConsoleCapture.Run("defines", "split-menu", "--path", _root, "--overwrite");

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains("Customer", File.ReadAllText(MenuSettingsPath), StringComparison.Ordinal);
            Assert.False(ProgramSettingsFormat.IsLegacyFormat(File.ReadAllText(ProgramSettingsPath)));
        }

        [Fact]
        [DisplayName("defines split-menu with --dry-run reports the migration without writing any file")]
        public void SplitMenu_DryRun_WritesNothing()
        {
            File.WriteAllText(ProgramSettingsPath, LegacyProgramSettings);

            var (exitCode, output, _) = ConsoleCapture.Run("defines", "split-menu", "--path", _root, "--dry-run");

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains("--dry-run: no files were written.", output, StringComparison.Ordinal);
            Assert.Contains("entry  ", output, StringComparison.Ordinal);
            Assert.Equal(LegacyProgramSettings, File.ReadAllText(ProgramSettingsPath));
            Assert.False(File.Exists(MenuSettingsPath));
        }

        [Fact]
        [DisplayName("defines split-menu on an already flat ProgramSettings.xml reports nothing to migrate and writes no menu")]
        public void SplitMenu_AlreadyFlat_NothingToMigrate()
        {
            File.WriteAllText(ProgramSettingsPath, LegacyProgramSettings);
            ConsoleCapture.Run("defines", "split-menu", "--path", _root);
            File.Delete(MenuSettingsPath);

            var (exitCode, output, _) = ConsoleCapture.Run("defines", "split-menu", "--path", _root);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains("nothing to migrate", output, StringComparison.Ordinal);
            Assert.False(File.Exists(MenuSettingsPath));
        }
    }
}
