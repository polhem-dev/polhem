using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// <see cref="BusinessObject.GetLangText(string)"/> / <see cref="BusinessObject.GetLangText(string, string)"/>
    /// wiring for tenant customization: the language lookup carries <c>SessionInfo.CustomizeId</c>, and
    /// with no session or an empty CustomizeId it behaves exactly as before (no customization).
    /// </summary>
    public class BusinessObjectLangCustomizeTests
    {
        [Fact]
        [DisplayName("GetLangText(ns, subKey) passes SessionInfo.CustomizeId")]
        public void GetLangText_WithSessionCustomizeId_PassesItToLanguageService()
        {
            var lang = new SpyLanguageService();
            var token = Guid.NewGuid();
            var bo = CreateBo(lang, token, culture: "zh-TW", customizeId: "acme");

            bo.CallGetLangText("Common", "OK");

            Assert.Equal("acme", lang.LastCustomizeId);
            Assert.Equal("zh-TW", lang.LastLang);
            Assert.Equal("Common", lang.LastNamespace);
            Assert.Equal("OK", lang.LastSubKey);
        }

        [Fact]
        [DisplayName("GetLangText(fullKey) splits on the first dot and passes CustomizeId")]
        public void GetLangText_FullKey_SplitsOnFirstDotAndPassesCustomizeId()
        {
            var lang = new SpyLanguageService();
            var bo = CreateBo(lang, Guid.NewGuid(), culture: "zh-TW", customizeId: "acme");

            bo.CallGetLangText("Customer.Field.sys_name.Caption");

            Assert.Equal("acme", lang.LastCustomizeId);
            Assert.Equal("Customer", lang.LastNamespace);
            Assert.Equal("Field.sys_name.Caption", lang.LastSubKey);
        }

        [Fact]
        [DisplayName("GetLangText(fullKey) without a dot is treated as namespace-only with an empty subKey (unchanged behavior)")]
        public void GetLangText_FullKeyWithoutDot_TreatedAsNamespaceOnly()
        {
            var lang = new SpyLanguageService();
            var bo = CreateBo(lang, Guid.NewGuid(), culture: "zh-TW", customizeId: string.Empty);

            bo.CallGetLangText("Common");

            Assert.Equal("Common", lang.LastNamespace);
            Assert.Equal(string.Empty, lang.LastSubKey);
        }

        [Fact]
        [DisplayName("GetLangText(fullKey) throws ArgumentNullException for null (unchanged behavior)")]
        public void GetLangText_NullFullKey_Throws()
        {
            var bo = CreateBo(new SpyLanguageService(), Guid.NewGuid(), culture: "zh-TW", customizeId: string.Empty);

            Assert.Throws<ArgumentNullException>(() => bo.CallGetLangText(null!));
        }

        // ---- Regression guards: a deployment without CustomizeId must behave exactly as before ----

        [Fact]
        [DisplayName("Regression guard: a session without CustomizeId passes an empty string (base-only lookup)")]
        public void GetLangText_SessionWithoutCustomizeId_PassesEmpty()
        {
            var lang = new SpyLanguageService();
            var bo = CreateBo(lang, Guid.NewGuid(), culture: "zh-TW", customizeId: string.Empty);

            bo.CallGetLangText("Common", "OK");

            Assert.Equal(string.Empty, lang.LastCustomizeId);
        }

        [Fact]
        [DisplayName("Regression guard: an empty AccessToken skips the session lookup, and both customizeId and lang are empty")]
        public void GetLangText_EmptyAccessToken_SkipsSessionLookup()
        {
            var lang = new SpyLanguageService();
            var sessions = new StubSessionInfoService();
            var bo = new LangProbeBusinessObject(BuildContext(lang, sessions), Guid.Empty);

            bo.CallGetLangText("Common", "OK");

            Assert.Equal(string.Empty, lang.LastCustomizeId);
            Assert.Equal(string.Empty, lang.LastLang);
            Assert.Equal(0, sessions.GetCallCount);
        }

        [Fact]
        [DisplayName("Regression guard: a missing session falls back to an empty customizeId without throwing")]
        public void GetLangText_NoSession_FallsBackToEmptyCustomizeId()
        {
            var lang = new SpyLanguageService();
            var sessions = new StubSessionInfoService(); // No session registered.
            var bo = new LangProbeBusinessObject(BuildContext(lang, sessions), Guid.NewGuid());

            var exception = Record.Exception(() => bo.CallGetLangText("Common", "OK"));

            Assert.Null(exception);
            Assert.Equal(string.Empty, lang.LastCustomizeId);
        }

        // ---- Fixtures ----

        private static LangProbeBusinessObject CreateBo(
            ILanguageService lang, Guid token, string culture, string customizeId)
        {
            var sessions = new StubSessionInfoService();
            sessions.Add(new SessionInfo
            {
                AccessToken = token,
                Culture = culture,
                CustomizeId = customizeId,
            });
            return new LangProbeBusinessObject(BuildContext(lang, sessions), token);
        }

        // Only LanguageService and SessionInfoService are exercised; the remaining context members
        // are never touched by the localization path, so they stay unset.
        private static BusinessObjectContext BuildContext(ILanguageService lang, ISessionInfoService sessions)
            => new BusinessObjectContext
            {
                DefineAccess = null!,
                SessionInfoService = sessions,
                LanguageService = lang,
                BoFactory = null!,
                Services = null!,
            };

        /// <summary>
        /// Exposes the protected <c>GetLangText</c> helpers so the wiring can be asserted directly.
        /// </summary>
        private sealed class LangProbeBusinessObject : BusinessObject
        {
            public LangProbeBusinessObject(IBusinessObjectContext ctx, Guid accessToken) : base(ctx, accessToken, "TestProg") { }

            public string CallGetLangText(string fullKey) => GetLangText(fullKey);

            public string CallGetLangText(string @namespace, string subKey) => GetLangText(@namespace, subKey);
        }

        // ---- Test doubles ----

        private sealed class SpyLanguageService : ILanguageService
        {
            public string? LastCustomizeId { get; private set; }
            public string? LastLang { get; private set; }
            public string? LastNamespace { get; private set; }
            public string? LastSubKey { get; private set; }

            public string GetLangText(string customizeId, string lang, string @namespace, string subKey)
            {
                LastCustomizeId = customizeId;
                LastLang = lang;
                LastNamespace = @namespace;
                LastSubKey = subKey;
                return "text";
            }

            public string GetLangText(string lang, string fullKey) => throw new NotSupportedException();
            public string GetLangText(string lang, string @namespace, string subKey) => throw new NotSupportedException();
            public bool TryGetLangText(string lang, string fullKey, out string text) => throw new NotSupportedException();
            public bool TryGetLangText(string lang, string @namespace, string subKey, out string text) => throw new NotSupportedException();
            public LanguageEnum? GetLangEnum(string lang, string fullName) => throw new NotSupportedException();
            public LanguageEnum? GetLangEnum(string lang, string @namespace, string enumName) => throw new NotSupportedException();
            public string? GetLangEnumText(string lang, string fullName, string code) => throw new NotSupportedException();
        }

        private sealed class StubSessionInfoService : ISessionInfoService
        {
            private readonly Dictionary<Guid, SessionInfo> _sessions = [];

            public int GetCallCount { get; private set; }

            public void Add(SessionInfo sessionInfo) => _sessions[sessionInfo.AccessToken] = sessionInfo;

            public SessionInfo Get(Guid accessToken)
            {
                GetCallCount++;
                return _sessions.TryGetValue(accessToken, out var s) ? s : null!;
            }

            public void Set(SessionInfo sessionInfo) => Add(sessionInfo);

            public void Remove(Guid accessToken) => _sessions.Remove(accessToken);
        }
    }
}
