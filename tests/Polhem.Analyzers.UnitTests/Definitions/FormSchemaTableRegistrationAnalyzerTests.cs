using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Definitions;
using Microsoft.CodeAnalysis;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Tests for POLHEM2001 (a FormSchema table must be registered under the declared scope).
    /// </summary>
    public class FormSchemaTableRegistrationAnalyzerTests
    {
        private const string SchemaPath = "Define/FormSchema/Product.FormSchema.xml";
        private const string SettingsPath = "Define/DbCategorySettings.xml";

        private const string Settings = """
            <?xml version="1.0" encoding="utf-8"?>
            <DbCategorySettings>
              <Categories>
                <DbCategory Id="common" DisplayName="共用資料庫">
                  <Tables>
                    <TableItem TableName="st_user" />
                  </Tables>
                </DbCategory>
                <DbCategory Id="company" DisplayName="公司資料庫">
                  <Tables>
                    <TableItem TableName="ft_product" />
                    <TableItem TableName="ft_order" />
                    <TableItem TableName="ft_order_detail" />
                  </Tables>
                </DbCategory>
              </Categories>
            </DbCategorySettings>
            """;

        [Fact]
        [DisplayName("A table registered under another scope reports POLHEM2001 and names the actual scope")]
        public void TableRegisteredUnderAnotherScope_ReportsDiagnosticNamingActualScope()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="common">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_product" />
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new FormSchemaTableRegistrationAnalyzer(),
                (SchemaPath, xml),
                (SettingsPath, Settings));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM2001", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);

            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("'ft_product'", message, StringComparison.Ordinal);
            Assert.Contains("It is registered under 'company'", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A table registered nowhere reports POLHEM2001 and suggests adding a TableItem")]
        public void TableNotRegisteredAnywhere_ReportsDiagnosticSuggestingRegistration()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Invoice" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Invoice" DbTableName="ft_invoice" />
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new FormSchemaTableRegistrationAnalyzer(),
                ("Define/FormSchema/Invoice.FormSchema.xml", xml),
                (SettingsPath, Settings));

            // Assert
            var message = Assert.Single(diagnostics).GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("add a TableItem for 'ft_invoice'", message, StringComparison.Ordinal);
            Assert.Contains("'company'", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A table registered under the declared scope reports nothing")]
        public void TableRegisteredUnderDeclaredScope_ReportsNothing()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_product" />
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new FormSchemaTableRegistrationAnalyzer(),
                (SchemaPath, xml),
                (SettingsPath, Settings));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("Each FormTable of a master-detail schema is checked")]
        public void MultipleFormTables_ChecksEach()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Order" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Order" DbTableName="ft_order" />
                    <FormTable TableName="OrderDetail" DbTableName="ft_order_detail" />
                    <FormTable TableName="OrderMemo" DbTableName="ft_order_memo" />
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new FormSchemaTableRegistrationAnalyzer(),
                ("Define/FormSchema/Order.FormSchema.xml", xml),
                (SettingsPath, Settings));

            // Assert
            var message = Assert.Single(diagnostics).GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("'ft_order_memo'", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Without DbCategorySettings.xml the whole rule stays silent (definitions may live in the database)")]
        public void MissingDbCategorySettings_ReportsNothing()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Invoice" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Invoice" DbTableName="ft_invoice" />
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new FormSchemaTableRegistrationAnalyzer(),
                ("Define/FormSchema/Invoice.FormSchema.xml", xml));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("An invalid CategoryId is left to POLHEM1001 and not reported twice")]
        public void InvalidCategoryId_DefersToPolhem1001()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="business">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_product" />
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new FormSchemaTableRegistrationAnalyzer(),
                (SchemaPath, xml),
                (SettingsPath, Settings));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("The diagnostic location points at the DbTableName attribute")]
        public void Diagnostic_LocatesDbTableNameAttribute()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="common">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_product" />
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new FormSchemaTableRegistrationAnalyzer(),
                (SchemaPath, xml),
                (SettingsPath, Settings));

            // Assert
            var lineSpan = Assert.Single(diagnostics).Location.GetLineSpan();
            Assert.Equal(SchemaPath, lineSpan.Path);

            // Line 4 (0-based index 3) is the FormTable element.
            Assert.Equal(3, lineSpan.StartLinePosition.Line);

            var line = xml.Split('\n')[3];
            var expectedStart = line.IndexOf("DbTableName", StringComparison.Ordinal);
            Assert.Equal(expectedStart, lineSpan.StartLinePosition.Character);
        }

        [Fact]
        [DisplayName("A table name casing mismatch reports nothing, so a misdiagnosis does not hide the real cause")]
        public void TableNameCasingMismatch_ReportsNothing()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="FT_PRODUCT" />
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new FormSchemaTableRegistrationAnalyzer(),
                (SchemaPath, xml),
                (SettingsPath, Settings));

            // Assert
            Assert.Empty(diagnostics);
        }
    }
}
