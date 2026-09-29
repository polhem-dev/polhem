using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// Tests for the menu definition classes such as MenuSettings, MenuFolder and MenuEntry.
    /// </summary>
    public class MenuSettingsTests
    {
        /// <summary>
        /// Builds a three-level nested menu: root -> transactions -> (customer, sales -> (sales-order, sales-return)), dashboard.
        /// </summary>
        private static MenuSettings BuildNestedMenu()
        {
            var settings = new MenuSettings();
            var transactions = settings.Items!.AddFolder("transactions", "交易");
            transactions.Order = 10;
            transactions.Items!.AddEntry("customer", "Customer", "客戶").Order = 10;

            var sales = transactions.Items!.AddFolder("sales", "銷售");
            sales.Order = 20;
            sales.Items!.AddEntry("sales-order", "Order", "訂單").Order = 10;
            sales.Items!.AddEntry("sales-return", "Order", "退貨單").Order = 20;

            settings.Items!.AddEntry("dashboard", "Dashboard", "儀表板").Order = 20;
            return settings;
        }

        [Fact]
        [DisplayName("MenuSettings has a non-null Items and the initial serialize state by default")]
        public void MenuSettings_Default_HasItems()
        {
            var settings = new MenuSettings();

            Assert.NotNull(settings.Items);
            Assert.Equal(string.Empty, settings.ObjectFilePath);
        }

        [Fact]
        [DisplayName("MenuSettings.SetObjectFilePath updates the file path")]
        public void MenuSettings_SetObjectFilePath_UpdatesPath()
        {
            var settings = new MenuSettings();

            settings.SetObjectFilePath("/tmp/menu.xml");

            Assert.Equal("/tmp/menu.xml", settings.ObjectFilePath);
        }

        [Fact]
        [DisplayName("MenuSettings.Items is not serialized while it is empty, whether or not it was read")]
        public void MenuSettings_Items_EmptyCollection_IsNotSerialized()
        {
            var settings = new MenuSettings();

            Assert.False(settings.ItemsSpecified);
            Assert.Empty(settings.Items!);
            Assert.False(settings.ItemsSpecified);
        }

        [Fact]
        [DisplayName("A three-level nested menu round-trips its full structure and types through XML")]
        public void MenuSettings_DeepNested_XmlRoundtrip_PreservesStructure()
        {
            var xml = XmlCodec.Serialize(BuildNestedMenu());

            var restored = XmlCodec.Deserialize<MenuSettings>(xml);

            Assert.NotNull(restored);
            Assert.Equal(2, restored!.Items!.Count);
            var transactions = Assert.IsType<MenuFolder>(restored.Items![0]);
            Assert.Equal(2, transactions.Items!.Count);
            Assert.IsType<MenuEntry>(transactions.Items![0]);

            var sales = Assert.IsType<MenuFolder>(transactions.Items![1]);
            Assert.Equal(2, sales.Items!.Count);
            var salesReturn = Assert.IsType<MenuEntry>(sales.Items![1]);
            Assert.Equal("sales-return", salesReturn.Id);
            Assert.Equal("Order", salesReturn.ProgId);
            Assert.Equal(20, salesReturn.Order);

            Assert.IsType<MenuEntry>(restored.Items![1]);
        }

        [Fact]
        [DisplayName("Polymorphic nodes are written with their own element names, not an xsi:type discriminator")]
        public void MenuSettings_Xml_UsesPerSubtypeElementNames()
        {
            var xml = XmlCodec.Serialize(BuildNestedMenu());

            Assert.Contains("<MenuFolder ", xml, StringComparison.Ordinal);
            Assert.Contains("<MenuEntry ", xml, StringComparison.Ordinal);
            Assert.DoesNotContain("xsi:type", xml, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("An empty MenuFolder does not write an Items element when serialized")]
        public void MenuFolder_EmptyItems_OmittedFromXml()
        {
            var settings = new MenuSettings();
            settings.Items!.AddFolder("empty", "空資料夾");

            var xml = XmlCodec.Serialize(settings);

            Assert.Contains("<MenuFolder ", xml, StringComparison.Ordinal);
            Assert.DoesNotContain("<Items />", xml, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("EnumerateNodes walks the whole tree depth-first in document order")]
        public void EnumerateNodes_WalksWholeTreeDepthFirst()
        {
            var ids = BuildNestedMenu().EnumerateNodes().Select(n => n.Id).ToArray();

            Assert.Equal(
                ["transactions", "customer", "sales", "sales-order", "sales-return", "dashboard"],
                ids);
        }

        [Fact]
        [DisplayName("FindNode finds a node by Id across levels")]
        public void FindNode_FindsNestedNode()
        {
            var settings = BuildNestedMenu();

            var node = settings.FindNode("sales-return");

            var entry = Assert.IsType<MenuEntry>(node);
            Assert.Equal("Order", entry.ProgId);
        }

        [Fact]
        [DisplayName("FindNode returns null when nothing matches")]
        public void FindNode_Missing_ReturnsNull()
        {
            Assert.Null(BuildNestedMenu().FindNode("nope"));
        }

        [Fact]
        [DisplayName("One progId can map to several menu nodes (1:N)")]
        public void MenuEntry_SameProgId_MayAppearInSeveralNodes()
        {
            var settings = BuildNestedMenu();

            var forOrder = settings.EnumerateNodes().OfType<MenuEntry>()
                .Where(e => e.ProgId == "Order").Select(e => e.Id).ToArray();

            Assert.Equal(["sales-order", "sales-return"], forOrder);
        }

        [Fact]
        [DisplayName("GetDisplayNodes filters out invisible nodes and sorts by Order")]
        public void GetDisplayNodes_FiltersInvisibleAndSortsByOrder()
        {
            var settings = new MenuSettings();
            settings.Items!.AddEntry("c", "C", "C").Order = 30;
            settings.Items!.AddEntry("a", "A", "A").Order = 10;
            var hidden = settings.Items!.AddEntry("b", "B", "B");
            hidden.Order = 20;
            hidden.Visible = false;

            var ids = settings.Items!.GetDisplayNodes().Select(n => n.Id).ToArray();

            Assert.Equal(["a", "c"], ids);
        }

        [Fact]
        [DisplayName("GetDisplayNodes keeps document order for equal Order values")]
        public void GetDisplayNodes_EqualOrder_KeepsDocumentOrder()
        {
            var settings = new MenuSettings();
            settings.Items!.AddEntry("first", "A", "A");
            settings.Items!.AddEntry("second", "B", "B");

            var ids = settings.Items!.GetDisplayNodes().Select(n => n.Id).ToArray();

            Assert.Equal(["first", "second"], ids);
        }

        [Fact]
        [DisplayName("Validate finds no problems in a well-formed menu")]
        public void Validate_ValidMenu_ReturnsEmpty()
        {
            Assert.Empty(BuildNestedMenu().Validate());
        }

        [Fact]
        [DisplayName("Validate catches an Id duplicated across levels (the collection itself rejects duplicates within a level)")]
        public void Validate_DuplicateIdAcrossLevels_IsReported()
        {
            var settings = new MenuSettings();
            var folder = settings.Items!.AddFolder("shared", "資料夾");
            // Sibling uniqueness is the collection's job; this duplicate is a level deeper, which
            // is exactly the gap the tree walk exists to close.
            folder.Items!.AddEntry("shared", "Order", "訂單");

            var problems = settings.Validate();

            Assert.Contains(problems, p => p.Contains("'shared'", StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("Validate catches an empty node Id")]
        public void Validate_EmptyId_IsReported()
        {
            var settings = new MenuSettings();
            settings.Items!.Add(new MenuEntry { ProgId = "Order", Caption = "訂單" });

            var problems = settings.Validate();

            Assert.Contains(problems, p => p.Contains("empty Id", StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("Validate catches an empty MenuEntry.ProgId")]
        public void Validate_EmptyProgId_IsReported()
        {
            var settings = new MenuSettings();
            settings.Items!.AddEntry("orphan", string.Empty, "無綁定");

            var problems = settings.Validate();

            Assert.Contains(problems, p => p.Contains("empty ProgId", StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("Validate catches a menu reference to a progId that does not exist in the registry")]
        public void Validate_UnregisteredProgId_IsReported()
        {
            var registry = new ProgramSettings();
            registry.Items!.Add("Customer", "客戶");
            var settings = new MenuSettings();
            settings.Items!.AddEntry("ghost", "NoSuchProgram", "幽靈");

            var problems = settings.Validate(registry);

            Assert.Contains(problems, p => p.Contains("NoSuchProgram", StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("Validate skips the referential integrity check when no registry is given")]
        public void Validate_NoRegistry_SkipsReferentialCheck()
        {
            var settings = new MenuSettings();
            settings.Items!.AddEntry("ghost", "NoSuchProgram", "幽靈");

            Assert.Empty(settings.Validate());
        }

        [Fact]
        [DisplayName("EnsureValid throws for an invalid menu and lists every problem")]
        public void EnsureValid_Invalid_ThrowsListingAllProblems()
        {
            var settings = new MenuSettings();
            var folder = settings.Items!.AddFolder("dup", "資料夾");
            folder.Items!.AddEntry("dup", string.Empty, "重複且無 ProgId");

            var ex = Assert.Throws<InvalidOperationException>(() => settings.EnsureValid());

            Assert.Contains("'dup'", ex.Message, StringComparison.Ordinal);
            Assert.Contains("empty ProgId", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("EnsureValid does not throw for a valid menu")]
        public void EnsureValid_Valid_DoesNotThrow()
        {
            var exception = Record.Exception(() => BuildNestedMenu().EnsureValid());

            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("MenuFolder.ToString and MenuEntry.ToString return recognizable strings")]
        public void ToString_ReturnsIdentifiableText()
        {
            Assert.Equal("sales - 銷售", new MenuFolder("sales", "銷售").ToString());
            Assert.Equal("order - 訂單 (Order)", new MenuEntry("order", "Order", "訂單").ToString());
        }

        [Fact]
        [DisplayName("MenuNodeBase.Visible defaults to true")]
        public void MenuNode_Visible_DefaultsToTrue()
        {
            Assert.True(new MenuFolder().Visible);
            Assert.True(new MenuEntry().Visible);
        }
    }
}
