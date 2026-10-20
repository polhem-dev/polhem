using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Repaints Semi's palette with the colours of <c>Styles/VisualStudioTheme.axaml</c>.
/// </summary>
/// <remarks>
/// Semi's control keys are <c>StaticResource</c> aliases of its palette, resolved inside Semi's own dictionaries, so
/// an override of a palette key in the application's resources reaches only what asks for that key by name (see
/// <c>Styles/TextEmphasis.axaml</c>). The aliases share the palette's brush instances, though, so writing the colour
/// into those instances recolours every Semi control without listing its keys, and keeps working when Semi adds or
/// renames control keys.
/// </remarks>
internal static class VisualStudioPalette
{
    private static readonly Uri ThemeUri = new("avares://Polhem.DefineEditor/Styles/VisualStudioTheme.axaml");

    // Semi keeps a separate copy of its Light dictionary for the Default variant.
    private static readonly (ThemeVariant Semi, ThemeVariant Ours)[] Variants =
    [
        (ThemeVariant.Default, ThemeVariant.Light),
        (ThemeVariant.Light, ThemeVariant.Light),
        (ThemeVariant.Dark, ThemeVariant.Dark),
    ];

    /// <summary>Writes every <c>SemiColor*</c> brush of the theme file into Semi's brush of the same key.</summary>
    /// <param name="app">The application, after its XAML has loaded.</param>
    public static void Apply(Application app)
    {
        // Loaded on its own: compiled XAML inlines the application's ResourceInclude, so the merged copy cannot be
        // told apart from the other merged dictionaries.
        if (AvaloniaXamlLoader.Load(ThemeUri) is not ResourceDictionary theme) return;

        foreach (var (semiVariant, ourVariant) in Variants)
        {
            if (!theme.ThemeDictionaries.TryGetValue(ourVariant, out var provider) || provider is not ResourceDictionary ours)
                continue;
            foreach (var key in ours.Keys.OfType<string>().Where(k => k.StartsWith("SemiColor", StringComparison.Ordinal)))
            {
                if (ours.TryGetValue(key, out var value) && value is ISolidColorBrush color
                    && app.Styles.TryGetResource(key, semiVariant, out var semiValue) && semiValue is SolidColorBrush semi)
                {
                    // Our brushes compile to immutable ones; Semi's palette brushes are mutable instances.
                    semi.Color = color.Color;
                    semi.Opacity = color.Opacity;
                }
            }
        }
    }
}
