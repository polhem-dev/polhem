using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Definitions;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Tests for POLHEM1007 (a field must not be declared twice in one table).
    /// </summary>
    public class DuplicateFieldNameAnalyzerTests
    {
        private const string SchemaPath = "Define/FormSchema/Order.FormSchema.xml";

        [Fact]
        [DisplayName("A duplicate field within one FormTable reports POLHEM1007")]
        public void DuplicateWithinTable_ReportsDiagnostic()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Order" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Order" DbTableName="ft_order">
                      <Fields>
                        <FormField FieldName="sys_id" DbType="String" />
                        <FormField FieldName="sys_name" DbType="String" />
                        <FormField FieldName="sys_id" DbType="String" />
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new DuplicateFieldNameAnalyzer(), (SchemaPath, xml));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM1007", diagnostic.Id);

            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("'Order'", message, StringComparison.Ordinal);
            Assert.Contains("'sys_id'", message, StringComparison.Ordinal);

            // The later occurrence is reported (line 8, 0-based 7); the earlier one stays as the reader's reference point.
            Assert.Equal(7, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
        }

        [Fact]
        [DisplayName("Fields with the same name in different master-detail tables are not falsely reported")]
        public void SameNameAcrossTables_ReportsNothing()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Order" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Order" DbTableName="ft_order">
                      <Fields>
                        <FormField FieldName="sys_id" DbType="String" />
                        <FormField FieldName="sys_name" DbType="String" />
                      </Fields>
                    </FormTable>
                    <FormTable TableName="OrderDetail" DbTableName="ft_order_detail">
                      <Fields>
                        <FormField FieldName="sys_id" DbType="String" />
                        <FormField FieldName="sys_name" DbType="String" />
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new DuplicateFieldNameAnalyzer(), (SchemaPath, xml));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("Names that differ only in casing still count as duplicates")]
        public void CasingOnlyDifference_IsTreatedAsDuplicate()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Order" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Order" DbTableName="ft_order">
                      <Fields>
                        <FormField FieldName="sys_id" DbType="String" />
                        <FormField FieldName="SYS_ID" DbType="String" />
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new DuplicateFieldNameAnalyzer(), (SchemaPath, xml));

            // Assert
            Assert.Single(diagnostics);
        }

        [Fact]
        [DisplayName("Duplicate fields in a TableSchema are checked too")]
        public void DuplicateWithinTableSchema_ReportsDiagnostic()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <TableSchema TableName="ft_order">
                  <Fields>
                    <DbField FieldName="sys_id" DbType="String" />
                    <DbField FieldName="sys_id" DbType="String" />
                  </Fields>
                </TableSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new DuplicateFieldNameAnalyzer(),
                ("Define/TableSchema/company/ft_order.TableSchema.xml", xml));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM1007", diagnostic.Id);
            Assert.Contains("'ft_order'", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("No duplicate fields reports nothing")]
        public void NoDuplicates_ReportsNothing()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Order" CategoryId="company">
                  <Tables>
                    <FormTable TableName="Order" DbTableName="ft_order">
                      <Fields>
                        <FormField FieldName="sys_id" DbType="String" />
                        <FormField FieldName="sys_name" DbType="String" />
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new DuplicateFieldNameAnalyzer(), (SchemaPath, xml));

            // Assert
            Assert.Empty(diagnostics);
        }
    }
}
