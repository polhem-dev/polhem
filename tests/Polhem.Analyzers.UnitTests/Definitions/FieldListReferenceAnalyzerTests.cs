using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Definitions;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Tests for POLHEM1004 (a field list may refer only to declared fields).
    /// </summary>
    public class FieldListReferenceAnalyzerTests
    {
        private const string SchemaPath = "Define/FormSchema/Product.FormSchema.xml";

        [Fact]
        [DisplayName("ListFields referring to an undeclared field reports POLHEM1004")]
        public void UnknownListField_ReportsDiagnostic()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company" ListFields="sys_id,unit_prise">
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
            var diagnostics = AnalyzerRunner.Run(new FieldListReferenceAnalyzer(), (SchemaPath, xml));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM1004", diagnostic.Id);

            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("'unit_prise'", message, StringComparison.Ordinal);
            Assert.Contains("ListFields", message, StringComparison.Ordinal);

            // An unknown field in `ListFields` is not skipped silently. The layout loses a column, but `SelectBuilder` still receives it and throws.
            Assert.Contains("InvalidOperationException", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("LookupFields is checked too")]
        public void UnknownLookupField_ReportsDiagnostic()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company" LookupFields="sys_id,sys_title">
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
            var diagnostics = AnalyzerRunner.Run(new FieldListReferenceAnalyzer(), (SchemaPath, xml));

            // Assert
            var message = Assert.Single(diagnostics).GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("LookupFields", message, StringComparison.Ordinal);

            // `LookupFields` really is skipped silently, because `GetLookupFields` filters with `master.Fields.Contains`.
            Assert.Contains("skipped silently", message, StringComparison.Ordinal);
            Assert.DoesNotContain("InvalidOperationException", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A master-detail list may mix fields from different tables without a false report")]
        public void FieldsFromMultipleTables_ReportNothing()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Order" CategoryId="company" ListFields="sys_id,line_amount">
                  <Tables>
                    <FormTable TableName="Order" DbTableName="ft_order">
                      <Fields>
                        <FormField FieldName="sys_id" DbType="String" />
                      </Fields>
                    </FormTable>
                    <FormTable TableName="OrderDetail" DbTableName="ft_order_detail">
                      <Fields>
                        <FormField FieldName="line_amount" DbType="Currency" />
                      </Fields>
                    </FormTable>
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(new FieldListReferenceAnalyzer(), (SchemaPath, xml));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("Whitespace and empty entries in the list are tolerated")]
        public void WhitespaceAndEmptyEntries_AreTolerated()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="company" ListFields=" sys_id , , sys_name ">
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
            var diagnostics = AnalyzerRunner.Run(new FieldListReferenceAnalyzer(), (SchemaPath, xml));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A schema without a ListFields attribute reports nothing")]
        public void MissingListFields_ReportsNothing()
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
            var diagnostics = AnalyzerRunner.Run(new FieldListReferenceAnalyzer(), (SchemaPath, xml));

            // Assert
            Assert.Empty(diagnostics);
        }
    }
}
