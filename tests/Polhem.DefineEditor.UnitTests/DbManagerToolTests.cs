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
        [DisplayName("The command runs the tool of this version through dotnet tool exec, in the editor's own folder, against the solution")]
        public void Command_RunsPinnedToolOutsideSolution()
        {
            var command = DbManagerTool.Command(s_define, ["check"], "/work", "/work/nuget.config");

            Assert.Equal("dotnet", command.Command);
            Assert.Equal(["tool", "exec", $"Polhem.DbManager@{DbManagerTool.Version}", "--yes", "--configfile", "/work/nuget.config",
                "--", "check", "--define", s_define], command.Arguments);
            Assert.Equal("/work", command.WorkingDirectory);
        }

        [Fact]
        [DisplayName("The work folder's NuGet configuration clears inherited sources and lists nuget.org and the extra source")]
        public void PrepareWorkDirectory_WritesOwnSources()
        {
            var root = Path.Combine(Path.GetTempPath(), $"polhem-work-{Guid.NewGuid():N}");
            try
            {
                var (workDirectory, configFile) = DbManagerTool.PrepareWorkDirectory(root, "/nupkgs");

                Assert.StartsWith(root, workDirectory, StringComparison.Ordinal);
                var sources = System.Xml.Linq.XDocument.Load(configFile).Root!.Element("packageSources")!.Elements().ToList();
                Assert.Equal("clear", sources[0].Name.LocalName);
                Assert.Equal([DbManagerTool.NuGetSource, "/nupkgs"], sources.Skip(1).Select(s => (string)s.Attribute("value")!));

                DbManagerTool.PrepareWorkDirectory(root, extraSource: null);
                Assert.Equal(2, System.Xml.Linq.XDocument.Load(configFile).Root!.Element("packageSources")!.Elements().Count());
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
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
