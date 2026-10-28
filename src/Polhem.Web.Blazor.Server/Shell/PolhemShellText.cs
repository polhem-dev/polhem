using System.Globalization;
using System.Resources;

namespace Polhem.Web.Blazor.Server.Shell
{
    /// <summary>
    /// The shell's own text in the circuit's UI culture, from <c>Resources/PolhemShellStrings.resx</c> (English, the
    /// neutral resources) and its translations.
    /// </summary>
    /// <remarks>
    /// A key with no translation falls back to English; a key missing everywhere shows the key itself, so the gap is
    /// visible. The components and the definitions keep their own text sources.
    /// </remarks>
    internal static class PolhemShellText
    {
        public const string CompanyId = nameof(CompanyId);
        public const string CompanyEmpty = nameof(CompanyEmpty);
        public const string EnterCompanyFailed = nameof(EnterCompanyFailed);
        public const string Language = nameof(Language);
        public const string LanguageHint = nameof(LanguageHint);
        public const string ToggleMenu = nameof(ToggleMenu);
        public const string SignOut = nameof(SignOut);
        public const string MenuLoadFailed = nameof(MenuLoadFailed);

        private static readonly ResourceManager s_resources = new(
            "Polhem.Web.Blazor.Server.Shell.Resources.PolhemShellStrings", typeof(PolhemShellText).Assembly);

        /// <summary>Returns the text of <paramref name="key"/> in the current UI culture.</summary>
        public static string Get(string key) => s_resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

        /// <summary>Returns the text of <paramref name="key"/> formatted with <paramref name="args"/>.</summary>
        public static string Format(string key, params object?[] args)
            => string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }
}
