using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Polhem.Analyzers.Definitions;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Tests for how <see cref="DefinitionContext"/> gathers definition files into models: duplicates, files it cannot
    /// read, and files outside the folders their kind requires.
    /// </summary>
    public class DefinitionContextTests
    {
        [Fact]
        [DisplayName("Create accepts a lazily evaluated file sequence and indexes each kind of definition")]
        public void Create_LazySequence_IndexesEveryKind()
        {
            var files = new[]
            {
                ("Define/FormSchema/Order.FormSchema.xml", """<FormSchema ProgId="Order" CategoryId="company" />"""),
                ("Define/TableSchema/company/ft_order.TableSchema.xml", """<TableSchema TableName="ft_order" />"""),
                ("Define/FormLayout/Order.FormLayout.xml", "<FormLayout />"),
                ("Define/Language/zh-TW/Order.Language.xml", """<LanguageResource Namespace="Order" />"""),
                ("Define/Readme.txt", "not a definition"),
            }.Select(file => (AdditionalText)new TestAdditionalText(file.Item1, file.Item2));

            var context = DefinitionContext.Create(files, CancellationToken.None);

            Assert.NotNull(context.FindByProgId("order"));
            Assert.True(context.HasTableSchemaFiles);
            Assert.True(context.HasFormLayoutFiles);
            Assert.Single(context.LanguageResources);
            Assert.Null(context.Categories);
        }

        [Fact]
        [DisplayName("Of two form schemas with the same ProgId both are listed and the first is the one looked up")]
        public void Create_DuplicateProgId_FirstWinsLookup()
        {
            var context = DefinitionContext.Create(Files(
                ("A/Order.FormSchema.xml", """<FormSchema ProgId="Order" CategoryId="company" />"""),
                ("B/Order.FormSchema.xml", """<FormSchema ProgId="Order" CategoryId="common" />""")), CancellationToken.None);

            Assert.Equal(2, context.FormSchemas.Count);
            Assert.Equal("company", context.FindByProgId("Order")!.CategoryId);
        }

        [Fact]
        [DisplayName("Table schemas outside a scope folder, repeated in the same folder or malformed are not indexed twice")]
        public void Create_TableSchemaEdgeCases_IndexesOnlyFirstValid()
        {
            var context = DefinitionContext.Create(Files(
                ("ft_loose.TableSchema.xml", """<TableSchema TableName="ft_loose" />"""),
                ("A/company/ft_order.TableSchema.xml", """<TableSchema TableName="ft_order" />"""),
                ("B/company/ft_order.TableSchema.xml", """<TableSchema TableName="ft_order" />"""),
                ("A/company/ft_broken.TableSchema.xml", "<TableSchema")), CancellationToken.None);

            var table = Assert.Single(context.TableSchemas);
            Assert.Equal("ft_order", table.TableName);
        }

        [Fact]
        [DisplayName("Malformed form schemas and language resources, and a language file outside a culture folder, are skipped")]
        public void Create_UnreadableFiles_AreSkipped()
        {
            var context = DefinitionContext.Create(Files(
                ("Define/FormSchema/Broken.FormSchema.xml", "<FormSchema"),
                ("Define/Language/zh-TW/Broken.Language.xml", "<LanguageResource"),
                ("Loose.Language.xml", "<LanguageResource />")), CancellationToken.None);

            Assert.Empty(context.FormSchemas);
            Assert.Empty(context.LanguageResources);
            Assert.False(context.HasTableSchemaFiles);
            Assert.False(context.HasFormLayoutFiles);
        }

        [Fact]
        [DisplayName("A file whose text cannot be read yields no model of any kind")]
        public void TryCreate_TextUnavailable_ReturnsNull()
        {
            Assert.Null(FormSchemaModel.TryCreate(new UnreadableText("Order.FormSchema.xml"), CancellationToken.None));
            Assert.Null(TableSchemaModel.TryCreate(new UnreadableText("company/ft_order.TableSchema.xml"), CancellationToken.None));
            Assert.Null(LanguageResourceModel.TryCreate(new UnreadableText("zh-TW/Order.Language.xml"), CancellationToken.None));
        }

        [Fact]
        [DisplayName("A form schema without ProgId or CategoryId takes the ProgId from its file name and has no CategoryId")]
        public void FormSchemaModel_MissingAttributes_FallsBackToFileName()
        {
            var model = FormSchemaModel.TryCreate(
                new TestAdditionalText("Define/FormSchema/Customer.FormSchema.xml",
                    """<FormSchema><FormField FieldName="sys_id" /><FormField FieldName="" /><FormField /></FormSchema>"""),
                CancellationToken.None);

            Assert.NotNull(model);
            Assert.Equal("Customer", model!.ProgId);
            Assert.Null(model.CategoryId);
            Assert.Equal(["sys_id"], model.DeclaredFieldNames);
            Assert.Same(model.DeclaredFieldNames, model.DeclaredFieldNames);
        }

        private static AdditionalText[] Files(params (string Path, string Content)[] files)
            => files.Select(file => (AdditionalText)new TestAdditionalText(file.Path, file.Content)).ToArray();

        /// <summary>
        /// An additional file whose text the host cannot provide.
        /// </summary>
        private sealed class UnreadableText(string path) : AdditionalText
        {
            public override string Path { get; } = path;

            public override SourceText? GetText(CancellationToken cancellationToken = default) => null;
        }
    }
}
