using System.ComponentModel;
using Polhem.Definition.Settings;

namespace Polhem.Cli.UnitTests
{
    /// <summary>
    /// Tests for the <c>dotnet polhem defines split-menu</c> migration, which splits a legacy nested ProgramSettings.xml
    /// into a flat registry plus MenuSettings.
    /// </summary>
    public class SplitMenuMigrationTests
    {
        private const string LegacyXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ProgramSettings>
              <Categories>
                <ProgramCategory Id="master-data" DisplayName="主檔">
                  <Items>
                    <ProgramItem ProgId="Customer" DisplayName="客戶" />
                    <ProgramItem ProgId="Product" DisplayName="產品" BusinessObject="MyErp.ProductBO, MyErp" />
                  </Items>
                </ProgramCategory>
                <ProgramCategory Id="transactions" DisplayName="交易">
                  <Items>
                    <ProgramItem ProgId="Order" DisplayName="訂單" />
                  </Items>
                </ProgramCategory>
              </Categories>
            </ProgramSettings>
            """;

        [Fact]
        [DisplayName("Split flattens the items of every category into a single registry")]
        public void Split_FlattensAllCategoriesIntoOneRegistry()
        {
            var result = SplitMenuMigration.Split(LegacyXml);

            Assert.Equal(3, result.Registry.Items!.Count);
            Assert.True(result.Registry.Items!.Contains("Customer"));
            Assert.True(result.Registry.Items!.Contains("Product"));
            Assert.True(result.Registry.Items!.Contains("Order"));
        }

        [Fact]
        [DisplayName("Split preserves the BusinessObject type name")]
        public void Split_PreservesBusinessObject()
        {
            var result = SplitMenuMigration.Split(LegacyXml);

            Assert.Equal("MyErp.ProductBO, MyErp", result.Registry.Items!["Product"].BusinessObject);
            Assert.Equal(string.Empty, result.Registry.Items!["Customer"].BusinessObject);
        }

        [Fact]
        [DisplayName("Split turns each category into a MenuFolder and each item into a MenuEntry under it")]
        public void Split_ProducesFolderPerCategoryWithEntries()
        {
            var result = SplitMenuMigration.Split(LegacyXml);

            var folders = result.Menu.Items!.OfType<MenuFolder>().ToList();
            Assert.Equal(2, folders.Count);
            Assert.Equal("master-data", folders[0].Id);
            Assert.Equal("主檔", folders[0].Caption);
            Assert.Equal(2, folders[0].Items!.Count);

            var entry = Assert.IsType<MenuEntry>(folders[1].Items!.Single());
            Assert.Equal("Order", entry.ProgId);
            Assert.Equal("訂單", entry.Caption);
        }

        [Fact]
        [DisplayName("Split assigns Order values that increase in document order")]
        public void Split_AssignsIncrementingOrder()
        {
            var result = SplitMenuMigration.Split(LegacyXml);

            var folders = result.Menu.Items!.OfType<MenuFolder>().ToList();
            Assert.Equal(10, folders[0].Order);
            Assert.Equal(20, folders[1].Order);
            Assert.Equal([10, 20], folders[0].Items!.Select(n => n.Order).ToArray());
        }

        [Fact]
        [DisplayName("The menu produced by Split passes the tree-wide Id uniqueness and ProgId reference checks")]
        public void Split_ProducesValidMenu()
        {
            var result = SplitMenuMigration.Split(LegacyXml);

            Assert.Empty(result.Menu.Validate(result.Registry));
        }

        [Fact]
        [DisplayName("When a category Id equals a ProgId, the entry Id gets a numeric suffix to avoid a tree-wide Id collision")]
        public void Split_CategoryIdCollidingWithProgId_Disambiguates()
        {
            // The category is literally named "Order" and also holds a program called "Order";
            // both become nodes in one key space.
            const string xml = """
                <ProgramSettings>
                  <Categories>
                    <ProgramCategory Id="Order" DisplayName="訂單作業">
                      <Items>
                        <ProgramItem ProgId="Order" DisplayName="訂單" />
                      </Items>
                    </ProgramCategory>
                  </Categories>
                </ProgramSettings>
                """;

            var result = SplitMenuMigration.Split(xml);

            var folder = Assert.IsType<MenuFolder>(result.Menu.Items!.Single());
            var entry = Assert.IsType<MenuEntry>(folder.Items!.Single());
            Assert.Equal("Order", folder.Id);
            Assert.Equal("Order-2", entry.Id);
            Assert.Equal("Order", entry.ProgId);
            Assert.Empty(result.Menu.Validate(result.Registry));
        }

        [Fact]
        [DisplayName("A progId repeated across categories aborts with the duplicates listed instead of picking one")]
        public void Split_DuplicateProgIdAcrossCategories_Throws()
        {
            const string xml = """
                <ProgramSettings>
                  <Categories>
                    <ProgramCategory Id="a" DisplayName="A">
                      <Items><ProgramItem ProgId="Order" DisplayName="訂單" /></Items>
                    </ProgramCategory>
                    <ProgramCategory Id="b" DisplayName="B">
                      <Items><ProgramItem ProgId="Order" DisplayName="訂單複本" /></Items>
                    </ProgramCategory>
                  </Categories>
                </ProgramSettings>
                """;

            var ex = Assert.Throws<UsageException>(() => SplitMenuMigration.Split(xml));

            Assert.Contains("Order", ex.Message, StringComparison.Ordinal);
            Assert.Contains("more than one category", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Empty Categories produce an empty registry and an empty menu instead of throwing")]
        public void Split_EmptyCategories_ProducesEmptyResult()
        {
            var result = SplitMenuMigration.Split("<ProgramSettings><Categories /></ProgramSettings>");

            Assert.Empty(result.Registry.Items!);
            Assert.Empty(result.Menu.Items!);
        }
    }
}
