using System.ComponentModel;
using Polhem.Base.Exceptions;
using Polhem.Business.System;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.System;
using Polhem.Tests.Shared;

using Polhem.Definition;
namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Integration tests for <see cref="SystemBusinessObject.SetDeploymentAdmin"/>: the flag really is written to
    /// <c>st_user</c>, and an unknown user or a blank userId is rejected.
    /// </summary>
    /// <remarks>
    /// Each test creates its own user row and deletes it in finally, leaving seed user '001' untouched, because the physical
    /// database is shared by several parallel test processes (see <see cref="TestUsers"/>).
    /// These BOs use <c>DbScope.Common</c>, and the test fixture binds <c>common</c> to SQL Server,
    /// so the gate must be <c>SQLServer</c>. When it was marked <c>SQLite</c>, skipping depended on
    /// <c>POLHEM_TEST_CONNSTR_SQLITE</c> while the test actually ran against SQL Server.
    /// </remarks>
    public class SystemBusinessObjectDeploymentAdminTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectDeploymentAdminTests(SharedDbFixture fx) { _fx = fx; }

        private SystemBusinessObject CreateBo()
            // The guard of `SetDeploymentAdmin` only admits local calls, so this must be declared explicitly.
            => new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: true);

        private IDbConnectionManager ConnectionManager => _fx.GetRequiredService<IDbConnectionManager>();

        private Repository.Abstractions.System.IUserRepository Repo
            => _fx.GetRequiredService<IRepositoryFactory>().Create<IUserRepository>();

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SetDeploymentAdmin writes the flag to st_user and can revoke it again")]
        public void SetDeploymentAdmin_GrantsThenRevokes()
        {
            string userId = TestUsers.Create(ConnectionManager, "bo-admin");
            try
            {
                var granted = CreateBo().SetDeploymentAdmin(new SetDeploymentAdminArgs
                {
                    UserId = userId,
                    IsDeploymentAdmin = true,
                });

                Assert.Equal(userId, granted.UserId);
                Assert.True(granted.IsDeploymentAdmin);
                Assert.True(Repo.IsDeploymentAdmin(userId));

                var revoked = CreateBo().SetDeploymentAdmin(new SetDeploymentAdminArgs
                {
                    UserId = userId,
                    IsDeploymentAdmin = false,
                });

                Assert.False(revoked.IsDeploymentAdmin);
                Assert.False(Repo.IsDeploymentAdmin(userId));
            }
            finally
            {
                TestUsers.Delete(ConnectionManager, userId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SetDeploymentAdmin rejects an unknown user with a readable message")]
        public void SetDeploymentAdmin_UnknownUser_ThrowsUserMessage()
        {
            var args = new SetDeploymentAdminArgs { UserId = "no-such-user", IsDeploymentAdmin = true };

            Assert.Throws<UserMessageException>(() => CreateBo().SetDeploymentAdmin(args));
        }

        [Fact]
        [DisplayName("SetDeploymentAdmin rejects a missing userId")]
        public void SetDeploymentAdmin_MissingUserId_ThrowsUserMessage()
        {
            var args = new SetDeploymentAdminArgs { UserId = string.Empty, IsDeploymentAdmin = true };

            Assert.Throws<UserMessageException>(() => CreateBo().SetDeploymentAdmin(args));
        }

        [Fact]
        [DisplayName("SetDeploymentAdmin throws ArgumentNullException for null args")]
        public void SetDeploymentAdmin_NullArgs_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => CreateBo().SetDeploymentAdmin(null!));
        }
    }
}
