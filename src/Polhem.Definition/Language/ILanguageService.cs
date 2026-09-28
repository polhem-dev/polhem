namespace Polhem.Definition.Language
{
    /// <summary>
    /// Service for resolving localized text from <see cref="LanguageResource"/> data.
    /// Caches at the (lang, namespace) granularity through the underlying
    /// <see cref="Polhem.Definition.Storage.IDefineAccess.GetLanguage(string, string)"/>
    /// cache slot — invalidating a single namespace in one language does not
    /// affect other namespaces or other languages.
    /// </summary>
    /// <remarks>
    /// The service is stateless with respect to the current user — the caller
    /// passes <c>lang</c> explicitly. BO base classes provide a convenience wrapper
    /// that reads <see cref="Identity.SessionInfo.Culture"/> for the current call.
    ///
    /// Every resolving member walks the chain returned by <see cref="LanguageFallback.GetChain"/>:
    /// the requested culture, its parent cultures (<c>en-GB</c> → <c>en</c>), then
    /// <see cref="DefaultLanguage"/> — which an English requested culture leaves out, because English is
    /// the base text. When every culture misses, <see cref="GetLangText(string, string)"/>
    /// returns the full key string so the missing translation is visible, and
    /// <see cref="TryResolveLangText"/> reports a miss so the caller can keep its own base text.
    /// The <c>TryGetLangText</c> overloads are the single-culture primitive the chain is built from
    /// and look in the requested culture only.
    /// </remarks>
    public interface ILanguageService
    {
        /// <summary>
        /// Gets the configured default language, the last culture of the fall-back chain.
        /// </summary>
        /// <remarks>
        /// Read from <see cref="Settings.CommonConfiguration.DefaultLanguage"/> by the server's
        /// service. The default implementation returns an empty string, which drops the hop.
        /// </remarks>
        string DefaultLanguage => string.Empty;

        /// <summary>
        /// Resolves the localized text for the given full key
        /// (<c>"{namespace}.{subKey}"</c> — split on the first <c>.</c>).
        /// Applies the fall-back chain documented on <see cref="ILanguageService"/>.
        /// </summary>
        /// <param name="lang">The BCP-47 language code (e.g. <c>"zh-TW"</c>).</param>
        /// <param name="fullKey">The full key, e.g. <c>"Common.OK"</c>, <c>"Customer.Field.Name.Caption"</c>.</param>
        /// <returns>The localized text, or the input <paramref name="fullKey"/> if all fall-backs miss.</returns>
        string GetLangText(string lang, string fullKey);

        /// <summary>
        /// Resolves the localized text using an explicit namespace and sub-key
        /// (skips the first-dot split that the full-key overload performs).
        /// Applies the fall-back chain documented on <see cref="ILanguageService"/>.
        /// </summary>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="namespace">The resource namespace (matches a file name stem; e.g. <c>"Common"</c>, <c>"Customer"</c>).</param>
        /// <param name="subKey">The sub-key within that namespace (e.g. <c>"OK"</c>, <c>"Field.Name.Caption"</c>).</param>
        /// <returns>The localized text, or <c>"{namespace}.{subKey}"</c> if all fall-backs miss.</returns>
        string GetLangText(string lang, string @namespace, string subKey);

        // ----- Tenant customization overlay -------------------------------------------------
        // These overloads thread an explicit customization code through the lookup. Text overlays
        // per key: a customization resource that contains the requested key wins, otherwise the
        // base value is used, so a customization file holds only the keys it changes. Enums
        // overlay per enum, not per entry — see GetLangEnum below. The base and customization
        // values are never merged into one object. An empty `customizeId` short-circuits straight
        // to the base lookup.
        //
        // Customization-aware overloads use the explicit (namespace, subKey/enumName) shape; the
        // full-key convenience forms have no customization overload because their signature would
        // be indistinguishable from the explicit base overloads (all-string arity collision).

        /// <summary>
        /// Tenant-customization-aware variant of
        /// <see cref="GetLangText(string, string, string)"/> (explicit namespace + sub-key).
        /// </summary>
        /// <param name="customizeId">The tenant customization code; empty resolves against the base layer only.</param>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="namespace">The resource namespace.</param>
        /// <param name="subKey">The sub-key within that namespace.</param>
        /// <returns>The localized text, or <c>"{namespace}.{subKey}"</c> if all fall-backs miss.</returns>
        string GetLangText(string customizeId, string lang, string @namespace, string subKey)
            => GetLangText(lang, @namespace, subKey);

        /// <summary>
        /// Attempts to resolve the localized text in the requested <paramref name="lang"/> only.
        /// **Does not walk the fall-back chain** — call sites that need it use
        /// <see cref="TryResolveLangText"/> or <see cref="GetLangText(string, string)"/> instead.
        /// </summary>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="fullKey">The full key, e.g. <c>"Common.OK"</c>.</param>
        /// <param name="text">The resolved text on hit; empty string on miss.</param>
        /// <returns><c>true</c> on hit; <c>false</c> on miss.</returns>
        bool TryGetLangText(string lang, string fullKey, out string text);

        /// <summary>
        /// Attempts to resolve the localized text in the requested <paramref name="lang"/> only,
        /// using an explicit namespace and sub-key. **Does not walk the fall-back chain.**
        /// </summary>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="namespace">The resource namespace.</param>
        /// <param name="subKey">The sub-key within that namespace.</param>
        /// <param name="text">The resolved text on hit; empty string on miss.</param>
        /// <returns><c>true</c> on hit; <c>false</c> on miss.</returns>
        bool TryGetLangText(string lang, string @namespace, string subKey, out string text);

        /// <summary>
        /// Tenant-customization-aware variant of
        /// <see cref="TryGetLangText(string, string, string, out string)"/>.
        /// </summary>
        /// <param name="customizeId">The tenant customization code; empty resolves against the base layer only.</param>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="namespace">The resource namespace.</param>
        /// <param name="subKey">The sub-key within that namespace.</param>
        /// <param name="text">The resolved text on hit; empty string on miss.</param>
        /// <returns><c>true</c> on hit; <c>false</c> on miss.</returns>
        /// <remarks>
        /// Default implementation ignores <paramref name="customizeId"/> and delegates to the base
        /// overload — services without customization support behave exactly as before.
        /// </remarks>
        bool TryGetLangText(string customizeId, string lang, string @namespace, string subKey, out string text)
            => TryGetLangText(lang, @namespace, subKey, out text);

        /// <summary>
        /// Resolves the localized text through the whole fall-back chain — requested culture, parent
        /// cultures, then <see cref="DefaultLanguage"/> — and reports a miss instead of returning the
        /// key, so the caller can keep its own base text.
        /// </summary>
        /// <param name="customizeId">The tenant customization code; empty resolves against the base layer only.</param>
        /// <param name="lang">The BCP-47 language code; empty starts the chain at the default language.</param>
        /// <param name="namespace">The resource namespace.</param>
        /// <param name="subKey">The sub-key within that namespace.</param>
        /// <param name="text">The resolved text on hit; empty string on miss.</param>
        /// <returns><c>true</c> when some culture of the chain declares the key.</returns>
        /// <remarks>
        /// This is the lookup for text that has a base value of its own: a schema caption, a rule
        /// message, UI text with an English default. The default implementation walks
        /// <see cref="LanguageFallback.GetChain"/> over the single-culture
        /// <see cref="TryGetLangText(string, string, string, string, out string)"/>.
        /// </remarks>
        bool TryResolveLangText(string customizeId, string lang, string @namespace, string subKey, out string text)
        {
            foreach (string culture in LanguageFallback.GetChain(lang, DefaultLanguage))
            {
                if (TryGetLangText(customizeId, culture, @namespace, subKey, out text))
                    return true;
            }
            text = string.Empty;
            return false;
        }

        /// <summary>
        /// Resolves a localized <see cref="LanguageEnum"/> (ordered code/text set) for the
        /// given full name (<c>"{namespace}.{enumName}"</c>). Applies the fall-back chain
        /// when the requested language has no matching enum.
        /// </summary>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="fullName">The full enum name, e.g. <c>"Common.Gender"</c>, <c>"Order.OrderStatus"</c>.</param>
        /// <returns>The matching <see cref="LanguageEnum"/>, or <c>null</c> if not found after fall-back.</returns>
        /// <remarks>
        /// Treat the returned <see cref="LanguageEnum"/> as read-only: in the framework's implementations it
        /// is the cached instance every session shares. Build a new <see cref="LanguageEnum"/> from its entries
        /// when a per-call variant is needed.
        /// </remarks>
        LanguageEnum? GetLangEnum(string lang, string fullName);

        /// <summary>
        /// Resolves a localized <see cref="LanguageEnum"/> using an explicit namespace
        /// and enum name. Applies the fall-back chain when the requested language
        /// has no matching enum.
        /// </summary>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="namespace">The resource namespace.</param>
        /// <param name="enumName">The enum name within that namespace.</param>
        /// <returns>The matching <see cref="LanguageEnum"/>, or <c>null</c> if not found after fall-back.</returns>
        /// <remarks>
        /// Treat the returned <see cref="LanguageEnum"/> as read-only: in the framework's implementations it
        /// is the cached instance every session shares. Build a new <see cref="LanguageEnum"/> from its entries
        /// when a per-call variant is needed.
        /// </remarks>
        LanguageEnum? GetLangEnum(string lang, string @namespace, string enumName);

        /// <summary>
        /// Tenant-customization-aware variant of
        /// <see cref="GetLangEnum(string, string, string)"/> (explicit namespace + enum name).
        /// </summary>
        /// <param name="customizeId">The tenant customization code; empty resolves against the base layer only.</param>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="namespace">The resource namespace.</param>
        /// <param name="enumName">The enum name within that namespace.</param>
        /// <returns>The matching <see cref="LanguageEnum"/>, or <c>null</c> if not found after fall-back.</returns>
        /// <remarks>
        /// The overlay is <b>whole-enum</b>, not per entry: a customization enum of the same name
        /// replaces the base enum outright, so the customization file must list every entry it
        /// wants the option set to have. This is deliberately coarser than the per-key overlay
        /// used for text — an enum is an ordered option set, where a partial merge would leave
        /// both the ordering and the meaning of an omitted entry ambiguous.
        /// <para>
        /// Treat the returned <see cref="LanguageEnum"/> as read-only: in the framework's implementations it
        /// is the cached instance every session shares. Build a new <see cref="LanguageEnum"/> from its entries
        /// when a per-call variant is needed.
        /// </para>
        /// </remarks>
        LanguageEnum? GetLangEnum(string customizeId, string lang, string @namespace, string enumName)
            => GetLangEnum(lang, @namespace, enumName);

        /// <summary>
        /// Convenience: resolves a single localized text for a code within a
        /// <see cref="LanguageEnum"/>. Applies the fall-back chain.
        /// </summary>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="fullName">The full enum name, e.g. <c>"Common.Gender"</c>.</param>
        /// <param name="code">The code to look up within the enum.</param>
        /// <returns>The localized text on hit; <c>null</c> when the enum or code is missing after fall-back.</returns>
        string? GetLangEnumText(string lang, string fullName, string code);

        /// <summary>
        /// Tenant-customization-aware variant of
        /// <see cref="GetLangEnumText(string, string, string)"/>.
        /// </summary>
        /// <param name="customizeId">The tenant customization code; empty resolves against the base layer only.</param>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="fullName">The full enum name, e.g. <c>"Common.Gender"</c>.</param>
        /// <param name="code">The code to look up within the enum.</param>
        /// <returns>The localized text on hit; <c>null</c> when the enum or code is missing after fall-back.</returns>
        string? GetLangEnumText(string customizeId, string lang, string fullName, string code)
            => GetLangEnumText(lang, fullName, code);
    }
}
