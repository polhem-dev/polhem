using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Polhem.Analyzers.UnitTests
{
    /// <summary>
    /// Asserts that the published rule reference and the code agree on the diagnostic IDs.
    /// </summary>
    /// <remarks>
    /// Consumers configure severities by ID in their own <c>.editorconfig</c>, copying the IDs from
    /// <c>docs/*/analyzer-rules.md</c>. An ID that is documented but not reported, or the reverse, makes
    /// such a setting silently do nothing, and nothing in the build reads the documentation.
    /// </remarks>
    public class DiagnosticIdDocumentationTests
    {
        private static readonly Regex s_id = new("POLHEM[0-9]{4}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        [Theory]
        [InlineData("en")]
        [InlineData("zh-TW")]
        [DisplayName("analyzer-rules.md 列出的分析器代號與 DiagnosticIds 完全一致")]
        public void AnalyzerRules_ListExactlyTheAnalyzerIds(string language)
        {
            var root = FindRepositoryRoot();
            var documented = IdsIn(File.ReadAllText(Path.Combine(root, "docs", language, "analyzer-rules.md")))
                .Where(id => id[6] != '9')
                .ToHashSet(StringComparer.Ordinal);
            var declared = typeof(DiagnosticIds)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral)
                .Select(f => (string)f.GetRawConstantValue()!)
                .ToHashSet(StringComparer.Ordinal);

            Assert.NotEmpty(declared);
            Assert.Equal(declared.OrderBy(x => x, StringComparer.Ordinal), documented.OrderBy(x => x, StringComparer.Ordinal));
        }

        [Theory]
        [InlineData("en")]
        [InlineData("zh-TW")]
        [DisplayName("analyzer-rules.md 列出的建置閘門代號都由某個 targets 檔報出")]
        public void AnalyzerRules_BuildGateIdsAreRaisedByTargets(string language)
        {
            var root = FindRepositoryRoot();
            var documented = IdsIn(File.ReadAllText(Path.Combine(root, "docs", language, "analyzer-rules.md")))
                .Where(id => id[6] == '9')
                .ToList();
            var targets = string.Concat(
                Directory.EnumerateFiles(root, "*.targets", SearchOption.AllDirectories)
                    .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                             && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    .Select(File.ReadAllText));

            Assert.NotEmpty(documented);
            Assert.All(documented, id => Assert.Contains("Code=\"" + id + "\"", targets, StringComparison.Ordinal));
        }

        private static IEnumerable<string> IdsIn(string text)
            => s_id.Matches(text).Select(m => m.Value).Distinct(StringComparer.Ordinal);

        private static string FindRepositoryRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && dir.GetDirectories(".git").Length == 0)
            {
                dir = dir.Parent;
            }
            Assert.True(dir != null, "No repository root (.git) above the test output directory.");
            return dir!.FullName;
        }
    }
}
