using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;
using Polhem.UI.Core.Definitions;

namespace Polhem.UI.Core.UnitTests.Definitions
{
    /// <summary>
    /// Covers <see cref="StorageDefinitionSource"/> over the shared definition files in <c>tests/Define</c>, which it
    /// only reads.
    /// </summary>
    public class StorageDefinitionSourceTests
    {
        private static StorageDefinitionSource CreateSource()
            => new(new FileDefineStorage(new PathOptions { DefinePath = Path.Combine(RepoRoot.Find(), "tests", "Define") }));

        [Fact]
        [DisplayName("GetFormSchemaAsync returns the stored schema of the program")]
        public async Task GetFormSchemaAsync_StoredProgram_ReturnsSchema()
        {
            var schema = await CreateSource().GetFormSchemaAsync("Customer");

            Assert.Equal("Customer", schema.ProgId);
        }

        [Fact]
        [DisplayName("GetFormLayoutAsync returns a new instance on every call, because a view degrades its layout in place")]
        public async Task GetFormLayoutAsync_ReturnsCallerOwnedInstance()
        {
            var source = CreateSource();
            var schema = await source.GetFormSchemaAsync("Customer");

            var first = await source.GetFormLayoutAsync("Customer", schema);
            var second = await source.GetFormLayoutAsync("Customer", schema);

            Assert.NotSame(first, second);
        }
    }
}
