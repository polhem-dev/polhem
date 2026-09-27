using System.ComponentModel;
using Polhem.Business.System;
using Polhem.Business.UnitTests.Fakes;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.System;
using Polhem.Tests.Shared;

using Polhem.Definition;
namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Verifies that the session lifecycle's write points really reach the <c>st_session</c> seed:
    /// Login writes it, EnterCompany and a company switch update it, LeaveCompany clears it, and Logout deletes it.
    /// </summary>
    /// <remarks>
    /// Every one of these is required: if Login did not write, a token would stop working as soon as it was issued under a redeployment or multiple nodes; if Logout did not delete,
    /// the token would be revived from the seed after logout, making logout meaningless.
    /// </remarks>
    public class SystemBusinessObjectSessionSeedTests : IClassFixture<SharedDbFixture>
    {
        private const string SeedCompanyId = "C001";
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectSessionSeedTests(SharedDbFixture fx) { _fx = fx; }

        private ISessionRepository SessionRepository
            => _fx.GetRequiredService<IRepositoryFactory>().Create<ISessionRepository>();

        private Guid LoginAsSeedUser()
        {
            var bo = new TestableSystemBusinessObject(
                TestBusinessObjectContext.Create(_fx), Guid.Empty, _ => (true, "Seed User"));
            return bo.Login(new LoginArgs { UserId = "001", Password = "pwd" }).AccessToken;
        }

        private SystemBusinessObject CreateBo(Guid accessToken)
            => new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), accessToken, SysProgIds.System);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Login writes a seed without a company")]
        public void Login_WritesSeedWithoutCompany()
        {
            var accessToken = LoginAsSeedUser();
            try
            {
                var seed = SessionRepository.GetSession(accessToken);
                Assert.NotNull(seed);
                Assert.Equal(accessToken, seed!.AccessToken);
                Assert.Equal("001", seed.UserId);
                Assert.Null(seed.CompanyId);
                Assert.True(seed.EndTime > DateTime.UtcNow);
            }
            finally
            {
                CreateBo(accessToken).Logout(new LogoutArgs());
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("EnterCompany and LeaveCompany keep the seed's CompanyId in sync")]
        public void EnterAndLeaveCompany_UpdateSeedCompanyId()
        {
            var accessToken = LoginAsSeedUser();
            var bo = CreateBo(accessToken);
            try
            {
                bo.EnterCompany(new EnterCompanyArgs { CompanyId = SeedCompanyId });
                Assert.Equal(SeedCompanyId, SessionRepository.GetSession(accessToken)!.CompanyId);

                bo.LeaveCompany(new LeaveCompanyArgs());
                Assert.Null(SessionRepository.GetSession(accessToken)!.CompanyId);
            }
            finally
            {
                bo.Logout(new LogoutArgs());
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Logout deletes the seed so the token cannot be revived from the database")]
        public void Logout_DeletesSeed_TokenCannotBeRevived()
        {
            var accessToken = LoginAsSeedUser();
            var bo = CreateBo(accessToken);
            bo.EnterCompany(new EnterCompanyArgs { CompanyId = SeedCompanyId });

            bo.Logout(new LogoutArgs());

            // Both the cache and the seed must be gone. Clearing only the cache would let the next request rebuild the token.
            Assert.Null(_fx.GetRequiredService<ISessionInfoService>().Get(accessToken));
            Assert.Null(SessionRepository.GetSession(accessToken));
        }
    }
}
