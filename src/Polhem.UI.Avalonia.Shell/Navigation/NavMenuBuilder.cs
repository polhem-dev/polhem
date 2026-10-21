using Polhem.Definition.Language;
using Polhem.Definition.Settings;

namespace Polhem.UI.Avalonia.Shell.Navigation
{
    /// <summary>
    /// Turns the server's <see cref="MenuSettings"/> into the flat list of rows the navigation menu shows.
    /// </summary>
    /// <remarks>
    /// A menu definition nests folders arbitrarily, while the shell's menu is a flat list, so each folder becomes a
    /// header row followed by its contents, depth first. Order and visibility come from
    /// <see cref="MenuNodeCollection.GetDisplayNodes"/>, the same rule every UI head applies. A folder left with no
    /// form to open is dropped, so the menu shows no header without links.
    /// </remarks>
    internal static class NavMenuBuilder
    {
        /// <summary>
        /// Builds the rows of <paramref name="settings"/>, with captions from <paramref name="localizer"/> in
        /// <paramref name="lang"/> when one is given and the menu file's own captions otherwise.
        /// </summary>
        public static IReadOnlyList<NavItem> Build(MenuSettings? settings, MenuLocalizer? localizer, string lang)
            => Build(settings?.Items, node => localizer?.GetCaption(node, lang) ?? node.Caption);

        /// <summary>Builds the rows of <paramref name="nodes"/> with captions from <paramref name="caption"/>.</summary>
        public static IReadOnlyList<NavItem> Build(MenuNodeCollection? nodes, Func<MenuNodeBase, string> caption)
        {
            ArgumentNullException.ThrowIfNull(caption);

            var items = new List<NavItem>();
            AddNodes(items, nodes, caption);
            return items;
        }

        private static void AddNodes(List<NavItem> items, MenuNodeCollection? nodes, Func<MenuNodeBase, string> caption)
        {
            if (nodes is null) return;

            foreach (var node in nodes.GetDisplayNodes())
            {
                switch (node)
                {
                    case MenuFolder folder:
                        var headerIndex = items.Count;
                        items.Add(NavItem.Header(caption(folder)));
                        AddNodes(items, folder.Items, caption);
                        if (!items.Skip(headerIndex + 1).Any(i => !i.IsHeader))
                            items.RemoveRange(headerIndex, items.Count - headerIndex);
                        break;
                    case MenuEntry entry when !string.IsNullOrWhiteSpace(entry.ProgId):
                        items.Add(NavItem.Form(caption(entry), entry.ProgId));
                        break;
                }
            }
        }
    }
}
