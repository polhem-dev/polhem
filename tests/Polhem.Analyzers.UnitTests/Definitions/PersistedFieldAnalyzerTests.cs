using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Definitions;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Tests for POLHEM2006 (a persisted field must exist in the matching TableSchema).
    /// </summary>
    public class PersistedFieldAnalyzerTests
    {
        private const string SchemaPath = "Define/FormSchema/Product.FormSchema.xml";
        private const string TablePath = "Define/TableSchema/company/ft_product.TableSchema.xml";

        private const string TableSchema = """
            <?xml version="1.0" encoding="utf-8"?>
            <TableSchema TableName="ft_product">
              <Fields>
                <DbField FieldName="sys_id" DbType="String" />
                <DbField FieldName="sys_name" DbType="String" />
                <DbField FieldName="supplier_rowid" DbType="Guid" />
              </Fields>
            </TableSchema>
            """;

        [Fact]
        [DisplayName("A persisted field missing from the TableSchema reports POLHEM2006")]
        public void PersistedFieldMissingColumn_ReportsDiagnostic()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_product">
                      <Fields>
                        <FormField FieldName="sys_id" DbType="String" />
                        <FormField FieldName="unit_price" DbType="Currency" />
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new PersistedFieldAnalyzer(),
                (SchemaPath, xml),
                (TablePath, TableSchema));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM2006", diagnostic.Id);

            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("'unit_price'", message, StringComparison.Ordinal);
            Assert.Contains("'ft_product'", message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("RelationField")]
        [InlineData("VirtualField")]
        [DisplayName("A non-persisted field need not exist in the TableSchema")]
        public void NonPersistedField_ReportsNothing(string fieldType)
        {
            var xml = $"""
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_product">
                      <Fields>
                        <FormField FieldName="sys_id" DbType="String" />
                        <FormField FieldName="ref_supplier_name" DbType="String" Type="{fieldType}" />
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new PersistedFieldAnalyzer(),
                (SchemaPath, xml),
                (TablePath, TableSchema));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A field without a Type is treated as persisted and still needs a matching column")]
        public void MissingTypeAttribute_IsTreatedAsPersisted()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_product">
                      <Fields>
                        <FormField FieldName="not_a_column" DbType="String" />
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new PersistedFieldAnalyzer(),
                (SchemaPath, xml),
                (TablePath, TableSchema));

            // Assert
            Assert.Single(diagnostics);
        }

        [Fact]
        [DisplayName("All fields present reports nothing")]
        public void AllFieldsPresent_ReportsNothing()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_product">
                      <Fields>
                        <FormField FieldName="sys_id" DbType="String" />
                        <FormField FieldName="sys_name" DbType="String" />
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new PersistedFieldAnalyzer(),
                (SchemaPath, xml),
                (TablePath, TableSchema));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A missing TableSchema is left to POLHEM2002 instead of reporting each field")]
        public void MissingTableSchema_DefersToPolhem2002()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_unknown">
                      <Fields>
                        <FormField FieldName="sys_id" DbType="String" />
                        <FormField FieldName="sys_name" DbType="String" />
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new PersistedFieldAnalyzer(),
                (SchemaPath, xml),
                (TablePath, TableSchema));

            // Assert
            Assert.Empty(diagnostics);
        }
    }
}
