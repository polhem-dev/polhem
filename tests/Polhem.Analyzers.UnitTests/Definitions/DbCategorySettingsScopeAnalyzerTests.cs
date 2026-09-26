using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Definitions;
using Microsoft.CodeAnalysis;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Tests for POLHEM1002 (a DbCategory Id must be a valid database scope).
    /// </summary>
    public class DbCategorySettingsScopeAnalyzerTests
    {
        private const string SettingsPath = "Define/DbCategorySettings.xml";

        [Fact]
        [DisplayName("An unknown DbCategory Id reports POLHEM1002")]
        public void UnknownCategoryId_ReportsDiagnostic()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <DbCategorySettings>
                  <Categories>
                    <DbCategory Id="archive" DisplayName="Archive">
                      <Tables />
                    </DbCategory>
                  </Categories>
                </DbCategorySettings>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new DbCategorySettingsScopeAnalyzer(), (SettingsPath, xml));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM1002", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("'archive'", message, StringComparison.Ordinal);
            Assert.Contains("'common', 'company', 'log'", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("None of the valid scopes reports a diagnostic")]
        public void AllValidScopes_ReportNothing()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <DbCategorySettings>
                  <Categories>
                    <DbCategory Id="common" />
                    <DbCategory Id="company" />
                    <DbCategory Id="log" />
                  </Categories>
                </DbCategorySettings>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new DbCategorySettingsScopeAnalyzer(), (SettingsPath, xml));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A casing-only mismatch names the correct spelling")]
        public void WrongCasing_NamesCorrectCasing()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <DbCategorySettings>
                  <Categories>
                    <DbCategory Id="Log" />
                  </Categories>
                </DbCategorySettings>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new DbCategorySettingsScopeAnalyzer(), (SettingsPath, xml));

            // Assert
            var message = Assert.Single(diagnostics).GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("change it to 'log'", message, StringComparison.Ordinal);
        }
    }
}
