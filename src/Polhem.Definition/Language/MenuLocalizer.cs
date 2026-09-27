using System.Globalization;
using Polhem.Definition.Settings;

namespace Polhem.Definition.Language
{
    /// <summary>
    /// Resolves the display caption of a menu node from <see cref="LanguageResource"/> data in the
    /// <c>Menu</c> namespace, keyed by the node's <see cref="MenuNodeBase.Id"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><see cref="MenuFolder"/> ← <c>Folder.{Id}.Caption</c></description></item>
    /// <item><description><see cref="MenuEntry"/> ← <c>Entry.{Id}.Caption</c></description></item>
    /// </list>
    /// <para>
    /// Keyed by <see cref="MenuNodeBase.Id"/> rather than by program, because the same program may
    /// appear in several places under different titles. Keys resolve through the language fall-back
    /// chain (<see cref="LanguageFallback"/>); a key no culture declares returns the node's own
    /// <see cref="MenuNodeBase.Caption"/>, the authoring-language base text.
    /// </para>
    /// <para>
    /// Unlike <see cref="FormSchemaLocalizer"/> this reads and never writes: a menu comes from the
    /// process-wide definition cache and has no clone, so the caption is resolved at the moment a
    /// shell renders the node.
    /// </para>
    /// </remarks>
    public sealed class MenuLocalizer
    {
        /// <summary>The language namespace of menu captions.</summary>
        public const string Namespace = "Menu";

        /// <summary>Sub-key template for a folder caption. <c>{0}</c> is the node id.</summary>
        public const string FolderCaptionKeyFormat = "Folder.{0}.Caption";

        /// <summary>Sub-key template for an entry caption. <c>{0}</c> is the node id.</summary>
        public const string EntryCaptionKeyFormat = "Entry.{0}.Caption";

        private readonly ILanguageService _languageService;

        /// <summary>
        /// Initializes a new <see cref="MenuLocalizer"/>.
        /// </summary>
        /// <param name="languageService">The language resource service used to resolve sub-keys.</param>
        public MenuLocalizer(ILanguageService languageService)
        {
            _languageService = languageService ?? throw new ArgumentNullException(nameof(languageService));
        }

        /// <summary>
        /// Returns the caption of <paramref name="node"/> in <paramref name="lang"/>.
        /// </summary>
        /// <param name="node">The folder or entry.</param>
        /// <param name="lang">The BCP-47 culture; empty starts the chain at the default language.</param>
        /// <returns>The translated caption, else the node's own caption.</returns>
        public string GetCaption(MenuNodeBase node, string lang)
        {
            ArgumentNullException.ThrowIfNull(node);
            if (string.IsNullOrWhiteSpace(node.Id)) { return node.Caption; }

            string format = node is MenuFolder ? FolderCaptionKeyFormat : EntryCaptionKeyFormat;
            string subKey = string.Format(CultureInfo.InvariantCulture, format, node.Id);
            return _languageService.TryResolveLangText(string.Empty, lang, Namespace, subKey, out string text)
                ? text
                : node.Caption;
        }
    }
}
