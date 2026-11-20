using System.ComponentModel;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Polhem.DefineEditor.Services;
using Polhem.Expressions;
using Polhem.Tests.Shared;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The helper function summaries: what <see cref="FunctionLabels"/> returns, and that the zh-TW resources cover
    /// every function the evaluator registers and still match its English summary.
    /// </summary>
    public class FunctionLabelsTests
    {
        private static ExpressionFunction Today =>
            DynamicExpressoEvaluator.HelperFunctions.Single(function => function.Name == "Today");

        [Fact]
        [DisplayName("Summary returns the zh-TW translation in zh-TW and the English summary in English")]
        public void Summary_ByCulture_TranslatesOrFallsBack()
        {
            Assert.Equal("今天的日期（使用者所在時區）。", FunctionLabels.Summary(Today, CultureInfo.GetCultureInfo("zh-TW")));
            Assert.Equal(Today.Summary, FunctionLabels.Summary(Today, CultureInfo.GetCultureInfo("en")));
        }

        [Fact]
        [DisplayName("Usage gives the signature, then the example")]
        public void Usage_FunctionWithExample_ListsSignatureThenExample()
        {
            var lines = FunctionLabels.Usage(Today).Split(Environment.NewLine);

            Assert.Equal(ExpressionMembers.Signature(Today), lines[0]);
            Assert.EndsWith(Today.Example, lines[1], StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Signature lists each parameter of a helper function by its type and name, then the return type")]
        public void Signature_FunctionWithParameters_ListsTypeAndNameOfEach()
        {
            var function = DynamicExpressoEvaluator.HelperFunctions.First(f => f.Parameters.Count > 0);
            var parameters = string.Join(", ",
                function.Parameters.Select(p => $"{ExpressionMembers.TypeName(p.Type)} {p.Name}"));

            Assert.Equal($"({parameters}) → {ExpressionMembers.TypeName(function.ReturnType)}",
                ExpressionMembers.Signature(function));
            Assert.All(function.Parameters, p => Assert.False(string.IsNullOrEmpty(p.Name)));
        }

        [Fact]
        [DisplayName("Every helper function has a zh-TW summary made from its current English summary")]
        public void ZhTwResources_CoverEveryHelperFunction()
        {
            var entries = ZhTwEntries();
            var problems = new List<string>();
            foreach (var function in DynamicExpressoEvaluator.HelperFunctions)
            {
                var key = FunctionLabels.SummaryKey(function);
                if (!entries.TryGetValue(key, out var entry))
                    problems.Add($"{key}: missing");
                else if (!string.Equals(entry.Comment, function.Summary, StringComparison.Ordinal))
                    problems.Add($"{key}: translated from \"{entry.Comment}\", but the summary is now \"{function.Summary}\"");
            }

            Assert.NotEmpty(DynamicExpressoEvaluator.HelperFunctions);
            Assert.True(problems.Count == 0,
                "Translate these in tools/DefineEditor/Resources/FunctionText.zh-TW.resx, with the English summary as the "
                + "<comment>:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
        }

        [Fact]
        [DisplayName("Every zh-TW function summary belongs to a helper function that still exists")]
        public void ZhTwResources_HaveNoOrphans()
        {
            var keys = DynamicExpressoEvaluator.HelperFunctions.Select(FunctionLabels.SummaryKey).ToHashSet(StringComparer.Ordinal);
            var orphans = ZhTwEntries().Keys.Where(key => !keys.Contains(key)).ToList();

            Assert.True(orphans.Count == 0, "Remove these entries: " + string.Join(", ", orphans));
        }

        private static Dictionary<string, (string Value, string? Comment)> ZhTwEntries()
        {
            var path = Path.Combine(RepoRoot.Find(), "tools", "DefineEditor", "Resources", "FunctionText.zh-TW.resx");
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(path, settings);
            return XDocument.Load(reader).Root!.Elements("data").ToDictionary(
                d => (string)d.Attribute("name")!,
                d => ((string)d.Element("value")!, (string?)d.Element("comment")),
                StringComparer.Ordinal);
        }
    }
}
