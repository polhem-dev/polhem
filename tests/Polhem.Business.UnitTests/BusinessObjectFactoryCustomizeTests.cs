using System.ComponentModel;
using Polhem.Business.Form;
using Polhem.Business.UnitTests.Fakes;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Tests for how <see cref="BusinessObjectFactory"/> wires customization into BO type resolution:
    /// it calls <see cref="IBoTypeResolver.Resolve(string, string)"/> with <c>SessionInfo.CustomizeId</c>, and passes an empty
    /// string when there is no session or CustomizeId is empty (the resolution result is identical to the previous behavior).
    /// </summary>
    public class BusinessObjectFactoryCustomizeTests
    {
        [Fact]
        [DisplayName("CreateBusinessObject resolves the BO type with the session's CustomizeId")]
        public void CreateBusinessObject_Form_PassesSessionCustomizeIdToResolver()
        {
            var resolver = new SpyResolver();
            var token = Guid.NewGuid();
            var sessions = new StubSessionInfoService();
            sessions.Add(new SessionInfo { AccessToken = token, CustomizeId = "acme" });
            var factory = CreateFactory(resolver, sessions);

            factory.CreateBusinessObject(token, "P001", isLocalCall: true);

            Assert.Equal("acme", resolver.LastCustomizeId);
            Assert.Equal("P001", resolver.LastProgId);
        }

        [Fact]
        [DisplayName("The resolved customized BO type is actually instantiated")]
        public void CreateBusinessObject_Form_InstantiatesResolvedType()
        {
            var resolver = new SpyResolver { ResolvedType = typeof(TenantFormBo) };
            var token = Guid.NewGuid();
            var sessions = new StubSessionInfoService();
            sessions.Add(new SessionInfo { AccessToken = token, CustomizeId = "acme" });
            var factory = CreateFactory(resolver, sessions);

            var bo = factory.CreateBusinessObject(token, "P001", isLocalCall: true);

            Assert.IsType<TenantFormBo>(bo);
        }

        // ---- Regression guards: a deployment without CustomizeId must behave exactly as before ----

        [Fact]
        [DisplayName("Regression guard: a session without CustomizeId resolves with an empty string (base only)")]
        public void CreateBusinessObject_Form_SessionWithoutCustomizeId_PassesEmpty()
        {
            var resolver = new SpyResolver();
            var token = Guid.NewGuid();
            var sessions = new StubSessionInfoService();
            sessions.Add(new SessionInfo { AccessToken = token });
            var factory = CreateFactory(resolver, sessions);

            factory.CreateBusinessObject(token, "P001", isLocalCall: true);

            Assert.Equal(string.Empty, resolver.LastCustomizeId);
        }

        [Fact]
        [DisplayName("Regression guard: an empty AccessToken skips the session lookup and resolves with an empty string")]
        public void CreateBusinessObject_Form_EmptyAccessToken_SkipsSessionLookup()
        {
            var resolver = new SpyResolver();
            var sessions = new StubSessionInfoService();
            var factory = CreateFactory(resolver, sessions);

            factory.CreateBusinessObject(Guid.Empty, "P001", isLocalCall: true);

            Assert.Equal(string.Empty, resolver.LastCustomizeId);
            Assert.Equal(0, sessions.GetCallCount);
        }

        [Fact]
        [DisplayName("Regression guard: a missing session resolves with an empty string without throwing")]
        public void CreateBusinessObject_Form_NoSession_PassesEmpty()
        {
            var resolver = new SpyResolver();
            var sessions = new StubSessionInfoService(); // No session registered.
            var factory = CreateFactory(resolver, sessions);

            var exception = Record.Exception(() => factory.CreateBusinessObject(Guid.NewGuid(), "P001", isLocalCall: true));

            Assert.Null(exception);
            Assert.Equal(string.Empty, resolver.LastCustomizeId);
        }

        // ---- Fixtures ----

        public class TenantFormBo : FormBusinessObject
        {
            public TenantFormBo(Definition.IPolhemContext ctx, Guid accessToken, string progId, bool isLocalCall = true)
                : base(ctx, accessToken, progId, isLocalCall) { }
        }

        // DefineAccess / LanguageService are only forwarded onto the PolhemContext and never touched by
        // the resolution path under test, but the factory ctor null-checks every dependency, so
        // inert stubs stand in for them.
        private static BusinessObjectFactory CreateFactory(IBoTypeResolver resolver, ISessionInfoService sessions)
            => new BusinessObjectFactory(
                new EmptyServiceProvider(), new FakeDefineAccess(), sessions, new InertLanguageService(), resolver);

        // ---- Test doubles ----

        private sealed class SpyResolver : IBoTypeResolver
        {
            public Type ResolvedType { get; set; } = typeof(FormBusinessObject);
            public string? LastCustomizeId { get; private set; }
            public string? LastProgId { get; private set; }

            public Type Resolve(string progId) => Resolve(string.Empty, progId);

            public Type Resolve(string customizeId, string progId)
            {
                LastCustomizeId = customizeId;
                LastProgId = progId;
                return ResolvedType;
            }
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

        private sealed class EmptyServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }

        /// <summary>
        /// Satisfies the factory's null check without participating in any test — the resolution
        /// path never resolves language text.
        /// </summary>
        private sealed class InertLanguageService : ILanguageService
        {
            public string GetLangText(string lang, string fullKey) => throw new NotSupportedException();
            public string GetLangText(string lang, string @namespace, string subKey) => throw new NotSupportedException();
            public bool TryGetLangText(string lang, string fullKey, out string text) => throw new NotSupportedException();
            public bool TryGetLangText(string lang, string @namespace, string subKey, out string text) => throw new NotSupportedException();
            public LanguageEnum? GetLangEnum(string lang, string fullName) => throw new NotSupportedException();
            public LanguageEnum? GetLangEnum(string lang, string @namespace, string enumName) => throw new NotSupportedException();
            public string? GetLangEnumText(string lang, string fullName, string code) => throw new NotSupportedException();
        }
    }
}
