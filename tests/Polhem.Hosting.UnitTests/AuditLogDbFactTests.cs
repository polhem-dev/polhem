using System.ComponentModel;
using System.Globalization;
using Polhem.Db;
using Polhem.Definition.Database;
using Polhem.Definition.Logging;
using Polhem.Repository.AuditLog;
using Polhem.Tests.Shared;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// End-to-end integration tests: the actual INSERT produced by the sink writes one record to a log table
    /// (<c>st_log_login</c>, <c>st_log_change</c>, <c>st_log_access</c>, <c>st_log_anomaly_api</c>,
    /// <c>st_log_anomaly_db</c>) of the log database, and the record is read back. They run against a real database,
    /// one per dialect, and are skipped automatically when the matching <c>POLHEM_TEST_CONNSTR_*</c> is not set.
    /// </summary>
    public class AuditLogDbFactTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public AuditLogDbFactTests(SharedDbFixture fx) { _fx = fx; }

        private void RunLoginRoundTrip(DatabaseType databaseType)
        {
            var databaseId = TestDbConventions.GetDatabaseId(databaseType, "log");
            var factory = _fx.GetRequiredService<IDbAccessFactory>();
            var dbAccess = factory.Create(databaseId);

            var rowId = Guid.NewGuid();
            var entry = new LoginAuditEntry
            {
                SysRowId = rowId,
                UserId = "demo",
                UserName = "Demo User",
                AccessToken = Guid.NewGuid(),
                Event = LoginEvent.LoginSucceeded,
            };

            // Executes the exact INSERT the sink produces, against the test log database.
            dbAccess.Execute(AuditLogWriteRepository.BuildInsert(entry));

            var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT COUNT(*) FROM st_log_login WHERE sys_rowid={0}", rowId));
            var count = Convert.ToInt64(result.Scalar, CultureInfo.InvariantCulture);

            Assert.Equal(1L, count);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a login record written to st_log_login can be read back")]
        public void LoginLog_SqlServer_RoundTrip() => RunLoginRoundTrip(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a login record written to st_log_login can be read back")]
        public void LoginLog_PostgreSQL_RoundTrip() => RunLoginRoundTrip(DatabaseType.PostgreSQL);

        private void RunChangeRoundTrip(DatabaseType databaseType)
        {
            var databaseId = TestDbConventions.GetDatabaseId(databaseType, "log");
            var factory = _fx.GetRequiredService<IDbAccessFactory>();
            var dbAccess = factory.Create(databaseId);

            var rowId = Guid.NewGuid();
            var entry = new ChangeAuditEntry
            {
                SysRowId = rowId,
                UserId = "demo",
                UserName = "Demo User",
                CompanyId = "c1",
                CompanyName = "Company One",
                ProgId = "Employee",
                ChangeTableName = "st_employee",
                RowKey = Guid.NewGuid().ToString(),
                ChangeKind = ChangeKind.Update,
                IsSensitive = false,
                ChangesXml = "<diffgr:diffgram xmlns:diffgr=\"urn:schemas-microsoft-com:xml-diffgram-v1\" />",
            };

            dbAccess.Execute(AuditLogWriteRepository.BuildInsert(entry));

            var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT COUNT(*) FROM st_log_change WHERE sys_rowid={0}", rowId));
            var count = Convert.ToInt64(result.Scalar, CultureInfo.InvariantCulture);

            Assert.Equal(1L, count);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a change record written to st_log_change can be read back")]
        public void ChangeLog_SqlServer_RoundTrip() => RunChangeRoundTrip(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a change record written to st_log_change can be read back")]
        public void ChangeLog_PostgreSQL_RoundTrip() => RunChangeRoundTrip(DatabaseType.PostgreSQL);

        private void RunAccessRoundTrip(DatabaseType databaseType)
        {
            var databaseId = TestDbConventions.GetDatabaseId(databaseType, "log");
            var factory = _fx.GetRequiredService<IDbAccessFactory>();
            var dbAccess = factory.Create(databaseId);

            var rowId = Guid.NewGuid();
            var entry = new AccessAuditEntry
            {
                SysRowId = rowId,
                UserId = "demo",
                UserName = "Demo User",
                CompanyId = "c1",
                CompanyName = "Company One",
                ProgId = "Order",
                RowKey = Guid.NewGuid().ToString(),
            };

            dbAccess.Execute(AuditLogWriteRepository.BuildInsert(entry));

            var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT COUNT(*) FROM st_log_access WHERE sys_rowid={0}", rowId));
            var count = Convert.ToInt64(result.Scalar, CultureInfo.InvariantCulture);

            Assert.Equal(1L, count);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a view record written to st_log_access can be read back")]
        public void AccessLog_SqlServer_RoundTrip() => RunAccessRoundTrip(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a view record written to st_log_access can be read back")]
        public void AccessLog_PostgreSQL_RoundTrip() => RunAccessRoundTrip(DatabaseType.PostgreSQL);

        private void RunApiAnomalyRoundTrip(DatabaseType databaseType)
        {
            var factory = _fx.GetRequiredService<IDbAccessFactory>();
            var dbAccess = factory.Create(TestDbConventions.GetDatabaseId(databaseType, "log"));

            var rowId = Guid.NewGuid();
            var entry = new ApiAnomalyEntry
            {
                SysRowId = rowId,
                UserId = "demo",
                UserName = "Demo User",
                Method = "Order.Save",
                Kind = AnomalyKind.Slow,
                ElapsedMs = 5000,
                ThresholdMs = 3000,
            };
            dbAccess.Execute(AuditLogWriteRepository.BuildInsert(entry));

            var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT COUNT(*) FROM st_log_anomaly_api WHERE sys_rowid={0}", rowId));
            Assert.Equal(1L, Convert.ToInt64(result.Scalar, CultureInfo.InvariantCulture));
        }

        private void RunDbAnomalyRoundTrip(DatabaseType databaseType)
        {
            var factory = _fx.GetRequiredService<IDbAccessFactory>();
            var dbAccess = factory.Create(TestDbConventions.GetDatabaseId(databaseType, "log"));

            var rowId = Guid.NewGuid();
            var entry = new DbAnomalyEntry
            {
                SysRowId = rowId,
                DatabaseId = "company",
                Command = "UPDATE ft_order SET amount={0}",
                Kind = AnomalyKind.Timeout,
                ElapsedMs = 30000,
                ErrorType = "DbException",
                ErrorMessage = "timeout expired",
            };
            dbAccess.Execute(AuditLogWriteRepository.BuildInsert(entry));

            var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT COUNT(*) FROM st_log_anomaly_db WHERE sys_rowid={0}", rowId));
            Assert.Equal(1L, Convert.ToInt64(result.Scalar, CultureInfo.InvariantCulture));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: an API anomaly written to st_log_anomaly_api can be read back")]
        public void ApiAnomaly_SqlServer_RoundTrip() => RunApiAnomalyRoundTrip(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: an API anomaly written to st_log_anomaly_api can be read back")]
        public void ApiAnomaly_PostgreSQL_RoundTrip() => RunApiAnomalyRoundTrip(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a DB anomaly written to st_log_anomaly_db can be read back")]
        public void DbAnomaly_SqlServer_RoundTrip() => RunDbAnomalyRoundTrip(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a DB anomaly written to st_log_anomaly_db can be read back")]
        public void DbAnomaly_PostgreSQL_RoundTrip() => RunDbAnomalyRoundTrip(DatabaseType.PostgreSQL);
    }
}
