using System.ComponentModel;
using Polhem.Tests.Shared;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Every packable project under <c>src/</c> is in the project list of <c>.github/workflows/nuget-publish.yml</c>, which
    /// publishes the packages, and in the pack step of <c>.github/workflows/build-ci.yml</c>, which checks that they pack.
    /// </summary>
    /// <remarks>
    /// Both lists are written by hand, and nothing else notices a project missing from them: the build and the tests pass,
    /// and the release simply does not publish the package. <c>Polhem.Definition.Drafting</c> and <c>Polhem.AI.Claude</c>
    /// were added in 1.14.0 development without reaching either list, and <c>Polhem.Api.Server</c> was never in the CI
    /// pack step. A project marked <c>IsPackable</c> false is not expected in either list.
    /// </remarks>
    public class PublishedPackageListGateTests
    {
        [Fact]
        [DisplayName("Every packable src project is published by nuget-publish.yml and packed by build-ci.yml")]
        public void PackableProjects_AreInPublishAndPackLists()
        {
            var root = RepoRoot.Find();
            var publish = File.ReadAllText(Path.Combine(root, ".github", "workflows", "nuget-publish.yml"));
            var ci = File.ReadAllText(Path.Combine(root, ".github", "workflows", "build-ci.yml"));

            var checkedProjects = new List<string>();
            var missing = new List<string>();
            foreach (var project in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories))
            {
                if (File.ReadAllText(project).Contains("<IsPackable>false</IsPackable>", StringComparison.OrdinalIgnoreCase))
                    continue;
                var relative = Path.GetRelativePath(root, project).Replace('\\', '/');
                checkedProjects.Add(relative);
                if (!publish.Contains($"'{relative}'", StringComparison.Ordinal))
                    missing.Add($"{relative} is not in nuget-publish.yml");
                if (!ci.Contains($"dotnet pack {relative} ", StringComparison.Ordinal))
                    missing.Add($"{relative} is not packed in build-ci.yml");
            }

            Assert.Contains("src/Polhem.Core/Polhem.Core.csproj", checkedProjects);
            Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
        }
    }
}
