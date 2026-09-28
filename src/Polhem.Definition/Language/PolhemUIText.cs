using Microsoft.Extensions.Localization;

namespace Polhem.Definition.Language
{
    /// <summary>
    /// The framework's own UI text — button captions, placeholders and labels the built-in views
    /// render — and the language namespace it is translated in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The type doubles as the marker of <see cref="LanguageResourceStringLocalizer{T}"/>, whose
    /// namespace is the marker's name: an <see cref="IStringLocalizer{T}"/> of this type resolves keys
    /// in the <see cref="Namespace"/> namespace. The UI heads resolve their text through such a
    /// localizer over <see cref="FrameworkLanguageService"/>, which answers from the host's own
    /// resources first and from the translations shipped inside the framework second.
    /// </para>
    /// <para>
    /// The English text is the base: <see cref="DefaultTexts"/> holds it, and it is what
    /// <see cref="Get"/> returns when no culture of the fall-back chain translates a key. A host
    /// overrides or adds a language by supplying its own language resource of that namespace or
    /// its own localizer. <c>PolhemTextResourceTests</c> keeps the keys, the English defaults and the
    /// shipped translations in step.
    /// </para>
    /// </remarks>
    public sealed class PolhemUIText
    {
        /// <summary>The language namespace of the framework's UI text.</summary>
        public const string Namespace = nameof(PolhemUIText);

        /// <summary>Key of the loading indicator text.</summary>
        public const string Loading = "Loading";
        /// <summary>Key of the empty-list text.</summary>
        public const string NoData = "NoData";
        /// <summary>Key of the view-record command.</summary>
        public const string View = "View";
        /// <summary>Key of the new-record command.</summary>
        public const string New = "New";
        /// <summary>Key of the add-row command.</summary>
        public const string Add = "Add";
        /// <summary>Key of the edit command.</summary>
        public const string Edit = "Edit";
        /// <summary>Key of the delete command.</summary>
        public const string Delete = "Delete";
        /// <summary>Key of the save command.</summary>
        public const string Save = "Save";
        /// <summary>Key of the cancel command.</summary>
        public const string Cancel = "Cancel";
        /// <summary>Key of the back command.</summary>
        public const string Back = "Back";
        /// <summary>Key of the confirm command.</summary>
        public const string Ok = "OK";
        /// <summary>Key of the search command and placeholder.</summary>
        public const string Search = "Search";
        /// <summary>Key of the unsaved-changes marker.</summary>
        public const string Unsaved = "Unsaved";
        /// <summary>Key of the text a true Boolean value displays as.</summary>
        public const string True = "True";
        /// <summary>Key of the text a false Boolean value displays as.</summary>
        public const string False = "False";
        /// <summary>Key of the user-id label of the sign-in panel.</summary>
        public const string UserId = "UserId";
        /// <summary>Key of the password label of the sign-in panel.</summary>
        public const string Password = "Password";
        /// <summary>Key of the sign-in command.</summary>
        public const string SignIn = "SignIn";
        /// <summary>Key of the message shown when a sign-in returns no access token.</summary>
        public const string SignInEmptyToken = "SignInEmptyToken";
        /// <summary>
        /// Key of the prompt shown when a save is held back because required fields are empty;
        /// argument 0 is the list of their captions.
        /// </summary>
        public const string RequiredFieldsEmpty = "RequiredFieldsEmpty";

        private PolhemUIText()
        {
        }

        /// <summary>
        /// Gets the English text of every key, the base the translations are made from.
        /// </summary>
        public static IReadOnlyDictionary<string, string> DefaultTexts { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Loading] = "Loading…",
            [NoData] = "No data.",
            [View] = "View",
            [New] = "New",
            [Add] = "Add",
            [Edit] = "Edit",
            [Delete] = "Delete",
            [Save] = "Save",
            [Cancel] = "Cancel",
            [Back] = "Back",
            [Ok] = "OK",
            [Search] = "Search",
            [Unsaved] = "● unsaved",
            [True] = "Yes",
            [False] = "No",
            [UserId] = "User ID",
            [Password] = "Password",
            [SignIn] = "Sign in",
            [SignInEmptyToken] = "Login failed: the server returned an empty access token.",
            [RequiredFieldsEmpty] = "Fill in the required fields: {0}",
        };

        /// <summary>
        /// Returns the text of <paramref name="key"/> in the localizer's culture, or the English
        /// default when no culture of the fall-back chain translates it.
        /// </summary>
        /// <param name="localizer">The localizer to resolve through.</param>
        /// <param name="key">One of the key constants of this type.</param>
        /// <returns>The localized text, else the English default, else the key itself.</returns>
        public static string Get(IStringLocalizer localizer, string key)
        {
            ArgumentNullException.ThrowIfNull(localizer);
            ArgumentNullException.ThrowIfNull(key);

            var localized = localizer[key];
            if (!localized.ResourceNotFound)
                return localized.Value;
            return DefaultTexts.TryGetValue(key, out string? text) ? text : key;
        }
    }
}
