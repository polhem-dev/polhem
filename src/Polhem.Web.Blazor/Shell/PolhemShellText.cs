using System.Globalization;
using System.Resources;

namespace Polhem.Web.Blazor.Shell
{
    /// <summary>
    /// The shell's own text in the circuit's UI culture, from <c>Resources/PolhemShellStrings.resx</c> (English, the
    /// neutral resources) and its translations.
    /// </summary>
    /// <remarks>
    /// A key with no translation falls back to English; a key missing everywhere shows the key itself, so the gap is
    /// visible. The components and the definitions keep their own text sources.
    /// </remarks>
    public static class PolhemShellText
    {
        /// <summary>The caption of the company input.</summary>
        public const string CompanyId = nameof(CompanyId);
        /// <summary>The message for an empty company.</summary>
        public const string CompanyEmpty = nameof(CompanyEmpty);
        /// <summary>The message for a company that could not be entered.</summary>
        public const string EnterCompanyFailed = nameof(EnterCompanyFailed);
        /// <summary>The accessible name of the language switch.</summary>
        public const string Language = nameof(Language);
        /// <summary>The note under the language switch.</summary>
        public const string LanguageHint = nameof(LanguageHint);
        /// <summary>The accessible name of the menu button.</summary>
        public const string ToggleMenu = nameof(ToggleMenu);
        /// <summary>The caption of the sign-out command.</summary>
        public const string SignOut = nameof(SignOut);
        /// <summary>The message for a menu that could not be loaded.</summary>
        public const string MenuLoadFailed = nameof(MenuLoadFailed);
        /// <summary>The note for a menu with no form.</summary>
        public const string MenuEmpty = nameof(MenuEmpty);

        private static readonly ResourceManager s_resources = new(
            "Polhem.Web.Blazor.Shell.Resources.PolhemShellStrings", typeof(PolhemShellText).Assembly);

        /// <summary>Returns the text of <paramref name="key"/> in the current UI culture.</summary>
        public static string Get(string key) => s_resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

        /// <summary>Returns the text of <paramref name="key"/> formatted with <paramref name="args"/>.</summary>
        public static string Format(string key, params object?[] args)
            => string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }
}
