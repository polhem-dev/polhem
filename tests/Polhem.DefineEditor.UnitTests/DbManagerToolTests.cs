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
        [DisplayName("The command runs the tool of this version from nuget.org, in the given folder, with the packages folder pinned")]
        public void Command_RunsPinnedToolOutsideSolution()
        {
            var work = Path.Combine(Path.GetTempPath(), "work");
            var command = DbManagerTool.Command(s_define, ["check"], work);

            Assert.Equal("dotnet", command.Command);
            Assert.Equal(["tool", "exec", $"Polhem.DbManager@{DbManagerTool.Version}", "--yes", "--source", DbManagerTool.NuGetSource,
                "--", "check", "--define", s_define], command.Arguments);
            Assert.Equal(work, command.WorkingDirectory);
            Assert.Equal(DbManagerTool.GlobalPackagesFolder(), command.Environment["NUGET_PACKAGES"]);
        }

        [Fact]
        [DisplayName("An extra source is a second --source before the separator, a local one as a full path")]
        public void Command_ExtraSource_AddsFullPathSource()
        {
            var arguments = DbManagerTool.Command(s_define, ["check"], "/work", "nupkgs").Arguments;

            var separator = arguments.IndexOf("--");
            Assert.Equal(Path.GetFullPath("nupkgs"), arguments[separator - 1]);
            Assert.Equal("--source", arguments[separator - 2]);
            Assert.Equal("https://feed.example.com/v3/index.json", DbManagerTool.FullSource("https://feed.example.com/v3/index.json"));
            var local = Path.Combine(Path.GetTempPath(), "nupkgs");
            Assert.Equal(local, DbManagerTool.FullSource(new Uri(local).AbsoluteUri));
        }

        [Fact]
        [DisplayName("The work folder is the user's own, under the local application data folder")]
        public void WorkDirectory_IsUnderLocalApplicationData()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var folder = DbManagerTool.WorkDirectory();

            Assert.StartsWith(local, folder, StringComparison.Ordinal);
            Assert.True(Directory.Exists(folder));
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
        [DisplayName("The tool counts as cached once its hash file is written, not when only its folder exists")]
        public void IsCached_LooksForVersionFolder()
        {
            var packages = Path.Combine(Path.GetTempPath(), $"polhem-packages-{Guid.NewGuid():N}");
            try
            {
                Assert.False(DbManagerTool.IsCached(packages));
                var version = DbManagerTool.Version.ToLowerInvariant();
                var folder = Path.Combine(packages, "polhem.dbmanager", version);
                Directory.CreateDirectory(folder);
                Assert.False(DbManagerTool.IsCached(packages));
                File.WriteAllText(Path.Combine(folder, $"polhem.dbmanager.{version}.nupkg.sha512"), "x");
                Assert.True(DbManagerTool.IsCached(packages));
            }
            finally
            {
                Directory.Delete(packages, recursive: true);
            }
        }
    }
}
