using System.Globalization;
using Avalonia;
using Semi.Avalonia;

namespace Polhem.UI.Avalonia.Shell.Localization
{
    /// <summary>
    /// Puts the Semi theme's own text (such as the date picker's segment placeholders) in the language closest to
    /// the shell's.
    /// </summary>
    /// <remarks>
    /// Semi matches a culture exactly and otherwise keeps Simplified Chinese, so a neutral language such as <c>en</c>,
    /// or a system culture such as <c>en-TW</c>, would leave its placeholders in Chinese. DefineEditor carries the same
    /// lookup in a copy of this class under the same name, because it does not reference the shell.
    /// </remarks>
    internal static class SemiLocale
    {
        /// <summary>The language used when nothing in the culture's chain is one Semi ships.</summary>
        public static readonly CultureInfo Fallback = CultureInfo.GetCultureInfo("en-US");

        /// <summary>
        /// Writes Semi's text for <paramref name="culture"/> into the application's resources, where it takes
        /// precedence over the theme's own default. Calling it again switches the language in place.
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
        /// Returns the cultures to try, most general first: the fallback, then for each level of the culture's chain
        /// from the neutral language down, that level's specific culture and the level itself.
        /// </summary>
        /// <param name="culture">The interface culture to follow.</param>
        internal static IEnumerable<CultureInfo> Candidates(CultureInfo culture)
        {
            ArgumentNullException.ThrowIfNull(culture);
            return EnumerateCandidates(culture);
        }

        private static IEnumerable<CultureInfo> EnumerateCandidates(CultureInfo culture)
        {
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
}
