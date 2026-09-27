using System.Collections.Concurrent;
using System.Reflection;
using Polhem.Base.Serialization;

namespace Polhem.Definition.Language
{
    /// <summary>
    /// An <see cref="ILanguageService"/> that answers from a host's own service first and from the
    /// translations shipped inside the framework second.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The framework's own text — <see cref="PolhemUIText"/> and <see cref="PolhemMessages"/> — ships
    /// translated as resources embedded in this assembly, so it is localized without a deployment
    /// copying anything into its definition path. A host that wants other wording, or another
    /// language, supplies a language resource of the same namespace through its own
    /// <see cref="ILanguageService"/> (on the server, a file under <c>Language/{lang}/</c> of the
    /// definition path); for each culture its key wins over the shipped one.
    /// </para>
    /// <para>
    /// Every culture of the fall-back chain consults both sources before the next culture is tried,
    /// so a host translation in the user's parent culture does not lose to a shipped translation in
    /// the default language.
    /// </para>
    /// </remarks>
    public sealed class FrameworkLanguageService : ILanguageService
    {
        private const string ResourcePrefix = "Polhem.Definition.Language/";

        private static readonly Assembly s_assembly = typeof(FrameworkLanguageService).Assembly;
        private static readonly ConcurrentDictionary<string, LanguageResource?> s_builtIn = new(StringComparer.OrdinalIgnoreCase);

        private readonly ILanguageService? _inner;
        private readonly Func<string>? _defaultLanguageProvider;

        /// <summary>
        /// Initializes a new <see cref="FrameworkLanguageService"/> whose default language is the
        /// inner service's.
        /// </summary>
        /// <param name="inner">The host's own service, consulted first; <c>null</c> for the shipped translations only.</param>
        public FrameworkLanguageService(ILanguageService? inner)
        {
            _inner = inner;
        }

        /// <summary>
        /// Initializes a new <see cref="FrameworkLanguageService"/> with an explicit source of the
        /// default language, for a client that learns it from the server.
        /// </summary>
        /// <param name="inner">The host's own service, consulted first; <c>null</c> for the shipped translations only.</param>
        /// <param name="defaultLanguageProvider">Returns the default language at each lookup; an empty result drops the hop.</param>
        public FrameworkLanguageService(ILanguageService? inner, Func<string> defaultLanguageProvider)
        {
            _inner = inner;
            _defaultLanguageProvider = defaultLanguageProvider ?? throw new ArgumentNullException(nameof(defaultLanguageProvider));
        }

        /// <inheritdoc/>
        public string DefaultLanguage
            => _defaultLanguageProvider?.Invoke() ?? _inner?.DefaultLanguage ?? string.Empty;

        /// <summary>
        /// Returns the translation of <paramref name="namespace"/> in <paramref name="lang"/> that
        /// ships inside the framework, or <c>null</c> when none does.
        /// </summary>
        /// <param name="lang">The BCP-47 culture.</param>
        /// <param name="namespace">The language namespace.</param>
        /// <returns>The shared, cached resource; callers must not modify it.</returns>
        public static LanguageResource? GetBuiltIn(string lang, string @namespace)
        {
            if (string.IsNullOrWhiteSpace(lang) || string.IsNullOrWhiteSpace(@namespace)) { return null; }
            return s_builtIn.GetOrAdd($"{lang}/{@namespace}", static key => Load(key));
        }

        private static LanguageResource? Load(string key)
        {
            // Manifest resource names are case-sensitive, and a culture can arrive in any casing.
            string wanted = $"{ResourcePrefix}{key}.Language.xml";
            string? name = Array.Find(s_assembly.GetManifestResourceNames(),
                n => string.Equals(n, wanted, StringComparison.OrdinalIgnoreCase));
            if (name is null) { return null; }

            using var stream = s_assembly.GetManifestResourceStream(name);
            if (stream is null) { return null; }
            using var reader = new StreamReader(stream);
            return XmlCodec.Deserialize<LanguageResource>(reader.ReadToEnd());
        }

        /// <inheritdoc/>
        public string GetLangText(string lang, string fullKey)
        {
            (string @namespace, string subKey) = LanguageKey.Split(fullKey);
            return GetLangText(string.Empty, lang, @namespace, subKey);
        }

        /// <inheritdoc/>
        public string GetLangText(string lang, string @namespace, string subKey)
            => GetLangText(string.Empty, lang, @namespace, subKey);

        /// <inheritdoc/>
        public string GetLangText(string customizeId, string lang, string @namespace, string subKey)
            => TryResolveLangText(customizeId, lang, @namespace, subKey, out string text) ? text : $"{@namespace}.{subKey}";

        /// <inheritdoc/>
        public bool TryGetLangText(string lang, string fullKey, out string text)
        {
            (string @namespace, string subKey) = LanguageKey.Split(fullKey);
            return TryGetLangText(string.Empty, lang, @namespace, subKey, out text);
        }

        /// <inheritdoc/>
        public bool TryGetLangText(string lang, string @namespace, string subKey, out string text)
            => TryGetLangText(string.Empty, lang, @namespace, subKey, out text);

        /// <inheritdoc/>
        public bool TryGetLangText(string customizeId, string lang, string @namespace, string subKey, out string text)
        {
            if (_inner != null && _inner.TryGetLangText(customizeId, lang, @namespace, subKey, out text))
                return true;

            string? builtIn = GetBuiltIn(lang, @namespace)?.GetText(subKey);
            text = builtIn ?? string.Empty;
            return builtIn != null;
        }

        /// <inheritdoc/>
        public bool TryResolveLangText(string customizeId, string lang, string @namespace, string subKey, out string text)
        {
            foreach (string culture in LanguageFallback.GetChain(lang, DefaultLanguage))
            {
                if (TryGetLangText(customizeId, culture, @namespace, subKey, out text))
                    return true;
            }
            text = string.Empty;
            return false;
        }

        /// <inheritdoc/>
        public LanguageEnum? GetLangEnum(string lang, string fullName)
        {
            (string @namespace, string enumName) = LanguageKey.Split(fullName);
            return GetLangEnum(string.Empty, lang, @namespace, enumName);
        }

        /// <inheritdoc/>
        public LanguageEnum? GetLangEnum(string lang, string @namespace, string enumName)
            => GetLangEnum(string.Empty, lang, @namespace, enumName);

        /// <inheritdoc/>
        /// <remarks>
        /// Enums resolve through the inner service's whole chain first and the shipped resources
        /// second. The framework ships no option sets of its own, so the shipped half only matters
        /// to a namespace a later version adds them to.
        /// </remarks>
        public LanguageEnum? GetLangEnum(string customizeId, string lang, string @namespace, string enumName)
        {
            var hit = _inner?.GetLangEnum(customizeId, lang, @namespace, enumName);
            if (hit != null)
                return hit;

            foreach (string culture in LanguageFallback.GetChain(lang, DefaultLanguage))
            {
                hit = GetBuiltIn(culture, @namespace)?.GetEnum(enumName);
                if (hit != null)
                    return hit;
            }
            return null;
        }

        /// <inheritdoc/>
        public string? GetLangEnumText(string lang, string fullName, string code)
        {
            (string @namespace, string enumName) = LanguageKey.Split(fullName);
            return GetLangEnum(string.Empty, lang, @namespace, enumName)?.GetText(code);
        }
    }
}
