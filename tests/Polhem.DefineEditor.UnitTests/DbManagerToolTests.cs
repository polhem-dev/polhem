using System.ComponentModel;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// How DefineEditor runs the database manager: the command line, the version it pins and the download check.
    /// </summary>
    public class DbManagerToolTests
    {
        private static readonly string s_define = Path.Combine(Path.GetTempPath(), "project", "Define");

        [Fact]
        [DisplayName("The command runs the tool of this version through dotnet tool exec, against the solution folder")]
        public void Command_RunsPinnedToolAgainstSolution()
        {
            var command = DbManagerTool.Command(s_define, ["check"]);

            Assert.Equal("dotnet", command.Command);
            Assert.Equal(["tool", "exec", $"Polhem.DbManager@{DbManagerTool.Version}", "--yes", "--", "check", "--define", s_define],
                command.Arguments);
            Assert.Equal(s_define, command.ResolveWorkingDirectory(s_define));
        }

        [Fact]
        [DisplayName("An extra package source is passed to dotnet tool exec, before the tool's own arguments")]
        public void Command_ExtraSource_AddsSourceBeforeSeparator()
        {
            var arguments = DbManagerTool.Command(s_define, ["check"], "/nupkgs").Arguments;

            Assert.True(arguments.IndexOf("--add-source") < arguments.IndexOf("--"));
            Assert.Equal("/nupkgs", arguments[arguments.IndexOf("--add-source") + 1]);
        }

        [Fact]
        [DisplayName("The upgrade arguments name each table with --table and end a preview with --dry-run")]
        public void UpgradeArguments_TablesAndDryRun()
        {
            Assert.Equal(["upgrade", "--table", "ft_order", "--table", "ft_order_detail", "--dry-run"],
                DbManagerTool.UpgradeArguments(["ft_order", "ft_order_detail"], dryRun: true));
            Assert.Equal(["upgrade"], DbManagerTool.UpgradeArguments([], dryRun: false));
        }

        [Fact]
        [DisplayName("The pinned version is a plain package version, without build metadata")]
        public void Version_HasNoBuildMetadata()
        {
            Assert.DoesNotContain("+", DbManagerTool.Version, StringComparison.Ordinal);
            Assert.True(System.Version.TryParse(DbManagerTool.Version.Split('-')[0], out _));
        }

        [Fact]
        [DisplayName("The tool counts as cached when its version folder is in the global packages folder")]
        public void IsCached_LooksForVersionFolder()
        {
            var packages = Path.Combine(Path.GetTempPath(), $"polhem-packages-{Guid.NewGuid():N}");
            try
            {
                Assert.False(DbManagerTool.IsCached(packages));
                Directory.CreateDirectory(Path.Combine(packages, "polhem.dbmanager", DbManagerTool.Version.ToLowerInvariant()));
                Assert.True(DbManagerTool.IsCached(packages));
            }
            finally
            {
                Directory.Delete(packages, recursive: true);
            }
        }
    }
}
