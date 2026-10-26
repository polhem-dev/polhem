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

        /// <summary>
        /// A leave request whose employee lookup also copies the department: `dept_rowid` is a stored field and
        /// `ref_dept_name` a relation field, both written by lookup-only mappings. The department foreign key may or
        /// may not join `ref_dept_name` itself.
        /// </summary>
        private static string LeaveSchema(string deptMappings) => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <FormSchema ProgId="Leave" CategoryId="company">
              <Tables>
                <FormTable TableName="Leave" DbTableName="ft_leave">
                  <Fields>
                    <FormField FieldName="emp_rowid" DbType="Guid" RelationProgId="Employee">
                      <RelationFieldMappings>
                        <FieldMapping SourceField="dept_rowid" DestinationField="dept_rowid" LookupOnly="true" />
                        <FieldMapping SourceField="ref_dept_name" DestinationField="ref_dept_name" LookupOnly="true" />
                      </RelationFieldMappings>
                    </FormField>
                    <FormField FieldName="dept_rowid" DbType="Guid" RelationProgId="Department">
                      {deptMappings}
                    </FormField>
                    <FormField FieldName="ref_dept_name" DbType="String" Type="RelationField" />
                  </Fields>
                </FormTable>
              </Tables>
            </FormSchema>
            """;

        [Fact]
        [DisplayName("A RelationField written only by lookup-only mappings reports POLHEM1006")]
        public void RelationFieldOnlyLookupOnly_ReportsDiagnostic()
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(new RelationMappingAnalyzer(), (SchemaPath, LeaveSchema(string.Empty)));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM1006", diagnostic.Id);
            Assert.Contains("'ref_dept_name'", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Lookup-only mappings into a stored field and into a joined RelationField report nothing")]
        public void LookupOnlyWithJoinedRelationField_ReportsNothing()
        {
            const string deptMappings = """
                <RelationFieldMappings>
                  <FieldMapping SourceField="sys_name" DestinationField="ref_dept_name" />
                </RelationFieldMappings>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new RelationMappingAnalyzer(), (SchemaPath, LeaveSchema(deptMappings)));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A lookup-only mapping into an undeclared field still reports POLHEM1005")]
        public void LookupOnlyUnknownDestination_ReportsDiagnostic()
        {
            var xml = LeaveSchema(string.Empty).Replace(
                "DestinationField=\"dept_rowid\"", "DestinationField=\"dept_code\"", StringComparison.Ordinal);

            // Act
            var diagnostics = AnalyzerRunner.Run(new RelationMappingAnalyzer(), (SchemaPath, xml));

            // Assert
            Assert.Contains(diagnostics, d => d.Id == "POLHEM1005");
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
