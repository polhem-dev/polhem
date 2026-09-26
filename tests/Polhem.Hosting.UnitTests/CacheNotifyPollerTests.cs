using System.ComponentModel;
using Polhem.Db.CacheNotify;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Hosting.CacheNotify;
using Polhem.ObjectCaching;
using Polhem.Tests.Shared;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// Integration tests for <see cref="CacheNotifyPollSession"/> against a live database per
    /// dialect. Drives the polling core directly (no timer) and observes the real
    /// <see cref="ICacheContainer.CompanyInfo"/> cache: a bump of <c>"CompanyInfo:{id}"</c> via
    /// <see cref="ICacheNotifyService"/> must, after a poll, evict that company entry through the
    /// container's convention-based dispatch. Tests skip when the dialect's
    /// <c>POLHEM_TEST_CONNSTR_*</c> env var is unset.
    /// </summary>
    public class CacheNotifyPollerTests : IClassFixture<SharedDbFixture>
    {
        private const string CompanyGroup = nameof(CompanyInfo);

        private readonly SharedDbFixture _fx;

        public CacheNotifyPollerTests(SharedDbFixture fx) { _fx = fx; }

        // Bumps the key in its own committed transaction, mirroring a business write path.
        private void Touch(DatabaseType databaseType, string cacheKey)
        {
            var databaseId = TestDbConventions.GetDatabaseId(databaseType);
            var connectionManager = _fx.GetRequiredService<IDbConnectionManager>();
            var service = _fx.GetRequiredService<ICacheNotifyService>();

            using var connection = connectionManager.CreateConnection(databaseId);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            service.Touch(cacheKey, transaction, databaseType);
            transaction.Commit();
        }

        private CacheNotifyPollSession NewSession(DatabaseType databaseType)
        {
            var databaseId = TestDbConventions.GetDatabaseId(databaseType);
            var reader = _fx.GetRequiredService<ICacheNotifyReader>();
            return new CacheNotifyPollSession(databaseId, reader, marginSeconds: 5);
        }

        private CompanyInfo SeedCompany(string companyId)
        {
            var container = _fx.GetRequiredService<ICacheContainer>();
            var info = new CompanyInfo { CompanyId = companyId, CompanyName = "RT " + companyId };
            container.CompanyInfo.Set(info);
            return info;
        }

        // Baseline poll evicts nothing; a post-baseline bump of "CompanyInfo:{id}" is observed and
        // routes through the container to evict that company once; a repeat poll without a new bump
        // is idempotent (version comparison). The id is unique per invocation so the incremental
        // window cannot catch another test's bump on the shared database.
        private void RunPollerLifecycle(DatabaseType databaseType)
        {
            var container = _fx.GetRequiredService<ICacheContainer>();
            string companyId = "RT_" + Guid.NewGuid().ToString("N");
            SeedCompany(companyId);

            var session = NewSession(databaseType);

            session.Poll(); // baseline only — does not evict
            Assert.NotNull(container.CompanyInfo.Get(companyId));

            Touch(databaseType, $"{CompanyGroup}:{companyId}");

            session.Poll(); // delta → convention dispatch evicts the company
            Assert.Null(container.CompanyInfo.Get(companyId));

            // No new bump: re-seed then poll again; the unchanged version must not evict again.
            SeedCompany(companyId);
            session.Poll();
            Assert.NotNull(container.CompanyInfo.Get(companyId));
        }

        // A bump whose group maps to no cache is ignored: an unrelated seeded company survives.
        private void RunUnroutedGroupIgnored(DatabaseType databaseType)
        {
            var container = _fx.GetRequiredService<ICacheContainer>();
            string companyId = "RT_" + Guid.NewGuid().ToString("N");
            SeedCompany(companyId);

            var session = NewSession(databaseType);
            session.Poll(); // baseline

            Touch(databaseType, $"NoSuchCacheGroup_{Guid.NewGuid():N}:{Guid.NewGuid():N}");

            session.Poll(); // delta sees the row but no cache owns its group
            Assert.NotNull(container.CompanyInfo.Get(companyId));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: after a bump the poller evicts CompanyInfo through convention dispatch, and a repeat poll is idempotent")]
        public void Poller_SqlServer_Lifecycle() => RunPollerLifecycle(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: after a bump the poller evicts CompanyInfo through convention dispatch, and a repeat poll is idempotent")]
        public void Poller_PostgreSQL_Lifecycle() => RunPollerLifecycle(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: after a bump the poller evicts CompanyInfo through convention dispatch, and a repeat poll is idempotent")]
        public void Poller_MySQL_Lifecycle() => RunPollerLifecycle(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: after a bump the poller evicts CompanyInfo through convention dispatch, and a repeat poll is idempotent")]
        public void Poller_Oracle_Lifecycle() => RunPollerLifecycle(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a bump of a group with no matching cache evicts nothing")]
        public void Poller_SqlServer_UnroutedIgnored() => RunUnroutedGroupIgnored(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a bump of a group with no matching cache evicts nothing")]
        public void Poller_PostgreSQL_UnroutedIgnored() => RunUnroutedGroupIgnored(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a bump of a group with no matching cache evicts nothing")]
        public void Poller_MySQL_UnroutedIgnored() => RunUnroutedGroupIgnored(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a bump of a group with no matching cache evicts nothing")]
        public void Poller_Oracle_UnroutedIgnored() => RunUnroutedGroupIgnored(DatabaseType.Oracle);
    }
}
