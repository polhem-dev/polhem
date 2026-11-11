using System.ComponentModel;
using Polhem.Definition.Settings;
using Polhem.UI.Core.Navigation;

namespace Polhem.UI.Core.UnitTests.Navigation
{
    /// <summary>
    /// How <see cref="MenuRows.Flatten"/> turns a nested menu into the flat rows every shell shows.
    /// </summary>
    public class MenuRowsTests
    {
        private static IReadOnlyList<string> Flatten(MenuNodeCollection nodes)
            => MenuRows.Flatten(nodes, node => node.Caption, header => "# " + header, (caption, progId) => $"{caption}={progId}");

        [Fact]
        [DisplayName("Folders become headers followed by their forms, depth first, in display order")]
        public void Flatten_FoldersBecomeHeadersFollowedByForms()
        {
            var settings = new MenuSettings();
            var master = new MenuFolder("master", "Master") { Order = 2 };
            master.Items!.Add(new MenuEntry("customer", "Customer", "Customers") { Order = 2 });
            master.Items.Add(new MenuEntry("product", "Product", "Products") { Order = 1 });
            settings.Items!.Add(master);
            settings.Items.Add(new MenuEntry("home", "Dashboard", "Dashboard") { Order = 1 });

            Assert.Equal(["Dashboard=Dashboard", "# Master", "Products=Product", "Customers=Customer"], Flatten(settings.Items));
        }

        [Fact]
        [DisplayName("A folder that only holds empty folders or entries without a program is dropped with its headers")]
        public void Flatten_FolderWithNothingToOpen_IsDropped()
        {
            var settings = new MenuSettings();
            var outer = new MenuFolder("outer", "Outer");
            var inner = new MenuFolder("inner", "Inner");
            inner.Items!.Add(new MenuEntry("blank", string.Empty, "No program"));
            outer.Items!.Add(inner);
            settings.Items!.Add(outer);
            settings.Items.Add(new MenuEntry("home", "Dashboard", "Dashboard"));

            Assert.Equal(["Dashboard=Dashboard"], Flatten(settings.Items));
        }

        [Fact]
        [DisplayName("An empty or missing menu gives no rows")]
        public void Flatten_NoNodes_ReturnsEmpty()
        {
            Assert.Empty(MenuRows.Flatten<string>(null, node => node.Caption, header => header, (caption, _) => caption));
        }
    }
}
