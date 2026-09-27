using System.ComponentModel;
using System.Data;
using System.Globalization;
using Polhem.Base.Security;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Repository.System;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// Seed read/write tests for <see cref="SessionRepository"/>, one run per provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>st_session</c> stores a rebuild seed, not a SessionInfo snapshot: only the values that cannot be derived
    /// again (token, user, expiry, company). So the tests focus on the round-trip and on the effect of the insert,
    /// update and delete operations.
    /// </para>
    /// <para>
    /// The token itself is never stored: the key column holds <see cref="AccessTokenHasher.ComputeStorageKey"/> of
    /// it and the seed XML leaves it out, which the stored-row tests below check on each provider.
    /// </para>
    /// <para>
    /// The tests in this class use <see cref="ProviderScopedRouter"/> to route <c>DbScope.Common</c> to that
    /// provider's test database. With the default routing they would all land on SQL Server. The placeholder order
    /// of <c>UpdateSession</c> is <c>{1} {2} {0}</c>, and Oracle's positional binding sent a <c>DateTime</c> into
    /// <c>access_token</c> (RAW(16)). That defect was exactly on this path, yet it was found by a load test rather
    /// than by a test.
    /// </para>
    /// </remarks>
    public class SessionRepositoryTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public SessionRepositoryTests(SharedDbFixture fx) { _fx = fx; }

        private SessionRepository CreateRepo(DatabaseType databaseType)
            => new SessionRepository(
                TestRepositoryContext.Create(
                    _fx.GetRequiredService<IDbConnectionManager>(),
                    router: new ProviderScopedRouter(databaseType)),
                Guid.Empty,
                string.Empty);

        private static SessionUser CreateSeed(int expiresInSeconds = 3600, string? companyId = null)
            => new SessionUser
            {
                AccessToken = Guid.NewGuid(),
                UserID = "001",
                UserName = "測試管理員",
                EndTime = DateTime.UtcNow.AddSeconds(expiresInSeconds),
                CompanyId = companyId,
            };

        #region InsertSession + GetSession round-trip

        private void RunInsertThenGet(DatabaseType databaseType)
        {
            var repo = CreateRepo(databaseType);
            var seed = CreateSeed(companyId: "C001");

            repo.InsertSession(seed);

            var actual = repo.GetSession(seed.AccessToken);
            Assert.NotNull(actual);
            Assert.Equal(seed.AccessToken, actual!.AccessToken);
            Assert.Equal("001", actual.UserID);
            Assert.Equal("測試管理員", actual.UserName);
            Assert.Equal("C001", actual.CompanyId);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("A seed written by InsertSession is fully read back by GetSession (SQL Server)")]
        public void InsertSession_ThenGetSession_RoundTrips_SqlServer() => RunInsertThenGet(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("A seed written by InsertSession is fully read back by GetSession (PostgreSQL)")]
        public void InsertSession_ThenGetSession_RoundTrips_PostgreSql() => RunInsertThenGet(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A seed written by InsertSession is fully read back by GetSession (SQLite)")]
        public void InsertSession_ThenGetSession_RoundTrips_Sqlite() => RunInsertThenGet(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("A seed written by InsertSession is fully read back by GetSession (MySQL)")]
        public void InsertSession_ThenGetSession_RoundTrips_MySql() => RunInsertThenGet(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("A seed written by InsertSession is fully read back by GetSession (Oracle)")]
        public void InsertSession_ThenGetSession_RoundTrips_Oracle() => RunInsertThenGet(DatabaseType.Oracle);

        #endregion

        #region The stored row carries neither form of the token

        private void RunStoredRowHoldsNoToken(DatabaseType databaseType)
        {
            var repo = CreateRepo(databaseType);
            var seed = CreateSeed(companyId: "C001");
            repo.InsertSession(seed);

            var dbAccess = new DbAccess(TestDbConventions.GetDatabaseId(databaseType, DbCategoryIds.Common),
                _fx.GetRequiredService<IDbConnectionManager>());
            const string sql = "SELECT session_user_xml FROM st_session WHERE access_token={0}";

            // The row is keyed by the storage hash; the token itself finds nothing.
            var byToken = dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable, sql, seed.AccessToken)).Table!;
            var byHash = dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable, sql,
                AccessTokenHasher.ComputeStorageKey(seed.AccessToken))).Table!;
            Assert.Empty(byToken.Rows.Cast<DataRow>());
            string xml = Convert.ToString(Assert.Single(byHash.Rows.Cast<DataRow>())[0], CultureInfo.InvariantCulture)!;

            // Nor does the seed XML carry the token, in any of its usual spellings.
            Assert.DoesNotContain(seed.AccessToken.ToString("N"), xml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(seed.AccessToken.ToString("D"), xml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("AccessToken", xml, StringComparison.Ordinal);

            // The token is restored from the request on read.
            Assert.Equal(seed.AccessToken, repo.GetSession(seed.AccessToken)!.AccessToken);
            repo.DeleteSession(seed.AccessToken);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("st_session stores a hash of the token and a seed without it, and GetSession restores the token (SQL Server)")]
        public void InsertSession_StoresNoToken_SqlServer() => RunStoredRowHoldsNoToken(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("st_session stores a hash of the token and a seed without it, and GetSession restores the token (PostgreSQL)")]
        public void InsertSession_StoresNoToken_PostgreSql() => RunStoredRowHoldsNoToken(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("st_session stores a hash of the token and a seed without it, and GetSession restores the token (SQLite)")]
        public void InsertSession_StoresNoToken_Sqlite() => RunStoredRowHoldsNoToken(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("st_session stores a hash of the token and a seed without it, and GetSession restores the token (MySQL)")]
        public void InsertSession_StoresNoToken_MySql() => RunStoredRowHoldsNoToken(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("st_session stores a hash of the token and a seed without it, and GetSession restores the token (Oracle)")]
        public void InsertSession_StoresNoToken_Oracle() => RunStoredRowHoldsNoToken(DatabaseType.Oracle);

        #endregion

        #region GetSession — nonexistent token

        private void RunGetSessionNotFound(DatabaseType databaseType)
        {
            Assert.Null(CreateRepo(databaseType).GetSession(Guid.NewGuid()));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("GetSession returns null for a nonexistent AccessToken (SQL Server)")]
        public void GetSession_NonExistentToken_ReturnsNull_SqlServer() => RunGetSessionNotFound(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("GetSession returns null for a nonexistent AccessToken (PostgreSQL)")]
        public void GetSession_NonExistentToken_ReturnsNull_PostgreSql() => RunGetSessionNotFound(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("GetSession returns null for a nonexistent AccessToken (SQLite)")]
        public void GetSession_NonExistentToken_ReturnsNull_Sqlite() => RunGetSessionNotFound(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("GetSession returns null for a nonexistent AccessToken (MySQL)")]
        public void GetSession_NonExistentToken_ReturnsNull_MySql() => RunGetSessionNotFound(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("GetSession returns null for a nonexistent AccessToken (Oracle)")]
        public void GetSession_NonExistentToken_ReturnsNull_Oracle() => RunGetSessionNotFound(DatabaseType.Oracle);

        #endregion

        #region GetSession — expired seed

        private void RunGetSessionExpired(DatabaseType databaseType)
        {
            var repo = CreateRepo(databaseType);
            var seed = CreateSeed(expiresInSeconds: -3600);
            repo.InsertSession(seed);

            Assert.Null(repo.GetSession(seed.AccessToken));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("GetSession returns null for an expired seed (SQL Server)")]
        public void GetSession_ExpiredSeed_ReturnsNull_SqlServer() => RunGetSessionExpired(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("GetSession returns null for an expired seed (PostgreSQL)")]
        public void GetSession_ExpiredSeed_ReturnsNull_PostgreSql() => RunGetSessionExpired(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("GetSession returns null for an expired seed (SQLite)")]
        public void GetSession_ExpiredSeed_ReturnsNull_Sqlite() => RunGetSessionExpired(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("GetSession returns null for an expired seed (MySQL)")]
        public void GetSession_ExpiredSeed_ReturnsNull_MySql() => RunGetSessionExpired(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("GetSession returns null for an expired seed (Oracle)")]
        public void GetSession_ExpiredSeed_ReturnsNull_Oracle() => RunGetSessionExpired(DatabaseType.Oracle);

        #endregion

        #region UpdateSession — placeholder order {1} {2} {0}, where positional binding goes wrong

        private void RunUpdateSession(DatabaseType databaseType)
        {
            var repo = CreateRepo(databaseType);
            var seed = CreateSeed();
            repo.InsertSession(seed);

            seed.CompanyId = "C002";
            repo.UpdateSession(seed);

            Assert.Equal("C002", repo.GetSession(seed.AccessToken)!.CompanyId);

            // Leaving the company clears it, so a rebuild does not put the user back into a company they left.
            seed.CompanyId = null;
            repo.UpdateSession(seed);

            Assert.Null(repo.GetSession(seed.AccessToken)!.CompanyId);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("UpdateSession overwrites the CompanyId of an existing seed (SQL Server)")]
        public void UpdateSession_OverwritesCompanyId_SqlServer() => RunUpdateSession(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("UpdateSession overwrites the CompanyId of an existing seed (PostgreSQL)")]
        public void UpdateSession_OverwritesCompanyId_PostgreSql() => RunUpdateSession(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("UpdateSession overwrites the CompanyId of an existing seed (SQLite)")]
        public void UpdateSession_OverwritesCompanyId_Sqlite() => RunUpdateSession(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("UpdateSession overwrites the CompanyId of an existing seed (MySQL)")]
        public void UpdateSession_OverwritesCompanyId_MySql() => RunUpdateSession(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("UpdateSession overwrites the CompanyId of an existing seed (Oracle)")]
        public void UpdateSession_OverwritesCompanyId_Oracle() => RunUpdateSession(DatabaseType.Oracle);

        #endregion

        #region DeleteSession

        private void RunDeleteSession(DatabaseType databaseType)
        {
            var repo = CreateRepo(databaseType);
            var seed = CreateSeed();
            repo.InsertSession(seed);

            repo.DeleteSession(seed.AccessToken);
            Assert.Null(repo.GetSession(seed.AccessToken));

            var exception = Record.Exception(() => repo.DeleteSession(seed.AccessToken));
            Assert.Null(exception);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("DeleteSession deletes the seed and a repeated call is idempotent (SQL Server)")]
        public void DeleteSession_RemovesSeed_AndIsIdempotent_SqlServer() => RunDeleteSession(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("DeleteSession deletes the seed and a repeated call is idempotent (PostgreSQL)")]
        public void DeleteSession_RemovesSeed_AndIsIdempotent_PostgreSql() => RunDeleteSession(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("DeleteSession deletes the seed and a repeated call is idempotent (SQLite)")]
        public void DeleteSession_RemovesSeed_AndIsIdempotent_Sqlite() => RunDeleteSession(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("DeleteSession deletes the seed and a repeated call is idempotent (MySQL)")]
        public void DeleteSession_RemovesSeed_AndIsIdempotent_MySql() => RunDeleteSession(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("DeleteSession deletes the seed and a repeated call is idempotent (Oracle)")]
        public void DeleteSession_RemovesSeed_AndIsIdempotent_Oracle() => RunDeleteSession(DatabaseType.Oracle);

        #endregion

        #region GetSession has no side effect + DeleteExpiredSessions

        private void RunGetSessionHasNoSideEffect(DatabaseType databaseType)
        {
            var repo = CreateRepo(databaseType);
            var expired = CreateSeed(expiresInSeconds: -3600);
            repo.InsertSession(expired);

            // Expired rows are filtered out by the query condition; there is no delete-on-read.
            Assert.Null(repo.GetSession(expired.AccessToken));
            // The row is still there after the read and is left to the cleanup schedule. If the read still deleted
            // it, there would be nothing left to delete here.
            Assert.True(repo.DeleteExpiredSessions() >= 1);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("GetSession performs no writes (side-effect-free read, SQL Server)")]
        public void GetSession_HasNoSideEffect_SqlServer() => RunGetSessionHasNoSideEffect(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("GetSession performs no writes (side-effect-free read, PostgreSQL)")]
        public void GetSession_HasNoSideEffect_PostgreSql() => RunGetSessionHasNoSideEffect(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("GetSession performs no writes (side-effect-free read, SQLite)")]
        public void GetSession_HasNoSideEffect_Sqlite() => RunGetSessionHasNoSideEffect(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("GetSession performs no writes (side-effect-free read, MySQL)")]
        public void GetSession_HasNoSideEffect_MySql() => RunGetSessionHasNoSideEffect(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("GetSession performs no writes (side-effect-free read, Oracle)")]
        public void GetSession_HasNoSideEffect_Oracle() => RunGetSessionHasNoSideEffect(DatabaseType.Oracle);

        #endregion

        #region DeleteExpiredSessions deletes only expired rows

        private void RunDeleteExpiredSessions(DatabaseType databaseType)
        {
            var repo = CreateRepo(databaseType);
            var live = CreateSeed();
            var expired = CreateSeed(expiresInSeconds: -3600);
            repo.InsertSession(live);
            repo.InsertSession(expired);

            repo.DeleteExpiredSessions();

            Assert.NotNull(repo.GetSession(live.AccessToken));

            // Idempotent: a second run must not affect unexpired rows or throw.
            var exception = Record.Exception(() => repo.DeleteExpiredSessions());
            Assert.Null(exception);
            Assert.NotNull(repo.GetSession(live.AccessToken));

            repo.DeleteSession(live.AccessToken);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("DeleteExpiredSessions deletes expired rows, keeps unexpired ones and is idempotent (SQL Server)")]
        public void DeleteExpiredSessions_RemovesOnlyExpired_AndIsIdempotent_SqlServer() => RunDeleteExpiredSessions(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("DeleteExpiredSessions deletes expired rows, keeps unexpired ones and is idempotent (PostgreSQL)")]
        public void DeleteExpiredSessions_RemovesOnlyExpired_AndIsIdempotent_PostgreSql() => RunDeleteExpiredSessions(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("DeleteExpiredSessions deletes expired rows, keeps unexpired ones and is idempotent (SQLite)")]
        public void DeleteExpiredSessions_RemovesOnlyExpired_AndIsIdempotent_Sqlite() => RunDeleteExpiredSessions(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("DeleteExpiredSessions deletes expired rows, keeps unexpired ones and is idempotent (MySQL)")]
        public void DeleteExpiredSessions_RemovesOnlyExpired_AndIsIdempotent_MySql() => RunDeleteExpiredSessions(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("DeleteExpiredSessions deletes expired rows, keeps unexpired ones and is idempotent (Oracle)")]
        public void DeleteExpiredSessions_RemovesOnlyExpired_AndIsIdempotent_Oracle() => RunDeleteExpiredSessions(DatabaseType.Oracle);

        #endregion

        #region Seed without CompanyId

        private void RunSeedWithoutCompanyId(DatabaseType databaseType)
        {
            var repo = CreateRepo(databaseType);
            var seed = CreateSeed();

            repo.InsertSession(seed);

            Assert.Null(repo.GetSession(seed.AccessToken)!.CompanyId);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("A seed without CompanyId rebuilds into the no-company state (SQL Server)")]
        public void GetSession_SeedWithoutCompanyId_RebuildsAsCompanyLess_SqlServer() => RunSeedWithoutCompanyId(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("A seed without CompanyId rebuilds into the no-company state (PostgreSQL)")]
        public void GetSession_SeedWithoutCompanyId_RebuildsAsCompanyLess_PostgreSql() => RunSeedWithoutCompanyId(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A seed without CompanyId rebuilds into the no-company state (SQLite)")]
        public void GetSession_SeedWithoutCompanyId_RebuildsAsCompanyLess_Sqlite() => RunSeedWithoutCompanyId(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("A seed without CompanyId rebuilds into the no-company state (MySQL)")]
        public void GetSession_SeedWithoutCompanyId_RebuildsAsCompanyLess_MySql() => RunSeedWithoutCompanyId(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("A seed without CompanyId rebuilds into the no-company state (Oracle)")]
        public void GetSession_SeedWithoutCompanyId_RebuildsAsCompanyLess_Oracle() => RunSeedWithoutCompanyId(DatabaseType.Oracle);

        #endregion
    }
}
