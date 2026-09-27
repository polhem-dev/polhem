using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Definition.Language;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages.System;
using Polhem.Business;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Security;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests.System
{
    /// <summary>
    /// End-to-end round-trip through <see cref="JsonRpcExecutor"/>: dispatches <c>System.GetLanguage</c> through the
    /// executor to <see cref="Polhem.Business.System.SystemBusinessObject.GetLanguage"/> and verifies that:
    /// <list type="bullet">
    /// <item>action routing (the progId.action reflection lookup) finds the method</item>
    /// <item>ApiInputConverter (GetLanguageRequest → GetLanguageArgs) keeps Lang / Namespace</item>
    /// <item>the naming-convention reflection of ApiOutputConverter (GetLanguageResult → GetLanguageResponse) works,
    ///   and the LanguageResource object is deep-copied correctly</item>
    /// <item>the language resource seeded by the fixture is read from IDefineAccess</item>
    /// </list>
    /// </summary>
    public class GetLanguageJsonRpcRoundTripTests : IClassFixture<GetLanguageJsonRpcRoundTripTests.LangFixture>
    {
        private readonly LangFixture _fx;

        public GetLanguageJsonRpcRoundTripTests(LangFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("System.GetLanguage dispatches through JsonRpcExecutor and returns the LanguageResource seeded by the fixture")]
        public async Task GetLanguage_ThroughJsonRpc_DispatchesAndReturnsResource()
        {
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);

            var boFactory = new BusinessObjectFactory(
                _fx.Provider,
                _fx.GetRequiredService<IDefineAccess>(),
                _fx.GetRequiredService<ISessionInfoService>(),
                _fx.GetRequiredService<ILanguageService>(),
                _fx.GetRequiredService<IBoTypeResolver>());

            var executor = new JsonRpcExecutor(
                boFactory,
                _fx.GetRequiredService<IAccessTokenValidator>(),
                _fx.GetRequiredService<IApiEncryptionKeyProvider>())
            {
                AccessToken = accessToken,
                IsLocalCall = true,
            };

            var request = new JsonRpcRequest
            {
                Method = $"{SysProgIds.System}.{SystemActions.GetLanguage}",
                Params = new JsonRpcParams
                {
                    Value = new GetLanguageRequest
                    {
                        Lang = LangFixture.SeedLang,
                        Namespace = LangFixture.SeedNamespace,
                    },
                },
                Id = Guid.NewGuid().ToString(),
            };

            var response = await executor.ExecuteAsync(request);

            Assert.Null(response.Error);
            var result = Assert.IsType<GetLanguageResponse>(response.Result!.Value);
            Assert.False(string.IsNullOrEmpty(result.Xml));
            var resource = XmlCodec.Deserialize<LanguageResource>(result.Xml!);
            Assert.NotNull(resource);
            Assert.Equal(LangFixture.SeedNamespace, resource!.Namespace);
            Assert.Equal(LangFixture.SeedLang, resource.Lang);
            Assert.Equal("你好", resource.GetText("Greeting"));
            var gender = resource.GetEnum("Gender");
            Assert.NotNull(gender);
            Assert.Equal("男", gender!.GetText("M"));
        }

        [Fact]
        [DisplayName("System.GetLanguage dispatches for a missing namespace and returns empty Xml")]
        public async Task GetLanguage_MissingNamespace_DispatchSucceedsWithEmptyXml()
        {
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);

            var boFactory = new BusinessObjectFactory(
                _fx.Provider,
                _fx.GetRequiredService<IDefineAccess>(),
                _fx.GetRequiredService<ISessionInfoService>(),
                _fx.GetRequiredService<ILanguageService>(),
                _fx.GetRequiredService<IBoTypeResolver>());

            var executor = new JsonRpcExecutor(
                boFactory,
                _fx.GetRequiredService<IAccessTokenValidator>(),
                _fx.GetRequiredService<IApiEncryptionKeyProvider>())
            {
                AccessToken = accessToken,
                IsLocalCall = true,
            };

            var request = new JsonRpcRequest
            {
                Method = $"{SysProgIds.System}.{SystemActions.GetLanguage}",
                Params = new JsonRpcParams
                {
                    Value = new GetLanguageRequest { Lang = "zh-TW", Namespace = "Nonexistent" },
                },
                Id = Guid.NewGuid().ToString(),
            };

            var response = await executor.ExecuteAsync(request);

            Assert.Null(response.Error);
            var result = Assert.IsType<GetLanguageResponse>(response.Result!.Value);
            Assert.True(string.IsNullOrEmpty(result.Xml));
        }

        [Fact]
        [DisplayName("System.GetLanguage returns an RpcError for an empty Lang")]
        public async Task GetLanguage_EmptyLang_ReturnsRpcError()
        {
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);

            var boFactory = new BusinessObjectFactory(
                _fx.Provider,
                _fx.GetRequiredService<IDefineAccess>(),
                _fx.GetRequiredService<ISessionInfoService>(),
                _fx.GetRequiredService<ILanguageService>(),
                _fx.GetRequiredService<IBoTypeResolver>());

            var executor = new JsonRpcExecutor(
                boFactory,
                _fx.GetRequiredService<IAccessTokenValidator>(),
                _fx.GetRequiredService<IApiEncryptionKeyProvider>())
            {
                AccessToken = accessToken,
                IsLocalCall = true,
            };

            var request = new JsonRpcRequest
            {
                Method = $"{SysProgIds.System}.{SystemActions.GetLanguage}",
                Params = new JsonRpcParams
                {
                    Value = new GetLanguageRequest { Lang = "", Namespace = "Common" },
                },
                Id = Guid.NewGuid().ToString(),
            };

            var response = await executor.ExecuteAsync(request);

            Assert.NotNull(response.Error);
            Assert.Contains("Lang is required", response.Error!.Message);
        }

        /// <summary>
        /// Writable fixture: copies the shared <c>tests/Define</c> to a temp dir, then
        /// seeds a <see cref="LanguageResource"/> via <see cref="IDefineAccess.SaveLanguage"/>
        /// for the dispatch test to read back through the JSON-RPC pipeline.
        /// </summary>
        public sealed class LangFixture : PolhemTestFixture
        {
            public const string SeedLang = "zh-TW";
            public const string SeedNamespace = "TestSys";

            public LangFixture() : base(b => b.UseTempDefinePath())
            {
                var defineAccess = GetRequiredService<IDefineAccess>();
                var resource = new LanguageResource
                {
                    Namespace = SeedNamespace,
                    Lang = SeedLang,
                };
                resource.Items.Add("Greeting", "你好");
                var gender = new LanguageEnum { Name = "Gender" };
                gender.Entries.Add("M", "男");
                gender.Entries.Add("F", "女");
                resource.Enums.Add(gender);
                defineAccess.SaveLanguage(resource);
            }
        }
    }
}
