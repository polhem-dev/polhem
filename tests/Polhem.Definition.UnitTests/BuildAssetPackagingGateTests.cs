using System.ComponentModel;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Packaging gate: asserts that <c>Polhem.Definition.targets</c> ships in <c>buildTransitive/</c>,
    /// not in <c>build/</c>, which only takes effect for direct consumers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NuGet imports the <c>build/</c> folder only for a **direct** <c>PackageReference</c>. This targets file
    /// injects the consumer's definition files into <c>AdditionalFiles</c> for POLHEM1xxx / POLHEM2xxx to read. In
    /// <c>build/</c>, every project that references only <c>Polhem.Business</c> / <c>Polhem.Db</c> / <c>Polhem.Api.AspNetCore</c> /
    /// <c>Polhem.Hosting</c> (with <c>Polhem.Definition</c> as a transitive dependency) does not get it.
    /// </para>
    /// <para>
    /// This failure has **no symptom at all**: the analyzer assembly still flows in transitively and loads, it just
    /// reads no files, so the rules pass silently and no diagnostic points it out. It happened once, and was found
    /// only when an external repository (bee-northwind-avalonia, 4.21.0) measured zero <c>AdditionalFiles</c>.
    /// </para>
    /// <para>
    /// This test checks the repository's source layout and <c>Pack</c> declarations, not the packed nupkg:
    /// running <c>dotnet pack</c> in a unit test is too slow. The regression it guards against (changing the folder
    /// or <c>PackagePath</c> back to <c>build</c>) is visible at this level.
    /// </para>
    /// </remarks>
    public class BuildAssetPackagingGateTests
    {
        /// <summary>
        /// The MSBuild targets file must be in this folder to take effect for transitive consumers.
        /// </summary>
        private const string RequiredFolder = "buildTransitive";

        [Fact]
        [DisplayName("Polhem.Definition.targets is in the buildTransitive folder, not build")]
        public void DefinitionTargets_LiveUnderBuildTransitive()
        {
            var projectDir = GetDefinitionProjectDirectory();

            Assert.True(
                File.Exists(Path.Combine(projectDir, RequiredFolder, "Polhem.Definition.targets")),
                $"{RequiredFolder}/Polhem.Definition.targets not found. The targets file must be in " +
                $"{RequiredFolder}/, otherwise only projects that reference Polhem.Definition directly import it.");

            Assert.False(
                Directory.Exists(Path.Combine(projectDir, "build")),
                "src/Polhem.Definition/build/ must not exist. NuGet imports build/ only for a direct PackageReference, " +
                $"while {RequiredFolder}/ applies to both direct and transitive consumers, so a second copy is not needed.");
        }

        [Fact]
        [DisplayName("The csproj packs the targets file to the buildTransitive path")]
        public void DefinitionCsproj_PacksTargetsToBuildTransitive()
        {
            var csproj = File.ReadAllText(
                Path.Combine(GetDefinitionProjectDirectory(), "Polhem.Definition.csproj"));

            Assert.Contains($"PackagePath=\"{RequiredFolder}\\\"", csproj, StringComparison.Ordinal);

            Assert.DoesNotContain("PackagePath=\"build\\\"", csproj, StringComparison.Ordinal);
        }

        /// <summary>
        /// Gets the absolute path of <c>src/Polhem.Definition</c>.
        /// </summary>
        private static string GetDefinitionProjectDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && dir.GetDirectories(".git").Length == 0)
            {
                dir = dir.Parent;
            }

            Assert.True(dir != null, "Could not find the repository root (.git) above the test output directory.");

            var projectDir = Path.Combine(dir!.FullName, "src", "Polhem.Definition");
            Assert.True(Directory.Exists(projectDir), $"{projectDir} not found.");

            return projectDir;
        }
    }
}
