using System.ComponentModel;
using Polhem.Base.Exceptions;
using Polhem.Base.Security;
using Polhem.Business.System;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Definition.Security;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.System;
using Polhem.Tests.Shared;

using Polhem.Definition;
namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Integration tests for <see cref="SystemBusinessObject.CreateApiKey"/>: an issued key can be verified,
    /// the plaintext appears only once (the server stores only the hash), invalid input is rejected, and remote calls pass the deployment-level authorization gate.
    /// </summary>
    /// <remarks>
    /// Each test uses a unique <c>sys_id</c> and cleans up in finally, because the physical database is shared by several parallel test processes.
    /// Tests that need a user row create their own (see <see cref="TestUsers"/>) and leave seed user '001' untouched.
    /// These BOs use <c>DbScope.Common</c>, and the test fixture binds <c>common</c> to SQL Server,
    /// so the gate must be <c>SQLServer</c>. When it was marked <c>SQLite</c>, skipping depended on
    /// <c>POLHEM_TEST_CONNSTR_SQLITE</c> while the test actually ran against SQL Server.
    /// </remarks>
    public class SystemBusinessObjectApiKeyTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectApiKeyTests(SharedDbFixture fx) { _fx = fx; }

        /// <summary>
        /// A local caller (<c>isLocalCall: true</c>), the call path used on the host at deployment time.
        /// </summary>
        private SystemBusinessObject CreateBo()
            // `isLocalCall: true` is the point here, not boilerplate: these tests verify exactly the path where a local call can mint a key
            // without being a deployment admin. Since the default changed to false it must be written out.
            => new SystemBusinessObject(TestPolhemContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall: true);

        private static string NewSysId() => "bo-" + Guid.NewGuid().ToString("N");

        private IDbConnectionManager ConnectionManager => _fx.GetRequiredService<IDbConnectionManager>();

        private ISessionInfoService SessionInfoService => _fx.GetRequiredService<ISessionInfoService>();

        private void DeleteKey(string sysId)
        {
            var dbType = ConnectionManager.GetConnectionInfo(DbCategoryIds.Common).DatabaseType;
            string sql = $"DELETE FROM {dbType.QuoteIdentifier("st_api_key")} " +
                         $"WHERE {dbType.QuoteIdentifier("sys_id")} = {{0}}";
            new DbAccess(DbCategoryIds.Common, ConnectionManager)
                .Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql, sysId));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("CreateApiKey returns a two-part plaintext key whose secret matches the stored hash")]
        public void CreateApiKey_ReturnsPlaintextKeyMatchingStoredHash()
        {
            string sysId = NewSysId();
            try
            {
                var result = CreateBo().CreateApiKey(new CreateApiKeyArgs
                {
                    SysId = sysId,
                    SysName = "Issued app",
                    KeyType = ApiKeyType.ThirdParty,
                    Contact = "ops@example.com",
                });

                Assert.Equal(sysId, result.SysId);
                Assert.True(ApiKeyFormat.TryParse(result.ApiKey, out string parsedId, out string secret));
                Assert.Equal(sysId, parsedId);

                var stored = _fx.GetRequiredService<IRepositoryFactory>()
                    .Create<IApiKeyRepository>().GetEnabledById(sysId);
                Assert.NotNull(stored);
                Assert.Equal("Issued app", stored!.SysName);
                Assert.Equal(ApiKeyType.ThirdParty, stored.KeyType);
                // Only the hash is persisted: the plaintext must never be recoverable from storage.
                Assert.DoesNotContain(secret, stored.HashedKey, StringComparison.Ordinal);
                Assert.True(ApiKeyHasher.VerifySecret(secret, stored.HashedKey));
            }
            finally
            {
                DeleteKey(sysId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("CreateApiKey rejects a second key with the same sys_id with a readable message instead of a unique index error")]
        public void CreateApiKey_DuplicateSysId_ThrowsUserMessage()
        {
            string sysId = NewSysId();
            var bo = CreateBo();
            try
            {
                bo.CreateApiKey(new CreateApiKeyArgs { SysId = sysId, SysName = "First" });

                var ex = Assert.Throws<UserMessageException>(() =>
                    bo.CreateApiKey(new CreateApiKeyArgs { SysId = sysId, SysName = "Second" }));

                Assert.Contains(sysId, ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                DeleteKey(sysId);
            }
        }

        [Theory]
        [DisplayName("CreateApiKey rejects an invalid sys_id (no separator characters, no uppercase)")]
        [InlineData("")]
        [InlineData("ab")]
        [InlineData("Has-Upper")]
        [InlineData("has.dot")]
        [InlineData("-leading")]
        public void CreateApiKey_InvalidSysId_ThrowsUserMessage(string sysId)
        {
            var args = new CreateApiKeyArgs { SysId = sysId, SysName = "App" };

            Assert.Throws<UserMessageException>(() => CreateBo().CreateApiKey(args));
        }

        [Fact]
        [DisplayName("CreateApiKey rejects a missing application name")]
        public void CreateApiKey_MissingSysName_ThrowsUserMessage()
        {
            var args = new CreateApiKeyArgs { SysId = NewSysId(), SysName = string.Empty };

            Assert.Throws<UserMessageException>(() => CreateBo().CreateApiKey(args));
        }

        [Fact]
        [DisplayName("CreateApiKey rejects an expiry that has already passed")]
        public void CreateApiKey_PastExpiry_ThrowsUserMessage()
        {
            var args = new CreateApiKeyArgs
            {
                SysId = NewSysId(),
                SysName = "App",
                ExpiredAt = DateTime.UtcNow.AddMinutes(-1),
            };

            Assert.Throws<UserMessageException>(() => CreateBo().CreateApiKey(args));
        }

        [Fact]
        [DisplayName("CreateApiKey throws ArgumentNullException for null args")]
        public void CreateApiKey_NullArgs_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => CreateBo().CreateApiKey(null!));
        }

        #region Deployment-level authorization of remote calls

        [Fact]
        [DisplayName("CreateApiKey rejects a remote call from a caller who is not a deployment-level administrator")]
        public void CreateApiKey_RemoteNonAdmin_ThrowsUnauthorized()
        {
            var ctx = TestPolhemContext.CreateWithOverrides(_fx,
                (typeof(IDeploymentAuthorizationService), new FakeDeploymentAuthorization(allowed: false)));
            var bo = new SystemBusinessObject(ctx, Guid.NewGuid(), SysProgIds.System, isLocalCall: false);

            // Authorization comes before input validation: even valid args are blocked, so an input error cannot mask the reason for the rejection.
            Assert.Throws<UnauthorizedAccessException>(() =>
                bo.CreateApiKey(new CreateApiKeyArgs { SysId = NewSysId(), SysName = "App" }));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("CreateApiKey issues a key for a remote call from a deployment-level administrator")]
        public void CreateApiKey_RemoteDeploymentAdmin_IssuesKey()
        {
            string userId = TestUsers.Create(ConnectionManager, "apikey-adm");
            string sysId = NewSysId();
            Guid token = Guid.Empty;
            try
            {
                CreateBo().SetDeploymentAdmin(new SetDeploymentAdminArgs
                {
                    UserId = userId,
                    IsDeploymentAdmin = true,
                });
                token = NewSession(userId);

                var result = RemoteBo(token).CreateApiKey(new CreateApiKeyArgs
                {
                    SysId = sysId,
                    SysName = "Remotely issued",
                });

                Assert.Equal(sysId, result.SysId);
                Assert.True(ApiKeyFormat.TryParse(result.ApiKey, out _, out _));
            }
            finally
            {
                Cleanup(token, userId, sysId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("CreateApiKey rejects a remote call from an ordinary user who is merely logged in")]
        public void CreateApiKey_RemoteAuthenticatedUserWithoutFlag_ThrowsUnauthorized()
        {
            string userId = TestUsers.Create(ConnectionManager, "apikey-usr");
            string sysId = NewSysId();
            Guid token = Guid.Empty;
            try
            {
                // A valid session with the flag at its default of false: exactly the case that must still be blocked after the upgrade.
                token = NewSession(userId);

                Assert.Throws<UnauthorizedAccessException>(() =>
                    RemoteBo(token).CreateApiKey(new CreateApiKeyArgs { SysId = sysId, SysName = "App" }));
            }
            finally
            {
                Cleanup(token, userId, sysId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("CreateApiKey needs no administrator for a local call, keeping the bootstrap path for the first key")]
        public void CreateApiKey_LocalCallWithoutAdmin_IssuesKey()
        {
            string userId = TestUsers.Create(ConnectionManager, "apikey-loc");
            string sysId = NewSysId();
            Guid token = Guid.Empty;
            try
            {
                token = NewSession(userId);

                // A deployment without an administrator must be able to mint its first key, otherwise the bootstrap path is broken.
                var result = new SystemBusinessObject(TestPolhemContext.Create(_fx), token, SysProgIds.System, isLocalCall: true)
                    .CreateApiKey(new CreateApiKeyArgs { SysId = sysId, SysName = "Bootstrap" });

                Assert.Equal(sysId, result.SysId);
            }
            finally
            {
                Cleanup(token, userId, sysId);
            }
        }

        /// <summary>
        /// A remote caller (<c>isLocalCall: false</c>) that goes through the real <see cref="IDeploymentAuthorizationService"/>.
        /// </summary>
        private SystemBusinessObject RemoteBo(Guid accessToken)
            => new SystemBusinessObject(TestPolhemContext.Create(_fx), accessToken, SysProgIds.System, isLocalCall: false);

        private Guid NewSession(string userId)
            => CreateBo().CreateSession(new CreateSessionArgs { UserID = userId, ExpiresIn = 600 }).AccessToken;

        private void Cleanup(Guid token, string userId, string sysId)
        {
            if (token != Guid.Empty)
                SessionInfoService.Remove(token);
            TestUsers.Delete(ConnectionManager, userId);
            DeleteKey(sysId);
        }

        private sealed class FakeDeploymentAuthorization : IDeploymentAuthorizationService
        {
            private readonly bool _allowed;

            public FakeDeploymentAuthorization(bool allowed) { _allowed = allowed; }

            public bool Can(Guid accessToken, DeploymentAction action) => _allowed;
        }

        #endregion
    }
}
