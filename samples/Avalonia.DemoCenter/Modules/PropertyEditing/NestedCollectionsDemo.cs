using System.ComponentModel;

namespace Avalonia.DemoCenter.Modules.PropertyEditing
{
    /// <summary>
    /// A demo object with a collection whose items hold a collection of their own, so the Property Grid case can open
    /// <see cref="Polhem.UI.Avalonia.Controls.CollectionEditDialog"/> from inside another one.
    /// </summary>
    public sealed class NestedCollectionsDemo
    {
        [Category("Data")]
        [Description("A name for the demo object.")]
        public string Name { get; set; } = "Price list";

        [Category("Data")]
        [Description("Groups, each with entries of its own: open a group's Entries from inside this dialog.")]
        public List<DemoGroup> Groups { get; } = [];

        /// <summary>
        /// Returns a demo object with two groups of two entries each.
        /// </summary>
        public static NestedCollectionsDemo Create()
        {
            var demo = new NestedCollectionsDemo();
            var drinks = new DemoGroup { Name = "Drinks" };
            drinks.Entries.Add(new DemoEntry { Name = "Tea", Value = "30" });
            drinks.Entries.Add(new DemoEntry { Name = "Coffee", Value = "45" });
            var snacks = new DemoGroup { Name = "Snacks" };
            snacks.Entries.Add(new DemoEntry { Name = "Cookie", Value = "20" });
            snacks.Entries.Add(new DemoEntry { Name = "Muffin", Value = "35" });
            demo.Groups.Add(drinks);
            demo.Groups.Add(snacks);
            return demo;
        }
    }
}
