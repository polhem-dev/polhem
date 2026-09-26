using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Definitions;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Tests for POLHEM1003 (a field DbType must be a member of the framework enum).
    /// </summary>
    public class FieldDbTypeAnalyzerTests
    {
        private const string SchemaPath = "Define/FormSchema/Product.FormSchema.xml";
        private const string TablePath = "Define/TableSchema/company/ft_product.TableSchema.xml";

        [Fact]
        [DisplayName("An unknown FormField DbType reports POLHEM1003")]
        public void UnknownFormFieldDbType_ReportsDiagnostic()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_product">
                      <Fields>
                        <FormField FieldName="unit_price" DbType="Money" />
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new FieldDbTypeAnalyzer(), (SchemaPath, xml));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM1003", diagnostic.Id);

            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("'unit_price'", message, StringComparison.Ordinal);
            Assert.Contains("'Money'", message, StringComparison.Ordinal);
            Assert.Contains("Currency", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A TableSchema DbField is checked too")]
        public void UnknownTableSchemaDbType_ReportsDiagnostic()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <TableSchema TableName="ft_product">
                  <Fields>
                    <DbField FieldName="sys_id" DbType="Varchar" />
                  </Fields>
                </TableSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new FieldDbTypeAnalyzer(), (TablePath, xml));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM1003", diagnostic.Id);
            Assert.Contains("'Varchar'", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("String")]
        [InlineData("Currency")]
        [InlineData("AutoIncrement")]
        [InlineData("Time")]
        [DisplayName("A valid DbType reports nothing")]
        public void ValidDbType_ReportsNothing(string dbType)
        {
            var xml = $"""
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_product">
                      <Fields>
                        <FormField FieldName="sample" DbType="{dbType}" />
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new FieldDbTypeAnalyzer(), (SchemaPath, xml));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A casing-only mismatch names the correct spelling")]
        public void WrongCasing_NamesCorrectCasing()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_product">
                      <Fields>
                        <FormField FieldName="sys_id" DbType="string" />
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new FieldDbTypeAnalyzer(), (SchemaPath, xml));

            // Assert
            var message = Assert.Single(diagnostics).GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("case-sensitive", message, StringComparison.Ordinal);
            Assert.Contains("change it to 'String'", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A field without a DbType attribute reports nothing")]
        public void MissingDbType_ReportsNothing()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_product">
                      <Fields>
                        <FormField FieldName="sys_id" />
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new FieldDbTypeAnalyzer(), (SchemaPath, xml));

            // Assert
            Assert.Empty(diagnostics);
        }
    }
}
