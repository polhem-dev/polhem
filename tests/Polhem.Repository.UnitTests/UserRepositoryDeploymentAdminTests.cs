using System.ComponentModel;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Repository.System;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// Verifies reading and writing <c>st_user.deployment_admin</c>, including the fallback for an unknown user.
    /// </summary>
    /// <remarks>
    /// As in <see cref="UserRepositoryLocaleTests"/>: <see cref="UserRepository"/> resolves the <c>common</c>
    /// category internally, so this is **not** a per-provider matrix, and <c>[DbFact]</c> only skips the tests when
    /// that provider is unreachable.
    ///
    /// Each test creates its own user row and leaves the seed user '001' alone. The physical database is shared by
    /// several test processes running in parallel, and sharing one row would turn "what the flag is right now" into
    /// a race (see <see cref="TestUsers"/>).
    /// </remarks>
    public class UserRepositoryDeploymentAdminTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public UserRepositoryDeploymentAdminTests(SharedDbFixture fx) { _fx = fx; }

        private IDbConnectionManager ConnectionManager => _fx.GetRequiredService<IDbConnectionManager>();

        private UserRepository CreateRepo() => new UserRepository(TestRepositoryContext.Create(ConnectionManager), Guid.Empty, string.Empty);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("IsDeploymentAdmin reads back the value written by SetDeploymentAdmin")]
        public void SetDeploymentAdmin_RoundTrips()
        {
            string userId = TestUsers.Create(ConnectionManager, "repo-admin");
            try
            {
                var repo = CreateRepo();

                Assert.True(repo.SetDeploymentAdmin(userId, true));
                Assert.True(repo.IsDeploymentAdmin(userId));

                Assert.True(repo.SetDeploymentAdmin(userId, false));
                Assert.False(repo.IsDeploymentAdmin(userId));
            }
            finally
            {
                TestUsers.Delete(ConnectionManager, userId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("A new user is not a deployment admin by default (the column DEFAULT applies)")]
        public void IsDeploymentAdmin_NewUser_DefaultsToFalse()
        {
            string userId = TestUsers.Create(ConnectionManager, "repo-default");
            try
            {
                Assert.False(CreateRepo().IsDeploymentAdmin(userId));
            }
            finally
            {
                TestUsers.Delete(ConnectionManager, userId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("IsDeploymentAdmin returns false for an unknown user (an authorization question, so both cases deny)")]
        public void IsDeploymentAdmin_UnknownUser_ReturnsFalse()
        {
            Assert.False(CreateRepo().IsDeploymentAdmin("no-such-user"));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SetDeploymentAdmin returns false for an unknown user instead of throwing")]
        public void SetDeploymentAdmin_UnknownUser_ReturnsFalse()
        {
            Assert.False(CreateRepo().SetDeploymentAdmin("no-such-user", true));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("A blank userId returns false without querying the database")]
        public void BlankUserId_ReturnsFalse(string userId)
        {
            var repo = CreateRepo();
            Assert.False(repo.IsDeploymentAdmin(userId));
            Assert.False(repo.SetDeploymentAdmin(userId, true));
        }
    }
}
