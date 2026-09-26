using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Definitions;
using Microsoft.CodeAnalysis;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Tests for POLHEM1001 (a FormSchema CategoryId must be a valid database scope).
    /// </summary>
    public class FormSchemaCategoryIdAnalyzerTests
    {
        private const string SchemaPath = "Define/FormSchema/Product.FormSchema.xml";

        [Fact]
        [DisplayName("An unknown CategoryId reports POLHEM1001 and lists the valid values")]
        public void UnknownCategoryId_ReportsDiagnostic()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="business">
                  <Tables />
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new FormSchemaCategoryIdAnalyzer(), (SchemaPath, xml));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM1001", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("'Product'", message, StringComparison.Ordinal);
            Assert.Contains("'business'", message, StringComparison.Ordinal);
            Assert.Contains("'common', 'company', 'log'", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A CategoryId that differs only in casing reports POLHEM1001 and names the correct spelling")]
        public void WrongCasingCategoryId_ReportsDiagnosticNamingCorrectCasing()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="Company">
                  <Tables />
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new FormSchemaCategoryIdAnalyzer(), (SchemaPath, xml));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM1001", diagnostic.Id);

            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("ordinal", message, StringComparison.Ordinal);
            Assert.Contains("change it to 'company'", message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("common")]
        [InlineData("company")]
        [InlineData("log")]
        [DisplayName("A CategoryId that is a valid scope reports nothing")]
        public void ValidCategoryId_ReportsNothing(string categoryId)
        {
            var xml = $"""
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="{categoryId}">
                  <Tables />
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new FormSchemaCategoryIdAnalyzer(), (SchemaPath, xml));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("The diagnostic location points at the CategoryId attribute itself")]
        public void Diagnostic_LocatesCategoryIdAttribute()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="business">
                  <Tables />
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new FormSchemaCategoryIdAnalyzer(), (SchemaPath, xml));

            // Assert
            var lineSpan = Assert.Single(diagnostics).Location.GetLineSpan();
            Assert.Equal(SchemaPath, lineSpan.Path);

            // Line 2 (0-based index 1) is the FormSchema root element.
            Assert.Equal(1, lineSpan.StartLinePosition.Line);

            // The location covers the whole `CategoryId="business"`, not only the attribute name.
            var line = xml.Split('\n')[1];
            var expectedStart = line.IndexOf("CategoryId", StringComparison.Ordinal);
            Assert.Equal(expectedStart, lineSpan.StartLinePosition.Character);
            Assert.Equal(expectedStart + "CategoryId=\"business\"".Length, lineSpan.EndLinePosition.Character);
        }

        [Fact]
        [DisplayName("A schema without a CategoryId attribute reports nothing")]
        public void MissingCategoryId_ReportsNothing()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product">
                  <Tables />
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new FormSchemaCategoryIdAnalyzer(), (SchemaPath, xml));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A definition file that is not a FormSchema is not checked")]
        public void NonFormSchemaFile_ReportsNothing()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <TableSchema CategoryId="business" />
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new FormSchemaCategoryIdAnalyzer(),
                ("Define/TableSchema/company/ft_product.TableSchema.xml", xml));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("Malformed XML is skipped silently instead of crashing the analyzer")]
        public void MalformedXml_ReportsNothingWithoutThrowing()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="business"
                """;

            // Act
            var exception = Record.Exception(
                () => AnalyzerRunner.Run(new FormSchemaCategoryIdAnalyzer(), (SchemaPath, xml)));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("A missing ProgId attribute falls back to a ProgId derived from the file name")]
        public void MissingProgId_FallsBackToFileName()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema CategoryId="business">
                  <Tables />
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new FormSchemaCategoryIdAnalyzer(), (SchemaPath, xml));

            // Assert
            var message = Assert.Single(diagnostics).GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("'Product'", message, StringComparison.Ordinal);
        }
    }
}
