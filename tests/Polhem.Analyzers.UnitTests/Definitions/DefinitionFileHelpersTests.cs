using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Polhem.Analyzers.Definitions;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Tests for the helpers the definition-file rules share: path handling in <see cref="DefinitionFileNames"/>,
    /// the scope registry built by <see cref="DbCategoryRegistry"/>, and the parse cache of
    /// <see cref="DefinitionDocumentLoader"/>.
    /// </summary>
    public class DefinitionFileHelpersTests
    {
        [Theory]
        [InlineData("Define/FormSchema/Product.FormSchema.xml", "Product")]
        [InlineData(@"Define\FormSchema\Product.FormSchema.xml", "Product")]
        [InlineData("Product.xml", "Product.xml")]
        [DisplayName("GetProgIdFromSidecar strips the directory and the suffix, and keeps a name without the suffix whole")]
        public void GetProgIdFromSidecar_StripsDirectoryAndSuffix(string path, string expected)
        {
            Assert.Equal(expected, DefinitionFileNames.GetProgIdFromSidecar(path, DefinitionFileNames.FormSchemaSuffix));
        }

        [Theory]
        [InlineData("st_user.TableSchema.xml")]
        [InlineData("/st_user.TableSchema.xml")]
        [InlineData("TableSchema//st_user.TableSchema.xml")]
        [DisplayName("GetParentFolderName returns null when the file has no named containing folder")]
        public void GetParentFolderName_NoNamedFolder_ReturnsNull(string path)
        {
            Assert.Null(DefinitionFileNames.GetParentFolderName(path));
        }

        [Fact]
        [DisplayName("GetParentFolderName returns a relative folder that has no parent of its own")]
        public void GetParentFolderName_SingleRelativeFolder_ReturnsIt()
        {
            Assert.Equal("company", DefinitionFileNames.GetParentFolderName(@"company\ft_order.TableSchema.xml"));
        }

        [Fact]
        [DisplayName("GetTableSchemaKey returns null for a table schema outside any scope folder")]
        public void GetTableSchemaKey_NoScopeFolder_ReturnsNull()
        {
            Assert.Null(DefinitionFileNames.GetTableSchemaKey("ft_order.TableSchema.xml"));
        }

        [Fact]
        [DisplayName("GetTableSchemaKey composes the scope folder and the table name")]
        public void GetTableSchemaKey_InScopeFolder_ComposesKey()
        {
            Assert.Equal(DefinitionFileNames.ComposeTableSchemaKey("company", "ft_order"),
                DefinitionFileNames.GetTableSchemaKey("Define/TableSchema/company/ft_order.TableSchema.xml"));
        }

        [Fact]
        [DisplayName("DbCategoryRegistry is not created when no DbCategorySettings.xml is among the files")]
        public void TryCreate_NoSettingsFile_ReturnsNull()
        {
            Assert.Null(DbCategoryRegistry.TryCreate(Files(("Define/Other.xml", "<Other />")), CancellationToken.None));
        }

        [Fact]
        [DisplayName("DbCategoryRegistry is not created from a malformed settings file")]
        public void TryCreate_MalformedSettings_ReturnsNull()
        {
            Assert.Null(DbCategoryRegistry.TryCreate(
                Files(("Define/DbCategorySettings.xml", "<DbCategorySettings><DbCategory")), CancellationToken.None));
        }

        [Fact]
        [DisplayName("DbCategoryRegistry is not created when no category carries an id")]
        public void TryCreate_NoCategoryWithId_ReturnsNull()
        {
            const string xml = """
                <DbCategorySettings>
                  <DbCategory><TableItem TableName="ft_order" /></DbCategory>
                </DbCategorySettings>
                """;

            Assert.Null(DbCategoryRegistry.TryCreate(Files(("Define/DbCategorySettings.xml", xml)), CancellationToken.None));
        }

        [Fact]
        [DisplayName("DbCategoryRegistry merges repeated scopes, skips unnamed tables and answers lookups")]
        public void TryCreate_RepeatedScopeAndUnnamedTable_BuildsRegistry()
        {
            const string xml = """
                <DbCategorySettings>
                  <DbCategory Id="company"><TableItem TableName="ft_order" /><TableItem /></DbCategory>
                  <DbCategory Id="company"><TableItem TableName="ft_customer" /></DbCategory>
                  <DbCategory Id="common"><TableItem TableName="st_user" /></DbCategory>
                </DbCategorySettings>
                """;

            var registry = DbCategoryRegistry.TryCreate(Files(("Define/DbCategorySettings.xml", xml)), CancellationToken.None);

            Assert.NotNull(registry);
            Assert.True(registry!.IsRegistered("company", "FT_ORDER"));
            Assert.True(registry.IsRegistered("company", "ft_customer"));
            Assert.False(registry.IsRegistered("log", "ft_order"));
            Assert.Equal("common", registry.FindScopeOf("st_user"));
            Assert.Null(registry.FindScopeOf("ft_missing"));
        }

        [Fact]
        [DisplayName("DefinitionDocumentLoader returns the cached document for the same text")]
        public void TryLoad_SameText_ReturnsCachedDocument()
        {
            var text = SourceText.From("<Root />");

            var first = DefinitionDocumentLoader.TryLoad(text);
            var second = DefinitionDocumentLoader.TryLoad(text);

            Assert.NotNull(first);
            Assert.Same(first, second);
        }

        [Fact]
        [DisplayName("DefinitionDocumentLoader caches a failed parse as null")]
        public void TryLoad_MalformedTextTwice_ReturnsNullBothTimes()
        {
            var text = SourceText.From("<Root");

            Assert.Null(DefinitionDocumentLoader.TryLoad(text));
            Assert.Null(DefinitionDocumentLoader.TryLoad(text));
        }

        private static AdditionalText[] Files(params (string Path, string Content)[] files)
            => files.Select(file => (AdditionalText)new TestAdditionalText(file.Path, file.Content)).ToArray();
    }
}
