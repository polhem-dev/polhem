using System.ComponentModel;
using Polhem.Definition.Language;
using Polhem.Definition.Settings;
using Polhem.UI.Avalonia.Shell.Navigation;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

public class NavMenuBuilderTests
{
    [Fact]
    [DisplayName("Build flattens nested folders into header rows followed by their links, depth first")]
    public void Build_NestedFolders_FlattensDepthFirst()
    {
        var settings = new MenuSettings();
        var master = new MenuFolder("master", "Master Data");
        master.Items!.Add(new MenuEntry("category", "Category", "Categories"));
        var nested = new MenuFolder("people", "People");
        nested.Items!.Add(new MenuEntry("customer", "Customer", "Customers"));
        master.Items.Add(nested);
        settings.Items!.Add(master);
        settings.Items.Add(new MenuEntry("order", "Order", "Orders"));

        var items = NavMenuBuilder.Build(settings, localizer: null, lang: "en");

        Assert.Equal(
            ["H:Master Data", "F:Categories:Category", "H:People", "F:Customers:Customer", "F:Orders:Order"],
            items.Select(Describe));
    }

    [Fact]
    [DisplayName("Build applies Order and Visible through GetDisplayNodes")]
    public void Build_OrderAndVisible_AppliedFromDisplayNodes()
    {
        var settings = new MenuSettings();
        settings.Items!.Add(new MenuEntry("b", "B", "Second") { Order = 2 });
        settings.Items.Add(new MenuEntry("a", "A", "First") { Order = 1 });
        settings.Items.Add(new MenuEntry("h", "H", "Hidden") { Order = 0, Visible = false });

        var items = NavMenuBuilder.Build(settings, localizer: null, lang: "en");

        Assert.Equal(["F:First:A", "F:Second:B"], items.Select(Describe));
    }

    [Fact]
    [DisplayName("Build drops a folder that has no form to open")]
    public void Build_FolderWithoutLinks_IsDropped()
    {
        var settings = new MenuSettings();
        var empty = new MenuFolder("empty", "Empty");
        empty.Items!.Add(new MenuFolder("inner", "Inner"));
        settings.Items!.Add(empty);
        settings.Items.Add(new MenuEntry("a", "A", "Only"));

        var items = NavMenuBuilder.Build(settings, localizer: null, lang: "en");

        Assert.Equal(["F:Only:A"], items.Select(Describe));
    }

    [Fact]
    [DisplayName("Build takes captions from MenuLocalizer and falls back to the menu file caption")]
    public void Build_WithLocalizer_UsesTranslatedCaptions()
    {
        var settings = new MenuSettings();
        var folder = new MenuFolder("master", "Master Data");
        folder.Items!.Add(new MenuEntry("category", "Category", "Categories"));
        folder.Items.Add(new MenuEntry("product", "Product", "Products"));
        settings.Items!.Add(folder);
        var localizer = new MenuLocalizer(new FakeLanguageService(new Dictionary<string, string>
        {
            ["Folder.master.Caption"] = "基本資料",
            ["Entry.category.Caption"] = "類別",
        }));

        var items = NavMenuBuilder.Build(settings, localizer, "zh-TW");

        Assert.Equal(["H:基本資料", "F:類別:Category", "F:Products:Product"], items.Select(Describe));
    }

    [Theory]
    [DisplayName("Build returns no rows for a missing or empty menu")]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_NoMenu_ReturnsEmpty(bool emptySettings)
    {
        var items = NavMenuBuilder.Build(emptySettings ? new MenuSettings() : null, localizer: null, lang: "en");

        Assert.Empty(items);
    }

    private static string Describe(NavItem item)
        => item.IsHeader ? "H:" + item.Title : "F:" + item.Title + ":" + item.ProgId;

    private sealed class FakeLanguageService(IReadOnlyDictionary<string, string> texts) : ILanguageService
    {
        public string GetLangText(string lang, string fullKey) => string.Empty;

        public string GetLangText(string lang, string @namespace, string subKey) => string.Empty;

        public bool TryGetLangText(string lang, string fullKey, out string text)
        {
            text = string.Empty;
            return false;
        }

        public bool TryGetLangText(string lang, string @namespace, string subKey, out string text)
            => texts.TryGetValue(subKey, out text!);

        public LanguageEnum? GetLangEnum(string lang, string fullName) => null;

        public LanguageEnum? GetLangEnum(string lang, string @namespace, string enumName) => null;

        public string? GetLangEnumText(string lang, string fullName, string code) => null;
    }
}
