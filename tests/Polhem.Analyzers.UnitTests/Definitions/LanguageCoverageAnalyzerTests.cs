using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Definitions;
using Microsoft.CodeAnalysis;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Tests for POLHEM2007 (every language should cover the same translation keys).
    /// </summary>
    public class LanguageCoverageAnalyzerTests
    {
        private const string ChinesePath = "Define/Language/zh-TW/Product.Language.xml";
        private const string EnglishPath = "Define/Language/en-US/Product.Language.xml";

        private static string Resource(string culture, params string[] keys)
        {
            var items = string.Join(
                "\n    ",
                keys.Select(key => $"<LanguageItem Key=\"{key}\" Value=\"text\" />"));

            return $"""
                <?xml version="1.0" encoding="utf-8"?>
                <LanguageResource Namespace="Product" Lang="{culture}">
                  <Items>
                    {items}
                  </Items>
                </LanguageResource>
                """;
        }

        [Fact]
        [DisplayName("A language missing a key that another language has reports POLHEM2007")]
        public void MissingKeys_ReportsDiagnostic()
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new LanguageCoverageAnalyzer(),
                (ChinesePath, Resource("zh-TW", "Schema.DisplayName", "Field.sys_id.Caption", "Field.sys_name.Caption")),
                (EnglishPath, Resource("en-US", "Schema.DisplayName")));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM2007", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);

            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("'en-US'", message, StringComparison.Ordinal);
            Assert.Contains("missing 2 key(s)", message, StringComparison.Ordinal);
            Assert.Contains("Field.sys_id.Caption", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Languages with the same keys report nothing")]
        public void ConsistentCoverage_ReportsNothing()
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new LanguageCoverageAnalyzer(),
                (ChinesePath, Resource("zh-TW", "Schema.DisplayName", "Field.sys_id.Caption")),
                (EnglishPath, Resource("en-US", "Schema.DisplayName", "Field.sys_id.Caption")));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A single language has nothing to compare with and reports nothing")]
        public void SingleCulture_ReportsNothing()
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new LanguageCoverageAnalyzer(),
                (ChinesePath, Resource("zh-TW", "Schema.DisplayName", "Field.sys_id.Caption")));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("Language files of different namespaces are compared separately")]
        public void DifferentNamespaces_AreComparedSeparately()
        {
            const string orderResource = """
                <?xml version="1.0" encoding="utf-8"?>
                <LanguageResource Namespace="Order" Lang="zh-TW">
                  <Items>
                    <LanguageItem Key="Order.Only.Key" Value="text" />
                  </Items>
                </LanguageResource>
                """;

            // Act: Product has the same keys in both languages, and Order has only one language, so Order must not be reported as missing Product's keys.
            var diagnostics = AnalyzerRunner.Run(
                new LanguageCoverageAnalyzer(),
                (ChinesePath, Resource("zh-TW", "Schema.DisplayName")),
                (EnglishPath, Resource("en-US", "Schema.DisplayName")),
                ("Define/Language/zh-TW/Order.Language.xml", orderResource));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("Many missing keys are summarized instead of listing every key")]
        public void ManyMissingKeys_AreSummarised()
        {
            var manyKeys = Enumerable.Range(1, 10).Select(index => $"Field.f{index}.Caption").ToArray();

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new LanguageCoverageAnalyzer(),
                (ChinesePath, Resource("zh-TW", manyKeys)),
                (EnglishPath, Resource("en-US", "Field.f1.Caption")));

            // Assert
            var message = Assert.Single(diagnostics).GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("missing 9 key(s)", message, StringComparison.Ordinal);
            Assert.Contains("and 6 more", message, StringComparison.Ordinal);
        }
    }
}
