using System.ComponentModel;
using Polhem.Definition.Settings;
using Polhem.Web.Blazor.Server.Shell;

namespace Polhem.Web.Blazor.Server.UnitTests.Shell
{
    /// <summary>
    /// Covers how <see cref="ShellMenuRows"/> flattens a menu into headers and form rows.
    /// </summary>
    public class ShellMenuRowsTests
    {
        private static List<ShellMenuRow> Flatten(MenuSettings settings)
        {
            var rows = new List<ShellMenuRow>();
            ShellMenuRows.AddNodes(rows, settings.Items, node => node.Caption);
            return rows;
        }

        [Fact]
        [DisplayName("Folders become headers followed by their forms, depth first, in display order")]
        public void AddNodes_FoldersBecomeHeadersFollowedByForms()
        {
            var settings = new MenuSettings();
            var master = new MenuFolder("master", "Master Data") { Order = 2 };
            master.Items!.Add(new MenuEntry("customer", "Customer", "Customers") { Order = 2 });
            master.Items.Add(new MenuEntry("product", "Product", "Products") { Order = 1 });
            settings.Items!.Add(master);
            settings.Items.Add(new MenuEntry("home", "Dashboard", "Dashboard") { Order = 1 });

            Assert.Equal(
                [new ShellMenuRow("Dashboard", "Dashboard"), new ShellMenuRow("Master Data", null),
                    new ShellMenuRow("Products", "Product"), new ShellMenuRow("Customers", "Customer")],
                Flatten(settings));
        }

        [Fact]
        [DisplayName("Hidden nodes, entries without a program and folders left empty are dropped")]
        public void AddNodes_DropsHiddenEntriesAndEmptyFolders()
        {
            var settings = new MenuSettings();
            var empty = new MenuFolder("empty", "Empty");
            empty.Items!.Add(new MenuEntry("hidden", "Hidden", "Hidden") { Visible = false });
            empty.Items.Add(new MenuEntry("blank", string.Empty, "No program"));
            settings.Items!.Add(empty);
            settings.Items.Add(new MenuEntry("order", "Order", "Orders"));

            Assert.Equal([new ShellMenuRow("Orders", "Order")], Flatten(settings));
        }
    }
}
