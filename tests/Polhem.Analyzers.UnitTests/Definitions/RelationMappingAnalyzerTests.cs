using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Definitions;
using Microsoft.CodeAnalysis;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Tests for POLHEM1005 (a relation mapping must point to a declared field) and POLHEM1006 (a relation field should be written by a mapping).
    /// </summary>
    public class RelationMappingAnalyzerTests
    {
        private const string SchemaPath = "Define/FormSchema/Product.FormSchema.xml";

        /// <summary>
        /// A correctly wired relation field: a mapping writes ref_supplier_id, and that field is declared as a RelationField.
        /// </summary>
        private const string WellFormed = """
            <?xml version="1.0" encoding="utf-8"?>
            <FormSchema ProgId="Product" CategoryId="company">
              <Tables>
                <FormTable TableName="Product" DbTableName="ft_product">
                  <Fields>
                    <FormField FieldName="supplier_rowid" DbType="Guid" RelationProgId="Supplier">
                      <RelationFieldMappings>
                        <FieldMapping SourceField="sys_id" DestinationField="ref_supplier_id" />
                      </RelationFieldMappings>
                    </FormField>
                    <FormField FieldName="ref_supplier_id" DbType="String" Type="RelationField" />
                  </Fields>
                </FormTable>
              </Tables>
            </FormSchema>
            """;

        [Fact]
        [DisplayName("A DestinationField pointing to an undeclared field reports POLHEM1005")]
        public void UnknownDestinationField_ReportsDiagnostic()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_product">
                      <Fields>
                        <FormField FieldName="supplier_rowid" DbType="Guid" RelationProgId="Supplier">
                          <RelationFieldMappings>
                            <FieldMapping SourceField="sys_id" DestinationField="ref_supplier_code" />
                          </RelationFieldMappings>
                        </FormField>
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new RelationMappingAnalyzer(), (SchemaPath, xml));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM1005", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Contains("'ref_supplier_code'", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A RelationField that no mapping writes to reports POLHEM1006")]
        public void UnmappedRelationField_ReportsDiagnostic()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_product">
                      <Fields>
                        <FormField FieldName="ref_supplier_name" DbType="String" Type="RelationField" />
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new RelationMappingAnalyzer(), (SchemaPath, xml));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM1006", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("'ref_supplier_name'", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A correctly wired relation field reports nothing")]
        public void WellFormedRelation_ReportsNothing()
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(new RelationMappingAnalyzer(), (SchemaPath, WellFormed));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A field that is not a RelationField is not subject to POLHEM1006")]
        public void NonRelationField_IsNotRequiredToBeMapped()
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
            var diagnostics = AnalyzerRunner.Run(new RelationMappingAnalyzer(), (SchemaPath, xml));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A missing mapping target does not also report POLHEM1006 for the same field")]
        public void MissingDestination_DoesNotAlsoReportUnmapped()
        {
            // `ref_supplier_id` is not declared, so only POLHEM1005 is reported, not a second report that the declared RelationField is never written.
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_product">
                      <Fields>
                        <FormField FieldName="supplier_rowid" DbType="Guid" RelationProgId="Supplier">
                          <RelationFieldMappings>
                            <FieldMapping SourceField="sys_id" DestinationField="ref_supplier_id" />
                          </RelationFieldMappings>
                        </FormField>
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new RelationMappingAnalyzer(), (SchemaPath, xml));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM1005", diagnostic.Id);
        }
    }
}
