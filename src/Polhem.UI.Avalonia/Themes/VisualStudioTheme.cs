using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Polhem.UI.Avalonia.Themes
{
    /// <summary>
    /// Recolours the Semi theme with VS Code's "Dark (Visual Studio)" and "Light (Visual Studio)" colours, with body
    /// text dimmed and drawn at regular weight as VS Code does. The definition editor and the shell share it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The theme is a set of Semi resource keys, so this package takes no dependency on Semi; it changes nothing
    /// unless Semi is the application's theme. Light and dark follow <see cref="Application.RequestedThemeVariant"/>.
    /// </para>
    /// <para>
    /// Semi's control keys are static-resource aliases of its palette, resolved inside Semi's own dictionaries,
    /// so a palette key overridden in the application's resources reaches only what asks for that key by name. The
    /// aliases share the palette's brush instances, though, so <see cref="Apply"/> also writes each colour into those
    /// instances, which recolours every Semi control without listing its keys. The text brightness and weights are
    /// overridden by Semi's control key names only; <c>Themes/VisualStudioText.axaml</c> explains why.
    /// </para>
    /// </remarks>
    public static class VisualStudioTheme
    {
        // Semi keeps a separate copy of its Light dictionary for the Default variant.
        private static readonly (ThemeVariant Semi, ThemeVariant Ours)[] s_variants =
        [
            (ThemeVariant.Default, ThemeVariant.Light),
            (ThemeVariant.Light, ThemeVariant.Light),
            (ThemeVariant.Dark, ThemeVariant.Dark),
        ];

        /// <summary>
        /// Applies the theme to <paramref name="app"/>: merges its resources into the application's and repaints
        /// Semi's palette brushes.
        /// </summary>
        /// <param name="app">The application.</param>
        /// <remarks>
        /// Call it once, after the Semi styles have been added to <see cref="Application.Styles"/>; the palette
        /// brushes it repaints are found there. Semi's palette brushes are shared by the whole process, so the
        /// repaint applies to every window.
        /// </remarks>
        public static void Apply(Application app)
        {
            ArgumentNullException.ThrowIfNull(app);

            var colors = new VisualStudioColors();
            app.Resources.MergedDictionaries.Add(new VisualStudioText());
            app.Resources.MergedDictionaries.Add(colors);

            foreach (var (semiVariant, ourVariant) in s_variants)
            {
                if (!colors.ThemeDictionaries.TryGetValue(ourVariant, out var provider) || provider is not ResourceDictionary ours)
                    continue;

                foreach (var key in ours.Keys.OfType<string>().Where(k => k.StartsWith("SemiColor", StringComparison.Ordinal)))
                {
                    if (ours.TryGetValue(key, out var value) && value is ISolidColorBrush color
                        && app.Styles.TryGetResource(key, semiVariant, out var semiValue) && semiValue is SolidColorBrush semi)
                    {
                        // The brushes here compile to immutable ones; Semi's palette brushes are mutable instances.
                        semi.Color = color.Color;
                        semi.Opacity = color.Opacity;
                    }
                }
            }
        }
    }
}
