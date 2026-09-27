using System.ComponentModel;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages.System;
using Polhem.Business;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Security;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests.System
{
    /// <summary>
    /// End-to-end round-trip through <see cref="JsonRpcExecutor"/>: dispatches <c>System.EnterCompany</c> through the
    /// executor to <see cref="Polhem.Business.System.SystemBusinessObject.EnterCompany"/> and verifies that:
    /// <list type="bullet">
    /// <item>action routing (the progId.action reflection lookup) finds the method</item>
    /// <item>ApiInputConverter (EnterCompanyRequest → EnterCompanyArgs) keeps CompanyId</item>
    /// <item>the naming-convention reflection of ApiOutputConverter (EnterCompanyResult → EnterCompanyResponse) works, and CompanyInfo is deep-copied correctly</item>
    /// <item>the SessionInfo.CompanyId written by the BO can be read back from ISessionInfoService</item>
    /// </list>
    /// </summary>
    public class EnterCompanyJsonRpcRoundTripTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public EnterCompanyJsonRpcRoundTripTests(SharedDbFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("System.EnterCompany dispatches through JsonRpcExecutor and writes SessionInfo.CompanyId")]
        public async Task EnterCompany_ThroughJsonRpc_DispatchesAndBindsCompany()
        {
            // Arrange: uses the user '001' and company 'C001' mapping already seeded by `SharedDatabaseState`, so the
            // call takes the full path of a cache miss, the DB fallback and the `HasAccess` JOIN.
            var accessToken = TestSessionFactory.CreateAccessToken(_fx, userId: "001");

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
                Method = $"{SysProgIds.System}.{SystemActions.EnterCompany}",
                Params = new JsonRpcParams
                {
                    Value = new EnterCompanyRequest { CompanyId = "C001" },
                },
                Id = Guid.NewGuid().ToString(),
            };

            // Act
            var response = await executor.ExecuteAsync(request);

            // Assert: the response succeeds and carries the CompanyInfo of seed company 'C001', loaded from the DB after a cache miss.
            Assert.Null(response.Error);
            var result = Assert.IsType<EnterCompanyResponse>(response.Result!.Value);
            Assert.NotNull(result.Company);
            Assert.Equal("C001", result.Company.CompanyId);
            Assert.Equal("測試公司", result.Company.CompanyName);
            // ApiOutputConverter must copy the capability snapshot through the Result → Response
            // reflection copy (empty when the seed grants the user nothing, but never null).
            Assert.NotNull(result.Capabilities);

            var session = _fx.GetRequiredService<ISessionInfoService>().Get(accessToken);
            Assert.NotNull(session);
            Assert.Equal("C001", session.CompanyId);
        }

        [Fact]
        [DisplayName("System.EnterCompany returns an RpcError for an unknown CompanyId and leaves SessionInfo.CompanyId unchanged")]
        public async Task EnterCompany_UnknownCompany_ReturnsRpcError()
        {
            var accessToken = TestSessionFactory.CreateAccessToken(_fx, userId: "001");

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
                Method = $"{SysProgIds.System}.{SystemActions.EnterCompany}",
                Params = new JsonRpcParams
                {
                    Value = new EnterCompanyRequest { CompanyId = "NO_SUCH_COMPANY" },
                },
                Id = Guid.NewGuid().ToString(),
            };

            var response = await executor.ExecuteAsync(request);

            Assert.NotNull(response.Error);
            Assert.Contains("Company access denied", response.Error!.Message);

            var session = _fx.GetRequiredService<ISessionInfoService>().Get(accessToken);
            Assert.NotNull(session);
            Assert.Null(session.CompanyId);
        }
    }
}
