using System.Globalization;

namespace Polhem.UI.Avalonia.Shell.Localization
{
    /// <summary>
    /// The interface languages the shell ships text for, and how the one it starts with is chosen.
    /// </summary>
    internal static class ShellLanguage
    {
        /// <summary>English, the neutral resources.</summary>
        public const string English = "en";

        /// <summary>Traditional Chinese (Taiwan).</summary>
        public const string TraditionalChinese = "zh-TW";

        /// <summary>
        /// Gets the languages offered by the language switch, each named in its own language.
        /// </summary>
        public static IReadOnlyList<ShellLanguageOption> Options { get; } =
        [
            new(English, "English"),
            new(TraditionalChinese, "繁體中文"),
        ];

        /// <summary>The command-line argument that sets the language of one run: <c>Language=en</c>.</summary>
        public const string ArgumentName = "Language";

        /// <summary>
        /// Chooses the language the shell starts with: the language requested for this run, else the remembered
        /// choice, each only when it is one the shell offers; otherwise the system language mapped onto the offered
        /// ones, otherwise English.
        /// </summary>
        /// <param name="requested">The language named on the command line; empty when none was.</param>
        /// <param name="remembered">The stored choice; empty when the viewer never chose.</param>
        /// <param name="system">The operating system's interface culture.</param>
        public static string ResolveInitial(string? requested, string? remembered, CultureInfo system)
        {
            ArgumentNullException.ThrowIfNull(system);

            return Find(requested) ?? Find(remembered) ?? Map(system) ?? English;
        }

        /// <summary>
        /// Returns the value of the <c>Language=</c> argument in <paramref name="args"/>, or an empty string.
        /// </summary>
        /// <param name="args">The command line; the first element, the program, is skipped.</param>
        public static string FindArgument(IReadOnlyList<string> args)
        {
            ArgumentNullException.ThrowIfNull(args);

            const string prefix = ArgumentName + "=";
            return args.Skip(1)
                .Where(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(a => a[prefix.Length..].Trim())
                .LastOrDefault() ?? string.Empty;
        }

        private static string? Find(string? code)
            => Options.FirstOrDefault(o => string.Equals(o.Code, code, StringComparison.OrdinalIgnoreCase))?.Code;

        /// <summary>
        /// Maps a culture onto an offered language, or returns <see langword="null"/> when none fits.
        /// </summary>
        internal static string? Map(CultureInfo culture)
        {
            // Walk up to the neutral parent: zh-Hant-TW → zh-Hant → zh, en-GB → en.
            for (var c = culture; !string.IsNullOrEmpty(c.Name); c = c.Parent)
            {
                switch (c.Name)
                {
                    case "en":
                        return English;
                    case "zh-TW" or "zh-HK" or "zh-MO" or "zh-Hant":
                        return TraditionalChinese;
                    case "zh-CN" or "zh-SG" or "zh-Hans":
                        // Simplified Chinese is not offered; English is the closer neutral fallback than the
                        // traditional script.
                        return null;
                }
            }

            return null;
        }
    }
}
