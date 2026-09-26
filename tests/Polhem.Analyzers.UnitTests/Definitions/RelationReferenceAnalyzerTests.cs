using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Definitions;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Tests for POLHEM2003 (a RelationProgId must exist) and POLHEM2004 (a SourceField must be a field of the referenced schema).
    /// </summary>
    public class RelationReferenceAnalyzerTests
    {
        private const string ProductPath = "Define/FormSchema/Product.FormSchema.xml";
        private const string SupplierPath = "Define/FormSchema/Supplier.FormSchema.xml";

        private const string Supplier = """
            <?xml version="1.0" encoding="utf-8"?>
            <FormSchema ProgId="Supplier" CategoryId="company">
              <Tables>
                <FormTable TableName="Supplier" DbTableName="ft_supplier">
                  <Fields>
                    <FormField FieldName="sys_id" DbType="String" />
                    <FormField FieldName="sys_name" DbType="String" />
                  </Fields>
                </FormTable>
              </Tables>
            </FormSchema>
            """;

        private static string ProductReferencing(string relationProgId, string sourceField) => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <FormSchema ProgId="Product" CategoryId="company">
              <Tables>
                <FormTable TableName="Product" DbTableName="ft_product">
                  <Fields>
                    <FormField FieldName="supplier_rowid" DbType="Guid" RelationProgId="{relationProgId}">
                      <RelationFieldMappings>
                        <FieldMapping SourceField="{sourceField}" DestinationField="ref_supplier_name" />
                      </RelationFieldMappings>
                    </FormField>
                    <FormField FieldName="ref_supplier_name" DbType="String" Type="RelationField" />
                  </Fields>
                </FormTable>
              </Tables>
            </FormSchema>
            """;

        [Fact]
        [DisplayName("A RelationProgId pointing to a missing schema reports POLHEM2003")]
        public void UnknownRelationProgId_ReportsDiagnostic()
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new RelationReferenceAnalyzer(),
                (ProductPath, ProductReferencing("Vendor", "sys_name")),
                (SupplierPath, Supplier));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM2003", diagnostic.Id);

            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("'Vendor'", message, StringComparison.Ordinal);
            Assert.Contains("'supplier_rowid'", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A SourceField missing from the referenced schema reports POLHEM2004")]
        public void UnknownSourceField_ReportsDiagnostic()
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new RelationReferenceAnalyzer(),
                (ProductPath, ProductReferencing("Supplier", "sys_title")),
                (SupplierPath, Supplier));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM2004", diagnostic.Id);

            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("'sys_title'", message, StringComparison.Ordinal);
            Assert.Contains("'Supplier'", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A valid relation and source field report nothing")]
        public void ValidRelation_ReportsNothing()
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new RelationReferenceAnalyzer(),
                (ProductPath, ProductReferencing("Supplier", "sys_name")),
                (SupplierPath, Supplier));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Theory]
        [InlineData("Employee")]
        [InlineData("Department")]
        [DisplayName("Referring to a built-in framework schema is not falsely reported (built-ins are embedded resources, not consumer files)")]
        public void FrameworkSuppliedProgId_ReportsNothing(string progId)
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new RelationReferenceAnalyzer(),
                (ProductPath, ProductReferencing(progId, "sys_name")),
                (SupplierPath, Supplier));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A missing ProgId does not also report POLHEM2004 (the source field cannot be judged)")]
        public void UnknownProgId_DoesNotAlsoReportSourceField()
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new RelationReferenceAnalyzer(),
                (ProductPath, ProductReferencing("Vendor", "does_not_exist")),
                (SupplierPath, Supplier));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM2003", diagnostic.Id);
        }

        [Fact]
        [DisplayName("A field without a RelationProgId is not checked")]
        public void FieldWithoutRelation_ReportsNothing()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_product">
                      <Fields>
                        <FormField FieldName="sys_id" DbType="String" />
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new RelationReferenceAnalyzer(), (ProductPath, xml));

            // Assert
            Assert.Empty(diagnostics);
        }
    }
}
