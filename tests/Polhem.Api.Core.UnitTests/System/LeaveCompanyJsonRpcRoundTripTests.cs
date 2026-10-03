using System.ComponentModel;
using Polhem.Api.Core.Messages.System;
using Polhem.Business;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;
using Polhem.Definition.Database;
using Polhem.Api.Core.UnitTests.Dispatch;

namespace Polhem.Api.Core.UnitTests.System
{
    /// <summary>
    /// An end-to-end round-trip through the JSON-RPC dispatcher: <c>System.LeaveCompany</c> is dispatched by the
    /// dispatcher to <see cref="Polhem.Business.System.SystemBusinessObject.LeaveCompany"/>, and the test verifies that
    /// SessionInfo.CompanyId is cleared and the call succeeds.
    /// <para>
    /// It needs <see cref="SharedDbFixture"/> (not <c>PolhemTestFixture</c>): since sessions are persisted,
    /// LeaveCompany updates the session's row in `st_session`, so this test really depends on <c>st_session</c>.
    /// <c>PolhemTestFixture</c> alone does not create the schema, and the test would pass only when another test
    /// process happened to create the tables first. That was the source of intermittent red runs in CI.
    /// </para>
    /// </summary>
    public class LeaveCompanyJsonRpcRoundTripTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public LeaveCompanyJsonRpcRoundTripTests(SharedDbFixture fx) { _fx = fx; }

        private TestDispatcher BuildExecutor(Guid accessToken)
        {
            var boFactory = new BusinessObjectFactory(
                _fx.Provider,
                _fx.GetRequiredService<IDefineAccess>(),
                _fx.GetRequiredService<ISessionInfoService>(),
                _fx.GetRequiredService<ILanguageService>(),
                _fx.GetRequiredService<IBoTypeResolver>());

            return new TestDispatcher(_fx.Provider, boFactory)
            {
                AccessToken = accessToken,
                IsLocalCall = true,
            };
        }

        private static TestRpcRequest BuildRequest()
            => new()
            {
                Method = $"{SysProgIds.System}.{SystemActions.LeaveCompany}",
                Params = new TestPayload { Value = new LeaveCompanyRequest() },
                Id = Guid.NewGuid().ToString(),
            };

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("System.LeaveCompany clears SessionInfo.CompanyId and succeeds")]
        public async Task LeaveCompany_AfterEntered_ClearsCompanyId()
        {
            // Arrange
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);
            var session = sessionService.Get(accessToken)!;
            session.CompanyScope = new SessionCompanyScope("C001", string.Empty, [], Guid.Empty, Guid.Empty, Guid.Empty);
            sessionService.Set(session);

            // Act
            var response = await BuildExecutor(accessToken).ExecuteAsync(BuildRequest());

            // Assert
            Assert.Null(response.Error);
            Assert.IsType<LeaveCompanyResponse>(response.Result!.Value);
            Assert.Null(sessionService.Get(accessToken)!.CompanyId);
        }

        [Fact]
        [DisplayName("System.LeaveCompany is idempotent and succeeds when no company has been entered")]
        public async Task LeaveCompany_WhenNotEntered_Idempotent()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);
            Assert.Null(sessionService.Get(accessToken)!.CompanyId);

            var response = await BuildExecutor(accessToken).ExecuteAsync(BuildRequest());

            Assert.Null(response.Error);
            Assert.IsType<LeaveCompanyResponse>(response.Result!.Value);
            Assert.Null(sessionService.Get(accessToken)!.CompanyId);
        }
    }
}
