using System.ComponentModel;
using Polhem.Business.System;
using Polhem.Definition.Identity;
using Polhem.Tests.Shared;

using Polhem.Definition;
using Polhem.Definition.Database;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Behavior tests for <see cref="SystemBusinessObject.Logout"/>.
    /// </summary>
    public class SystemBusinessObjectLogoutTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectLogoutTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Logout removes the SessionInfo of a valid session")]
        public void Logout_ValidSession_RemovesSessionInfo()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);

            var result = bo.Logout(new LogoutArgs());

            Assert.NotNull(result);
            Assert.Null(sessionService.Get(accessToken));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Logout clears CompanyId on the held SessionInfo and removes the session")]
        public void Logout_AfterEnteredCompany_ClearsCompanyIdAndRemoves()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);
            var session = sessionService.Get(accessToken)!;
            session.CompanyScope = new SessionCompanyScope("C001", string.Empty, [], Guid.Empty, Guid.Empty, Guid.Empty);
            sessionService.Set(session);
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);
            Assert.Equal("C001", session.CompanyId);

            bo.Logout(new LogoutArgs());

            // The held reference is how a consumer that still has the SessionInfo observes the clear.
            Assert.Null(session.CompanyId);
            Assert.Null(sessionService.Get(accessToken));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Logout is idempotent and succeeds for a session that does not exist")]
        public void Logout_UnknownSession_Idempotent()
        {
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.NewGuid(), SysProgIds.System);

            var result = bo.Logout(new LogoutArgs());

            Assert.IsType<LogoutResult>(result);
        }

        [Fact]
        [DisplayName("Logout throws ArgumentNullException for null args")]
        public void Logout_NullArgs_ThrowsArgumentNullException()
        {
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();

            try
            {
                Assert.Throws<ArgumentNullException>(() => bo.Logout(null!));
            }
            finally
            {
                sessionService.Remove(accessToken);
            }
        }
    }
}
