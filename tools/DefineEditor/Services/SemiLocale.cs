using System.Globalization;
using Avalonia;
using Semi.Avalonia;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Puts the Semi theme's own text (such as the date picker's segment placeholders in the FormLayout preview) in the
/// language closest to the editor's.
/// </summary>
/// <remarks>
/// Semi matches a culture exactly and otherwise keeps Simplified Chinese, so the editor's neutral <c>en</c> would
/// leave its placeholders in Chinese. <c>Polhem.UI.Avalonia.Shell</c> carries the same lookup in its own
/// <c>SemiLocale</c>; the editor does not reference the shell, so a change to the lookup belongs in both.
/// </remarks>
public static class SemiLocale
{
    /// <summary>The language used when nothing in the culture's chain is one Semi ships.</summary>
    public static readonly CultureInfo Fallback = CultureInfo.GetCultureInfo("en-US");

    /// <summary>
    /// Writes Semi's text for <paramref name="culture"/> into the application's resources, where it takes precedence
    /// over the theme's own default. Calling it again switches the language in place.
    /// </summary>
    /// <param name="application">The application whose resources receive the text.</param>
    /// <param name="culture">The interface culture to follow.</param>
    public static void Apply(Application application, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(application);

        // Semi ignores a culture it does not ship, so applying every candidate from the most general to the most
        // specific leaves the closest one it has.
        foreach (var candidate in Candidates(culture))
            SemiTheme.OverrideLocaleResources(application, candidate);
    }

    /// <summary>
    /// Returns the cultures to try, most general first: the fallback, then for each level of the culture's chain from
    /// the neutral language down, that level's specific culture and the level itself.
    /// </summary>
    /// <param name="culture">The interface culture to follow.</param>
    public static IEnumerable<CultureInfo> Candidates(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        var chain = new List<CultureInfo>();
        for (var level = culture; !string.IsNullOrEmpty(level.Name); level = level.Parent)
            chain.Add(level);
        chain.Reverse();

        yield return Fallback;
        foreach (var level in chain)
        {
            yield return CultureInfo.CreateSpecificCulture(level.Name);
            yield return level;
        }
    }
}
