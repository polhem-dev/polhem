using Microsoft.Extensions.Localization;
using Polhem.Api.Client;
using Polhem.Definition.Language;

namespace Polhem.UI.Avalonia
{
    /// <summary>
    /// The source of the text this package's views and controls render: button captions,
    /// placeholders and labels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The keys and their English base text are <see cref="PolhemUIText"/>; the default
    /// <see cref="Localizer"/> is a <see cref="LanguageResourceStringLocalizer{T}"/> over
    /// <see cref="FrameworkLanguageService"/>, which serves the translations shipped with the
    /// framework in the UI culture — the signed-in user's culture once
    /// <c>ClientInfo.ApplyLoginResult</c> has run — through the language fall-back chain, ending at
    /// the deployment's default language (<see cref="ApiClientInfo.DefaultLanguage"/>).
    /// </para>
    /// <para>
    /// A host replaces <see cref="Localizer"/> to reword the text or add a language, typically with
    /// the <c>IStringLocalizer&lt;PolhemUIText&gt;</c> from its own service container. A key the
    /// replacement does not declare still shows the English base text. The text is read when a
    /// control is built, so a replacement or a language switch applies to views opened afterwards.
    /// </para>
    /// </remarks>
    public static class UIText
    {
        /// <summary>
        /// Gets or sets the localizer the built-in views and controls read their text from.
        /// </summary>
        public static IStringLocalizer Localizer { get; set; } =
            new LanguageResourceStringLocalizer<PolhemUIText>(
                new FrameworkLanguageService(null, static () => ApiClientInfo.DefaultLanguage));

        /// <summary>
        /// Returns the text of <paramref name="key"/>, one of the <see cref="PolhemUIText"/> keys.
        /// </summary>
        internal static string Get(string key) => PolhemUIText.Get(Localizer, key);
    }
}
