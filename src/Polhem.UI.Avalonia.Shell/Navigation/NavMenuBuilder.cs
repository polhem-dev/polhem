using Polhem.Definition.Language;
using Polhem.Definition.Settings;
using Polhem.UI.Core.Navigation;

namespace Polhem.UI.Avalonia.Shell.Navigation
{
    /// <summary>
    /// Turns the server's <see cref="MenuSettings"/> into the flat list of rows the navigation menu shows.
    /// </summary>
    /// <remarks>
    /// The rule is <see cref="MenuRows.Flatten"/>, which every UI head shares; this class only supplies the rows.
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

            return MenuRows.Flatten(nodes, caption, NavItem.Header, NavItem.Form);
        }
    }
}
