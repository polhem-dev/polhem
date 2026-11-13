using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Definitions;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Tests for POLHEM2008 (a relation field must declare the type of the source field it is mapped from).
    /// </summary>
    public class RelationFieldTypeAnalyzerTests
    {
        private const string ProductPath = "Define/FormSchema/Product.FormSchema.xml";
        private const string SupplierPath = "Define/FormSchema/Supplier.FormSchema.xml";

        private const string Supplier = """
            <?xml version="1.0" encoding="utf-8"?>
            <FormSchema ProgId="Supplier" CategoryId="company">
              <Tables>
                <FormTable TableName="Supplier" DbTableName="ft_supplier">
                  <Fields>
                    <FormField FieldName="sys_rowid" DbType="Guid" />
                    <FormField FieldName="sys_id" DbType="String" />
                    <FormField FieldName="sys_name" />
                    <FormField FieldName="seq" DbType="AutoIncrement" />
                    <FormField FieldName="credit" DbType="Currency" />
                  </Fields>
                </FormTable>
                <FormTable TableName="Contact" DbTableName="ft_supplier_contact">
                  <Fields>
                    <FormField FieldName="phone" DbType="Integer" />
                  </Fields>
                </FormTable>
              </Tables>
            </FormSchema>
            """;

        private static string ProductMapping(string relationProgId, string sourceField, string destinationDbType) => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <FormSchema ProgId="Product" CategoryId="company">
              <Tables>
                <FormTable TableName="Product" DbTableName="ft_product">
                  <Fields>
                    <FormField FieldName="supplier_rowid" DbType="Guid" RelationProgId="{relationProgId}">
                      <RelationFieldMappings>
                        <FieldMapping SourceField="{sourceField}" DestinationField="ref_supplier_value" />
                      </RelationFieldMappings>
                    </FormField>
                    <FormField FieldName="ref_supplier_value" {destinationDbType} Type="RelationField" />
                  </Fields>
                </FormTable>
              </Tables>
            </FormSchema>
            """;

        [Fact]
        [DisplayName("A Guid source field mapped into a String relation field reports POLHEM2008")]
        public void GuidSourceDeclaredAsString_ReportsDiagnostic()
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new RelationFieldTypeAnalyzer(),
                (ProductPath, ProductMapping("Supplier", "sys_rowid", "DbType=\"String\"")),
                (SupplierPath, Supplier));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM2008", diagnostic.Id);
            Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, diagnostic.Severity);

            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("'ref_supplier_value'", message, StringComparison.Ordinal);
            Assert.Contains("'sys_rowid'", message, StringComparison.Ordinal);
            Assert.Contains("DbType=\"Guid\"", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A relation field without a DbType counts as String and is checked against its source")]
        public void AbsentDestinationDbType_IsTreatedAsString()
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new RelationFieldTypeAnalyzer(),
                (ProductPath, ProductMapping("Supplier", "sys_rowid", string.Empty)),
                (SupplierPath, Supplier));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Contains("'String'", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("sys_rowid", "DbType=\"Guid\"")]
        [InlineData("sys_id", "DbType=\"String\"")]
        [InlineData("sys_id", "DbType=\"Text\"")]
        [InlineData("sys_name", "DbType=\"String\"")]
        [InlineData("seq", "DbType=\"Integer\"")]
        [InlineData("credit", "DbType=\"Decimal\"")]
        [DisplayName("A relation field whose type holds values the same way as its source reports nothing")]
        public void MatchingType_ReportsNothing(string sourceField, string destinationDbType)
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new RelationFieldTypeAnalyzer(),
                (ProductPath, ProductMapping("Supplier", sourceField, destinationDbType)),
                (SupplierPath, Supplier));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Theory]
        [InlineData("Vendor", "sys_rowid")]
        [InlineData("Supplier", "sys_title")]
        [InlineData("Supplier", "phone")]
        [InlineData("Employee", "sys_rowid")]
        [DisplayName("A source that cannot be resolved is left to the rules that report it")]
        public void UnresolvableSource_ReportsNothing(string relationProgId, string sourceField)
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new RelationFieldTypeAnalyzer(),
                (ProductPath, ProductMapping(relationProgId, sourceField, "DbType=\"String\"")),
                (SupplierPath, Supplier));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A destination field that does not exist is left to POLHEM1005")]
        public void UnknownDestination_ReportsNothing()
        {
            var product = ProductMapping("Supplier", "sys_rowid", "DbType=\"String\"")
                .Replace("FieldName=\"ref_supplier_value\"", "FieldName=\"ref_other\"", StringComparison.Ordinal);

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new RelationFieldTypeAnalyzer(),
                (ProductPath, product),
                (SupplierPath, Supplier));

            // Assert
            Assert.Empty(diagnostics);
        }
    }
}
