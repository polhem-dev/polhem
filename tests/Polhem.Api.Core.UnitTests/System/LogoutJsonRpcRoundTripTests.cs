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
    /// End-to-end round-trip through <see cref="JsonRpcExecutor"/>: dispatches <c>System.Logout</c> through the
    /// executor to <see cref="Polhem.Business.System.SystemBusinessObject.Logout"/> and verifies that the SessionInfo
    /// disappears from the cache and the call succeeds.
    /// <para>
    /// Needs <see cref="SharedDbFixture"/> (not <c>PolhemTestFixture</c>): since sessions are persisted, Logout
    /// deletes the seeded `st_session` row, so this test really depends on <c>st_session</c>. <c>PolhemTestFixture</c>
    /// alone does not create the schema, so the test would only pass when another test process happened to create the
    /// table first, which was the source of intermittent red runs on CI.
    /// </para>
    /// </summary>
    public class LogoutJsonRpcRoundTripTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public LogoutJsonRpcRoundTripTests(SharedDbFixture fx) { _fx = fx; }

        private JsonRpcExecutor BuildExecutor(Guid accessToken)
        {
            var boFactory = new BusinessObjectFactory(
                _fx.Provider,
                _fx.GetRequiredService<IDefineAccess>(),
                _fx.GetRequiredService<ISessionInfoService>(),
                _fx.GetRequiredService<ILanguageService>(),
                _fx.GetRequiredService<IBoTypeResolver>());

            return new JsonRpcExecutor(
                boFactory,
                _fx.GetRequiredService<IAccessTokenValidator>(),
                _fx.GetRequiredService<IApiEncryptionKeyProvider>())
            {
                AccessToken = accessToken,
                IsLocalCall = true,
            };
        }

        private static JsonRpcRequest BuildRequest()
            => new()
            {
                Method = $"{SysProgIds.System}.{SystemActions.Logout}",
                Params = new JsonRpcParams { Value = new LogoutRequest() },
                Id = Guid.NewGuid().ToString(),
            };

        [Fact]
        [DisplayName("System.Logout removes the SessionInfo and succeeds")]
        public void Logout_ValidSession_RemovesSessionInfo()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);

            var response = BuildExecutor(accessToken).Execute(BuildRequest());

            Assert.Null(response.Error);
            Assert.IsType<LogoutResponse>(response.Result!.Value);
            Assert.Null(sessionService.Get(accessToken));
        }

        [Fact]
        [DisplayName("System.Logout removes a session that has already entered a company")]
        public void Logout_AfterEnteredCompany_ClearsThenRemoves()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);
            var session = sessionService.Get(accessToken)!;
            session.CompanyScope = new SessionCompanyScope("C001", string.Empty, [], Guid.Empty, Guid.Empty, Guid.Empty);
            sessionService.Set(session);

            var response = BuildExecutor(accessToken).Execute(BuildRequest());

            Assert.Null(response.Error);
            Assert.Null(sessionService.Get(accessToken));
        }
    }
}
