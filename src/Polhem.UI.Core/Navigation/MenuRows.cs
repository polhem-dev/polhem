using Polhem.Definition.Settings;

namespace Polhem.UI.Core.Navigation
{
    /// <summary>
    /// Flattens a menu definition into the rows a shell's menu shows, the same way in every UI head.
    /// </summary>
    /// <remarks>
    /// A menu definition nests folders arbitrarily, while a shell menu is a flat list. Each folder becomes a header row
    /// followed by its contents, depth first. Order and visibility come from
    /// <see cref="MenuNodeCollection.GetDisplayNodes"/>. An entry without a program id opens nothing and is skipped,
    /// and a folder left with nothing to open is dropped, so the menu shows no header without links.
    /// </remarks>
    public static class MenuRows
    {
        /// <summary>
        /// Flattens <paramref name="nodes"/> into rows built by the head.
        /// </summary>
        /// <typeparam name="TRow">The head's row type.</typeparam>
        /// <param name="nodes">The menu's nodes, or <c>null</c> for an empty menu.</param>
        /// <param name="caption">Returns the caption to show for a node.</param>
        /// <param name="header">Builds a folder's header row from its caption.</param>
        /// <param name="form">Builds an entry's row from its caption and program id.</param>
        /// <returns>The rows in display order.</returns>
        public static IReadOnlyList<TRow> Flatten<TRow>(MenuNodeCollection? nodes, Func<MenuNodeBase, string> caption,
            Func<string, TRow> header, Func<string, string, TRow> form)
        {
            ArgumentNullException.ThrowIfNull(caption);
            ArgumentNullException.ThrowIfNull(header);
            ArgumentNullException.ThrowIfNull(form);

            var rows = new List<TRow>();
            AddNodes(rows, nodes, caption, header, form);
            return rows;
        }

        // Returns how many form rows the nodes added, so a folder can tell whether it is left empty.
        private static int AddNodes<TRow>(List<TRow> rows, MenuNodeCollection? nodes, Func<MenuNodeBase, string> caption,
            Func<string, TRow> header, Func<string, string, TRow> form)
        {
            if (nodes is null) return 0;

            int forms = 0;
            foreach (var node in nodes.GetDisplayNodes())
            {
                switch (node)
                {
                    case MenuFolder folder:
                        int headerIndex = rows.Count;
                        rows.Add(header(caption(folder)));
                        int added = AddNodes(rows, folder.Items, caption, header, form);
                        if (added == 0)
                            rows.RemoveRange(headerIndex, rows.Count - headerIndex);
                        forms += added;
                        break;
                    case MenuEntry entry when !string.IsNullOrWhiteSpace(entry.ProgId):
                        rows.Add(form(caption(entry), entry.ProgId));
                        forms++;
                        break;
                }
            }
            return forms;
        }
    }
}
