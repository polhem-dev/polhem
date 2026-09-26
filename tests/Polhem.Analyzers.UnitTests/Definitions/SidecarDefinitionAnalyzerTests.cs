using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Definitions;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Tests for POLHEM2002 (a TableSchema must be in the folder of its scope) and POLHEM2005 (a matching FormLayout should exist).
    /// </summary>
    public class SidecarDefinitionAnalyzerTests
    {
        private const string SchemaPath = "Define/FormSchema/Product.FormSchema.xml";
        private const string LayoutPath = "Define/FormLayout/Product.FormLayout.xml";

        private const string Schema = """
            <?xml version="1.0" encoding="utf-8"?>
            <FormSchema ProgId="Product" CategoryId="company">
              <Tables>
                <FormTable TableName="Product" DbTableName="ft_product" />
              </Tables>
            </FormSchema>
            """;

        private const string TableSchema = """
            <?xml version="1.0" encoding="utf-8"?>
            <TableSchema TableName="ft_product">
              <Fields>
                <DbField FieldName="sys_id" DbType="String" />
              </Fields>
            </TableSchema>
            """;

        private const string Layout = """
            <?xml version="1.0" encoding="utf-8"?>
            <FormLayout LayoutId="Product" ProgId="Product" />
            """;

        [Fact]
        [DisplayName("A TableSchema in the wrong scope folder reports POLHEM2002 and names the actual folder")]
        public void TableSchemaInWrongScopeFolder_ReportsDiagnostic()
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new SidecarDefinitionAnalyzer(),
                (SchemaPath, Schema),
                ("Define/TableSchema/common/ft_product.TableSchema.xml", TableSchema),
                (LayoutPath, Layout));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM2002", diagnostic.Id);

            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("TableSchema/company/ft_product.TableSchema.xml", message, StringComparison.Ordinal);
            Assert.Contains("One exists under 'common' instead", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A missing TableSchema reports POLHEM2002 and suggests adding one")]
        public void TableSchemaMissingEntirely_SuggestsAdding()
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new SidecarDefinitionAnalyzer(),
                (SchemaPath, Schema),
                ("Define/TableSchema/company/ft_other.TableSchema.xml", TableSchema),
                (LayoutPath, Layout));

            // Assert
            var message = Assert.Single(diagnostics).GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("add the table schema, or correct DbTableName", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A TableSchema in the matching scope folder reports nothing")]
        public void TableSchemaInMatchingFolder_ReportsNothing()
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new SidecarDefinitionAnalyzer(),
                (SchemaPath, Schema),
                ("Define/TableSchema/company/ft_product.TableSchema.xml", TableSchema),
                (LayoutPath, Layout));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A missing matching FormLayout reports POLHEM2005")]
        public void MissingFormLayout_ReportsDiagnostic()
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new SidecarDefinitionAnalyzer(),
                (SchemaPath, Schema),
                ("Define/TableSchema/company/ft_product.TableSchema.xml", TableSchema),
                ("Define/FormLayout/Other.FormLayout.xml", Layout));

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM2005", diagnostic.Id);
            Assert.Contains("FormLayout/Product.FormLayout.xml", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("With no TableSchema files at all the rule stays silent (definitions may live in the database)")]
        public void NoTableSchemaFilesAtAll_StaysSilent()
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new SidecarDefinitionAnalyzer(),
                (SchemaPath, Schema),
                (LayoutPath, Layout));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("With no FormLayout files at all the rule stays silent")]
        public void NoFormLayoutFilesAtAll_StaysSilent()
        {
            // Act
            var diagnostics = AnalyzerRunner.Run(
                new SidecarDefinitionAnalyzer(),
                (SchemaPath, Schema),
                ("Define/TableSchema/company/ft_product.TableSchema.xml", TableSchema));

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("With an invalid CategoryId, POLHEM2002 defers to POLHEM1001")]
        public void InvalidCategoryId_DefersToPolhem1001()
        {
            const string invalidSchema = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Product" CategoryId="business">
                  <Tables>
                    <FormTable TableName="Product" DbTableName="ft_product" />
                  </Tables>
                </FormSchema>
                """;

            // Act
            var diagnostics = AnalyzerRunner.Run(
                new SidecarDefinitionAnalyzer(),
                (SchemaPath, invalidSchema),
                ("Define/TableSchema/company/ft_product.TableSchema.xml", TableSchema),
                (LayoutPath, Layout));

            // Assert
            Assert.Empty(diagnostics);
        }
    }
}
