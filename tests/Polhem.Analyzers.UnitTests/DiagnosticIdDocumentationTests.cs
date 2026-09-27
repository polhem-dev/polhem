using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;
using Polhem.Tests.Shared;

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
        [DisplayName("The analyzer IDs listed in analyzer-rules.md match DiagnosticIds exactly")]
        public void AnalyzerRules_ListExactlyTheAnalyzerIds(string language)
        {
            var root = RepoRoot.Find();
            var documented = IdsIn(File.ReadAllText(Path.Combine(root, "docs", language, "analyzer-rules.md")))
                .Where(id => id[6] != '9')
                .ToHashSet(StringComparer.Ordinal);
            var declared = DeclaredIds();

            // Reserved IDs are documented as reserved, not as rules; the test below covers them.
            documented.ExceptWith(DiagnosticIds.ReservedIds);

            Assert.NotEmpty(declared);
            Assert.Equal(declared.OrderBy(x => x, StringComparer.Ordinal), documented.OrderBy(x => x, StringComparer.Ordinal));
        }

        [Theory]
        [InlineData("en")]
        [InlineData("zh-TW")]
        [DisplayName("Every reserved analyzer ID is named in analyzer-rules.md and none is declared as a rule")]
        public void AnalyzerRules_ReservedIdsAreDocumentedAndUnused(string language)
        {
            var root = RepoRoot.Find();
            var documented = IdsIn(File.ReadAllText(Path.Combine(root, "docs", language, "analyzer-rules.md")))
                .ToHashSet(StringComparer.Ordinal);
            var declared = DeclaredIds();

            Assert.NotEmpty(DiagnosticIds.ReservedIds);
            Assert.All(DiagnosticIds.ReservedIds, id =>
            {
                Assert.Contains(id, documented);
                Assert.DoesNotContain(id, declared);
            });
        }

        private static HashSet<string> DeclaredIds()
            => typeof(DiagnosticIds)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral)
                .Select(f => (string)f.GetRawConstantValue()!)
                .ToHashSet(StringComparer.Ordinal);

        [Theory]
        [InlineData("en")]
        [InlineData("zh-TW")]
        [DisplayName("Every build gate ID listed in analyzer-rules.md is raised by some targets file")]
        public void AnalyzerRules_BuildGateIdsAreRaisedByTargets(string language)
        {
            var root = RepoRoot.Find();
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
    }
}
