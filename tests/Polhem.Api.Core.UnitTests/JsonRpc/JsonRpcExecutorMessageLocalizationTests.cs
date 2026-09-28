using System.ComponentModel;
using Polhem.Api.Core.JsonRpc;
using Polhem.Base.Exceptions;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Security;

namespace Polhem.Api.Core.UnitTests.JsonRpc
{
    /// <summary>
    /// Tests for <see cref="JsonRpcExecutor.LocalizeMessage"/>: a keyed user-facing message is
    /// translated in the session's culture before it leaves the server, and falls back to its English
    /// text whenever a translation does not apply.
    /// </summary>
    [Collection(SysInfoStaticCollection.Name)]
    public class JsonRpcExecutorMessageLocalizationTests
    {
        private static readonly Guid s_token = Guid.NewGuid();

        [Fact]
        [DisplayName("A keyed message is translated into the session's culture with its arguments placed")]
        public void LocalizeMessage_SessionCultureZhTw_Translates()
        {
            var executor = CreateExecutor(sessionCulture: "zh-TW");
            var ex = new ForbiddenException(PolhemMessages.PermissionDenied, "Permission denied: '{0}' on model '{1}'.", "Delete", "Order");

            string message = executor.LocalizeMessage(ex, ex.Message);

            Assert.Equal("權限不足：無法對模型「Order」執行「Delete」。", message);
        }

        [Fact]
        [DisplayName("A session culture with no translation and no default language keeps the English text")]
        public void LocalizeMessage_UntranslatedCulture_KeepsEnglish()
        {
            var executor = CreateExecutor(sessionCulture: "fr-FR");
            var ex = new UserMessageException(PolhemMessages.LoginInvalidCredentials, "Invalid username or password.");

            Assert.Equal("Invalid username or password.", executor.LocalizeMessage(ex, ex.Message));
        }

        [Fact]
        [DisplayName("A call without a session resolves in the default language")]
        public void LocalizeMessage_NoSession_UsesDefaultLanguage()
        {
            var executor = CreateExecutor(sessionCulture: null, defaultLanguage: "zh-TW");
            var ex = new UserMessageException(PolhemMessages.LoginInvalidCredentials, "Invalid username or password.");

            Assert.Equal("帳號或密碼錯誤。", executor.LocalizeMessage(ex, ex.Message));
        }

        [Fact]
        [DisplayName("An authentication failure does not look the session up again and resolves in the default language")]
        public void LocalizeMessage_AuthenticationFailure_SkipsSessionLookup()
        {
            var sessions = new StubSessionInfoService("fr-FR");
            var executor = CreateExecutor(sessions, defaultLanguage: "zh-TW");
            var ex = new AuthenticationRequiredException(PolhemMessages.SessionExpired, "Session has expired.");

            Assert.Equal("工作階段已過期。", executor.LocalizeMessage(ex, ex.Message));
            Assert.Equal(0, sessions.GetCount);
        }

        [Theory]
        [InlineData("en-GB", "Invalid username or password.")]
        [InlineData("fr-FR", "帳號或密碼錯誤。")]
        [InlineData("zh-TW", "帳號或密碼錯誤。")]
        [DisplayName("On a zh-TW-default deployment an en-GB session gets English while fr-FR and zh-TW sessions get zh-TW")]
        public void LocalizeMessage_ZhTwDefault_EnglishStops(string sessionCulture, string expected)
        {
            var executor = CreateExecutor(sessionCulture, defaultLanguage: "zh-TW");
            var ex = new UserMessageException(PolhemMessages.LoginInvalidCredentials, "Invalid username or password.");

            Assert.Equal(expected, executor.LocalizeMessage(ex, ex.Message));
        }

        [Fact]
        [DisplayName("A literal message without a key travels unchanged")]
        public void LocalizeMessage_LiteralMessage_Unchanged()
        {
            var executor = CreateExecutor(sessionCulture: "zh-TW");
            var ex = new UserMessageException("Order total is too high.");

            Assert.Equal("Order total is too high.", executor.LocalizeMessage(ex, ex.Message));
        }

        [Fact]
        [DisplayName("A form rule message is translated from the form's own namespace, and kept literal without a translation")]
        public void LocalizeMessage_RuleMessageKey_TranslatesFromFormNamespace()
        {
            var host = new TableLanguageService();
            host.Add("zh-TW", "Order", "Rule.total_positive.Message", "金額必須大於 0。");
            var executor = CreateExecutor(new StubSessionInfoService("zh-TW"), languageService: new FrameworkLanguageService(host));
            var translated = new UserMessageException("Order.Rule.total_positive.Message", "Total must be positive.");
            var untranslated = new UserMessageException("Order.Rule.other.Message", "Other rule failed.");

            Assert.Equal("金額必須大於 0。", executor.LocalizeMessage(translated, translated.Message));
            Assert.Equal("Other rule failed.", executor.LocalizeMessage(untranslated, untranslated.Message));
        }

        [Fact]
        [DisplayName("A translation naming more placeholders than the throw site supplies falls back to the English text")]
        public void LocalizeMessage_TranslationWithExtraPlaceholder_FallsBackToEnglish()
        {
            var host = new TableLanguageService();
            host.Add("zh-TW", "Order", "Rule.r1.Message", "欄位 {0} 與 {1} 不符。");
            var executor = CreateExecutor(new StubSessionInfoService("zh-TW"), languageService: new FrameworkLanguageService(host));
            var ex = new UserMessageException("Order.Rule.r1.Message", "Field {0} does not match.", "qty");

            Assert.Equal("Field qty does not match.", executor.LocalizeMessage(ex, ex.Message));
        }

        [Fact]
        [DisplayName("Without a language service every message travels in English")]
        public void LocalizeMessage_NoLanguageService_KeepsEnglish()
        {
            var executor = CreateExecutor(new StubSessionInfoService("zh-TW"), languageService: null);
            var ex = new UserMessageException(PolhemMessages.LoginInvalidCredentials, "Invalid username or password.");

            Assert.Equal("Invalid username or password.", executor.LocalizeMessage(ex, ex.Message));
        }

        [Fact]
        [DisplayName("An exception whose message the contract replaces is not translated")]
        public void LocalizeMessage_FixedMessageType_Unchanged()
        {
            var executor = CreateExecutor(sessionCulture: "zh-TW");
            var ex = new InvalidOperationException("internal detail");

            Assert.Equal("The request could not be completed.", executor.LocalizeMessage(ex, "The request could not be completed."));
        }

        private static JsonRpcExecutor CreateExecutor(string? sessionCulture, string defaultLanguage = "")
            => CreateExecutor(new StubSessionInfoService(sessionCulture),
                new FrameworkLanguageService(null, () => defaultLanguage));

        private static JsonRpcExecutor CreateExecutor(StubSessionInfoService sessions, string defaultLanguage)
            => CreateExecutor(sessions, new FrameworkLanguageService(null, () => defaultLanguage));

        private static JsonRpcExecutor CreateExecutor(StubSessionInfoService sessions, ILanguageService? languageService)
        {
            var executor = new JsonRpcExecutor(new NoObjects(), new AcceptAllTokens(), new NoKeys(), null, null, sessions)
            {
                AccessToken = s_token,
                LanguageService = languageService,
            };
            return executor;
        }

        private sealed class StubSessionInfoService(string? culture) : ISessionInfoService
        {
            public int GetCount { get; private set; }

            public SessionInfo Get(Guid accessToken)
            {
                GetCount++;
                return culture is null ? null! : new SessionInfo { AccessToken = accessToken, Culture = culture };
            }

            public void Set(SessionInfo sessionInfo) { }

            public void Remove(Guid accessToken) { }
        }

        private sealed class TableLanguageService : ILanguageService
        {
            private readonly Dictionary<string, string> _texts = new(StringComparer.OrdinalIgnoreCase);

            public void Add(string lang, string ns, string subKey, string text) => _texts[$"{lang}|{ns}|{subKey}"] = text;

            public bool TryGetLangText(string lang, string @namespace, string subKey, out string text)
            {
                bool hit = _texts.TryGetValue($"{lang}|{@namespace}|{subKey}", out string? value);
                text = value ?? string.Empty;
                return hit;
            }

            public bool TryGetLangText(string lang, string fullKey, out string text) => throw new NotSupportedException();
            public string GetLangText(string lang, string fullKey) => throw new NotSupportedException();
            public string GetLangText(string lang, string @namespace, string subKey) => throw new NotSupportedException();
            public LanguageEnum? GetLangEnum(string lang, string fullName) => null;
            public LanguageEnum? GetLangEnum(string lang, string @namespace, string enumName) => null;
            public string? GetLangEnumText(string lang, string fullName, string code) => null;
        }

        private sealed class NoObjects : IBusinessObjectFactory
        {
            public object CreateBusinessObject(Guid accessToken, string progId, bool isLocalCall) => throw new NotSupportedException();
        }

        private sealed class AcceptAllTokens : IAccessTokenValidator
        {
            public bool Validate(Guid accessToken) => true;
        }

        private sealed class NoKeys : IApiEncryptionKeyProvider
        {
            public byte[] GetKey(Guid accessToken) => [];

            public byte[] GenerateKeyForLogin(Guid accessToken) => [];

            public bool SupportsSessionRebuild => false;
        }
    }
}
