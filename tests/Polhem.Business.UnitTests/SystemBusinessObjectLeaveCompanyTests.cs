using System.ComponentModel;
using Polhem.Business.System;
using Polhem.Definition.Identity;
using Polhem.Tests.Shared;

using Polhem.Definition;
using Polhem.Core.Exceptions;
using Polhem.Definition.Database;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Behavior tests for <see cref="SystemBusinessObject.LeaveCompany"/>.
    /// </summary>
    public class SystemBusinessObjectLeaveCompanyTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectLeaveCompanyTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("LeaveCompany clears CompanyId for a session that has entered a company")]
        public void LeaveCompany_WhenEntered_ClearsCompanyId()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);
            var session = sessionService.Get(accessToken)!;
            session.CompanyScope = new SessionCompanyScope("C001", string.Empty, [], Guid.Empty, Guid.Empty, Guid.Empty);
            sessionService.Set(session);
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);

            try
            {
                var result = bo.LeaveCompany(new LeaveCompanyArgs());

                Assert.NotNull(result);
                Assert.Null(sessionService.Get(accessToken)!.CompanyId);
            }
            finally
            {
                sessionService.Remove(accessToken);
            }
        }

        [Fact]
        [DisplayName("LeaveCompany is idempotent and succeeds for a session that has not entered a company")]
        public void LeaveCompany_WhenNotEntered_Idempotent()
        {
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);

            try
            {
                var result = bo.LeaveCompany(new LeaveCompanyArgs());

                Assert.NotNull(result);
                Assert.Null(sessionService.Get(accessToken)!.CompanyId);
            }
            finally
            {
                sessionService.Remove(accessToken);
            }
        }

        [Fact]
        [DisplayName("LeaveCompany throws ArgumentNullException for null args")]
        public void LeaveCompany_NullArgs_ThrowsArgumentNullException()
        {
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);
            var sessionService = _fx.GetRequiredService<ISessionInfoService>();

            try
            {
                Assert.Throws<ArgumentNullException>(() => bo.LeaveCompany(null!));
            }
            finally
            {
                sessionService.Remove(accessToken);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("LeaveCompany throws AuthenticationRequiredException for an invalid session")]
        public void LeaveCompany_NoSession_ThrowsAuthenticationRequiredException()
        {
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.NewGuid(), SysProgIds.System);

            Assert.Throws<AuthenticationRequiredException>(() => bo.LeaveCompany(new LeaveCompanyArgs()));
        }
    }
}
