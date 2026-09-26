using System.ComponentModel;
using Polhem.Base.Security;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Definition.Security;
using Polhem.Repository.System;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// Read/write tests for <see cref="ApiKeyRepository"/>: the round-trip of hashed keys, and the compatibility gate
    /// (<c>GetGateState</c>) turning in force once an enabled key exists.
    /// </summary>
    /// <remarks>
    /// Each test uses a unique <c>sys_id</c> (`rt-{guid}`) and cleans up in finally, because the physical database is
    /// shared by several test processes running in parallel.
    /// </remarks>
    public class ApiKeyRepositoryTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public ApiKeyRepositoryTests(SharedDbFixture fx) { _fx = fx; }

        private ApiKeyRepository CreateRepo(DatabaseType databaseType)
            => new ApiKeyRepository(
                TestRepositoryContext.Create(
                    _fx.GetRequiredService<IDbConnectionManager>(),
                    router: new ProviderScopedRouter(databaseType)),
                Guid.Empty,
                string.Empty);

        private static string NewSysId() => "rt-" + Guid.NewGuid().ToString("N");

        private void DeleteKey(DatabaseType databaseType, string sysId)
        {
            string sql = $"DELETE FROM {databaseType.QuoteIdentifier("st_api_key")} " +
                         $"WHERE {databaseType.QuoteIdentifier("sys_id")} = {{0}}";
            _fx.NewDbAccess(TestDbConventions.GetDatabaseId(databaseType, DbCategoryIds.Common))
                .Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql, sysId));
        }

        #region Insert + GetEnabledById round-trip

        private void RunRoundTrip(DatabaseType databaseType)
        {
            var repo = CreateRepo(databaseType);
            string sysId = NewSysId();
            string secret = ApiKeyFormat.CreateSecret();
            var expiredAt = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            try
            {
                repo.Insert(new ApiKeyInfo
                {
                    SysId = sysId,
                    SysName = "Round-trip app",
                    HashedKey = ApiKeyHasher.HashSecret(secret),
                    KeyType = ApiKeyType.ThirdParty,
                    Contact = "ops@example.com",
                    ExpiredAt = expiredAt,
                });

                var actual = repo.GetEnabledById(sysId);

                Assert.NotNull(actual);
                Assert.Equal(sysId, actual!.SysId);
                Assert.Equal("Round-trip app", actual.SysName);
                Assert.Equal(ApiKeyType.ThirdParty, actual.KeyType);
                Assert.Equal("ops@example.com", actual.Contact);
                Assert.Equal(expiredAt, actual.ExpiredAt);
                // The stored form verifies against the plaintext secret and only against it.
                Assert.True(ApiKeyHasher.VerifySecret(secret, actual.HashedKey));
                Assert.False(ApiKeyHasher.VerifySecret(ApiKeyFormat.CreateSecret(), actual.HashedKey));
            }
            finally
            {
                DeleteKey(databaseType, sysId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("GetEnabledById reads back the full key row after Insert on SQL Server")]
        public void Insert_ThenGet_SqlServer() => RunRoundTrip(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("GetEnabledById reads back the full key row after Insert on PostgreSQL")]
        public void Insert_ThenGet_PostgreSql() => RunRoundTrip(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("GetEnabledById reads back the full key row after Insert on SQLite")]
        public void Insert_ThenGet_Sqlite() => RunRoundTrip(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("GetEnabledById reads back the full key row after Insert on MySQL")]
        public void Insert_ThenGet_MySql() => RunRoundTrip(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("GetEnabledById reads back the full key row after Insert on Oracle")]
        public void Insert_ThenGet_Oracle() => RunRoundTrip(DatabaseType.Oracle);

        #endregion

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("GetEnabledById returns a null expiry when Insert specified no expiry")]
        public void Insert_WithoutExpiry_ReadsBackNull()
        {
            const DatabaseType databaseType = DatabaseType.SQLite;
            var repo = CreateRepo(databaseType);
            string sysId = NewSysId();
            try
            {
                repo.Insert(new ApiKeyInfo
                {
                    SysId = sysId,
                    SysName = "No expiry app",
                    HashedKey = ApiKeyHasher.HashSecret(ApiKeyFormat.CreateSecret()),
                });

                var actual = repo.GetEnabledById(sysId);

                Assert.NotNull(actual);
                Assert.Null(actual!.ExpiredAt);
                Assert.False(actual.IsExpired(DateTime.UtcNow));
            }
            finally
            {
                DeleteKey(databaseType, sysId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("GetEnabledById returns null for an unknown sys_id")]
        public void GetEnabledById_UnknownSysId_ReturnsNull()
        {
            Assert.Null(CreateRepo(DatabaseType.SQLite).GetEnabledById(NewSysId()));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Exists is false before the insert and true after it")]
        public void Exists_ReflectsRowPresence()
        {
            const DatabaseType databaseType = DatabaseType.SQLite;
            var repo = CreateRepo(databaseType);
            string sysId = NewSysId();
            try
            {
                Assert.False(repo.Exists(sysId));

                repo.Insert(new ApiKeyInfo
                {
                    SysId = sysId,
                    SysName = "Exists app",
                    HashedKey = ApiKeyHasher.HashSecret(ApiKeyFormat.CreateSecret()),
                });

                Assert.True(repo.Exists(sysId));
            }
            finally
            {
                DeleteKey(databaseType, sysId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("GetGateState is in force when an enabled key exists (issuing the first key closes the gate)")]
        public void GetGateState_WithEnabledKey_IsInForce()
        {
            const DatabaseType databaseType = DatabaseType.SQLite;
            var repo = CreateRepo(databaseType);
            string sysId = NewSysId();
            try
            {
                repo.Insert(new ApiKeyInfo
                {
                    SysId = sysId,
                    SysName = "Gate app",
                    HashedKey = ApiKeyHasher.HashSecret(ApiKeyFormat.CreateSecret()),
                });

                var gate = repo.GetGateState();

                Assert.True(gate.InForce);
            }
            finally
            {
                DeleteKey(databaseType, sysId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("GetGateState does not throw when the table exists (table existence is decided by the schema provider)")]
        public void GetGateState_TableExists_DoesNotThrow()
        {
            var exception = Record.Exception(() => CreateRepo(DatabaseType.SQLite).GetGateState());

            Assert.Null(exception);
        }
    }
}
