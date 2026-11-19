using System.ComponentModel;
using System.Text.RegularExpressions;
using Polhem.Tests.Shared;

namespace Polhem.Expressions.UnitTests
{
    /// <summary>
    /// The functions the expression rules document lists are the ones <see cref="DynamicExpressoEvaluator.HelperFunctions"/>
    /// registers. The document's descriptions are written by hand; only the names are compared.
    /// </summary>
    public class HelperFunctionDocsTests
    {
        // A table row whose first cell is a call, such as "| `Between(value, low, high)` |" or "| `Today()` |".
        private static readonly Regex s_functionRow = new(@"^[ \t]*\|\s*`(?<name>[A-Za-z_]\w*)\(", RegexOptions.Multiline,
            TimeSpan.FromSeconds(1));

        // The section of the document that lists the functions; the aggregates of a later section are rewritten into
        // variables, not registered as functions.
        private static string FunctionSection()
        {
            var path = Path.Combine(RepoRoot.Find(), "docs", "en", "definitions", "expression-rules.md");
            var text = File.ReadAllText(path);
            var start = text.IndexOf("\n## Expression Syntax", StringComparison.Ordinal);
            Assert.True(start >= 0, "The document has no Expression Syntax section.");
            var end = text.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
            return end < 0 ? text[start..] : text[start..end];
        }

        [Fact]
        [DisplayName("The expression rules document lists every helper function by name, and no function the catalog lacks")]
        public void ExpressionRulesDocument_ListsTheCatalog()
        {
            var documented = s_functionRow.Matches(FunctionSection()).Select(m => m.Groups["name"].Value)
                .ToHashSet(StringComparer.Ordinal);
            var catalog = DynamicExpressoEvaluator.HelperFunctions.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);

            Assert.NotEmpty(documented);
            Assert.Empty(catalog.Except(documented).Order(StringComparer.Ordinal));
            Assert.Empty(documented.Except(catalog).Order(StringComparer.Ordinal));
        }
    }
}
