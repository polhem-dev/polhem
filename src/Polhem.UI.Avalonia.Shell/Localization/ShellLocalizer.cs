using System.Globalization;
using System.Resources;

namespace Polhem.UI.Avalonia.Shell.Localization
{
    /// <summary>
    /// The source of the shell's own text, in the interface language. The views bind to it through
    /// <see cref="ShellTextExtension"/>, so a language switch updates the open screen in place.
    /// </summary>
    /// <remarks>
    /// Backed by <c>Resources/ShellStrings.resx</c> (English, the neutral resources) and its translations. A key with
    /// no translation falls back to English; a key missing everywhere shows the key itself, so the gap is visible.
    /// <para>
    /// The language is the shell's, not the process culture: before sign-in it is the viewer's choice, and after
    /// sign-in the shell sets it to the account's culture, which <see cref="Polhem.UI.ApiBridge.ClientInfo"/> has
    /// made the process culture.
    /// </para>
    /// </remarks>
    internal sealed class ShellLocalizer
    {
        private static readonly ResourceManager s_resources = new(
            "Polhem.UI.Avalonia.Shell.Resources.ShellStrings", typeof(ShellLocalizer).Assembly);

        private CultureInfo _culture = CultureInfo.GetCultureInfo(ShellLanguage.English);

        /// <summary>Gets the instance the views and view models read from.</summary>
        public static ShellLocalizer Current { get; } = new();

        /// <summary>Raised after <see cref="Culture"/> changes to a different language.</summary>
        public event EventHandler? CultureChanged;

        /// <summary>Gets or sets the language the text is returned in.</summary>
        public CultureInfo Culture
        {
            get => _culture;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                if (string.Equals(_culture.Name, value.Name, StringComparison.OrdinalIgnoreCase)) return;

                _culture = value;
                CultureChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Returns the text of <paramref name="key"/> in <see cref="Culture"/>.</summary>
        public string this[string key] => Get(key, _culture);

        /// <summary>Returns the text of <paramref name="key"/> formatted with <paramref name="args"/>.</summary>
        public string Format(string key, params object?[] args)
            => string.Format(_culture, this[key], args);

        /// <summary>Returns the text of <paramref name="key"/> in <paramref name="culture"/>.</summary>
        internal static string Get(string key, CultureInfo culture)
            => string.IsNullOrEmpty(key) ? string.Empty : s_resources.GetString(key, culture) ?? key;
    }
}
