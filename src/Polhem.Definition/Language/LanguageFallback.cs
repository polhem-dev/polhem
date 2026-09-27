namespace Polhem.Definition.Language
{
    /// <summary>
    /// The one fall-back chain every language lookup walks: the requested culture, then its parent
    /// cultures, then the configured default language — except that an English culture ends the chain.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Captions, enums, UI text and server messages all resolve through this chain, so a user whose
    /// culture has no resource file sees the same language everywhere. When every culture in the
    /// chain misses, the caller keeps its base text: the caption written in the
    /// <see cref="Polhem.Definition.Forms.FormSchema"/>, or the English text written in code.
    /// </para>
    /// <para>
    /// Parent cultures are found by dropping the last subtag of the BCP-47 tag (<c>en-GB</c> →
    /// <c>en</c>, <c>zh-Hant-TW</c> → <c>zh-Hant</c> → <c>zh</c>), the "lookup" scheme of RFC 4647.
    /// It is done on the string rather than through <see cref="System.Globalization.CultureInfo.Parent"/>
    /// so the chain is the same on every runtime, including those that run with invariant
    /// globalization and know no cultures at all. A single-letter subtag (an extension or private-use
    /// marker such as <c>x</c>) is dropped together with the subtag that follows it.
    /// </para>
    /// <para>
    /// IMPORTANT: when the requested culture or one of its parents is English (<c>en</c>, <c>en-*</c>),
    /// the chain stops there and the default language is not added. The base text of the framework's
    /// definitions and of its own UI text and messages is English, so for an English user "no English
    /// resource" means "show the base text"; continuing to a <c>zh-TW</c> default would put Chinese in
    /// front of an <c>en-GB</c> user. Any other culture keeps the full chain, so a <c>fr-FR</c> user of
    /// a <c>zh-TW</c> deployment still gets <c>zh-TW</c>. <c>LanguageFallbackTests</c> pins both.
    /// </para>
    /// <para>
    /// The chain does not reach sibling cultures: <c>en-GB</c> falls back to <c>en</c>, not to
    /// <c>en-US</c>. A deployment that ships <c>en-US</c> resources and serves <c>en-GB</c> users
    /// either adds an <c>en</c> resource or relies on the default language and the base text.
    /// </para>
    /// </remarks>
    public static class LanguageFallback
    {
        /// <summary>
        /// Returns the cultures to try, in order, for <paramref name="lang"/>.
        /// </summary>
        /// <param name="lang">The requested BCP-47 culture; empty when the caller has none.</param>
        /// <param name="defaultLanguage">The configured default language; empty skips that hop.</param>
        /// <returns>
        /// The requested culture, its parents, then the default language, without duplicates
        /// (compared case-insensitively); the default language is left out when the requested culture
        /// is English. Empty when both inputs are empty.
        /// </returns>
        public static IReadOnlyList<string> GetChain(string? lang, string? defaultLanguage)
        {
            var chain = new List<string>();
            bool reachedEnglish = AddWithParents(chain, lang);
            if (!reachedEnglish)
                AddDistinct(chain, defaultLanguage?.Trim());
            return chain;
        }

        /// <summary>
        /// Says whether <paramref name="culture"/> is English (<c>en</c> or <c>en-*</c>), the language
        /// the base text is written in.
        /// </summary>
        /// <param name="culture">A BCP-47 culture name.</param>
        public static bool IsBaseLanguage(string? culture)
        {
            string value = culture?.Trim() ?? string.Empty;
            return string.Equals(value, "en", StringComparison.OrdinalIgnoreCase)
                   || value.StartsWith("en-", StringComparison.OrdinalIgnoreCase)
                   || value.StartsWith("en_", StringComparison.OrdinalIgnoreCase);
        }

        // Returns true when the walk reached an English culture, which ends the chain.
        private static bool AddWithParents(List<string> chain, string? lang)
        {
            string current = lang?.Trim() ?? string.Empty;
            bool reachedEnglish = false;
            while (current.Length > 0)
            {
                AddDistinct(chain, current);
                reachedEnglish |= IsBaseLanguage(current);
                current = GetParent(current);
            }
            return reachedEnglish;
        }

        private static string GetParent(string tag)
        {
            int dash = tag.LastIndexOfAny(['-', '_']);
            if (dash <= 0) { return string.Empty; }

            string parent = tag.Substring(0, dash);
            // RFC 4647 lookup: a trailing singleton (extension / private-use marker) cannot stand on
            // its own, so it goes with the subtag it introduced.
            int previous = parent.LastIndexOfAny(['-', '_']);
            if (previous > 0 && parent.Length - previous - 1 == 1)
            {
                parent = parent.Substring(0, previous);
            }
            return parent;
        }

        private static void AddDistinct(List<string> chain, string? culture)
        {
            if (string.IsNullOrEmpty(culture)) { return; }
            if (chain.Exists(existing => string.Equals(existing, culture, StringComparison.OrdinalIgnoreCase))) { return; }
            chain.Add(culture);
        }
    }
}
