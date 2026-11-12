using System.ComponentModel;
using System.Data.Common;
using System.Globalization;
using Polhem.Core.Security;
using Polhem.Db;
using Polhem.Definition.Database;
using Polhem.Definition.Logging;
using Polhem.Definition.Storage;
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
                TokenFingerprint = AccessTokenHasher.ComputeFingerprint(Guid.NewGuid()),
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

        private void RunFingerprintRoundTrip(DatabaseType databaseType)
        {
            var dbAccess = _fx.GetRequiredService<IDbAccessFactory>()
                .Create(TestDbConventions.GetDatabaseId(databaseType, "log"));

            var token = Guid.NewGuid();
            string fingerprint = AccessTokenHasher.ComputeFingerprint(token)!;
            var rowId = Guid.NewGuid();
            dbAccess.Execute(AuditLogWriteRepository.BuildInsert(new AccessAuditEntry
            {
                SysRowId = rowId,
                UserId = "demo",
                TokenFingerprint = fingerprint,
                ProgId = "Order",
                RowKey = "r1",
            }));

            var stored = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT token_fingerprint FROM st_log_access WHERE sys_rowid={0}", rowId)).Scalar;

            Assert.Equal(fingerprint, Convert.ToString(stored, CultureInfo.InvariantCulture));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: st_log_access stores the session fingerprint in token_fingerprint")]
        public void TokenFingerprint_SqlServer_RoundTrip() => RunFingerprintRoundTrip(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: st_log_access stores the session fingerprint in token_fingerprint")]
        public void TokenFingerprint_PostgreSQL_RoundTrip() => RunFingerprintRoundTrip(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: st_log_access stores the session fingerprint in token_fingerprint")]
        public void TokenFingerprint_Sqlite_RoundTrip() => RunFingerprintRoundTrip(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: st_log_access stores the session fingerprint in token_fingerprint")]
        public void TokenFingerprint_MySql_RoundTrip() => RunFingerprintRoundTrip(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: st_log_access stores the session fingerprint in token_fingerprint")]
        public void TokenFingerprint_Oracle_RoundTrip() => RunFingerprintRoundTrip(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a DB anomaly written to st_log_anomaly_db can be read back")]
        public void DbAnomaly_SqlServer_RoundTrip() => RunDbAnomalyRoundTrip(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a DB anomaly written to st_log_anomaly_db can be read back")]
        public void DbAnomaly_PostgreSQL_RoundTrip() => RunDbAnomalyRoundTrip(DatabaseType.PostgreSQL);

        private AuditLogWriteRepository CreateWriteRepository(DatabaseType databaseType)
        {
            var factory = new RedirectingDbAccessFactory(
                _fx.GetRequiredService<IDbAccessFactory>(), TestDbConventions.GetDatabaseId(databaseType, "log"));
            return new AuditLogWriteRepository(
                TestRepositoryContext.Create(defineAccess: _fx.GetRequiredService<IDefineAccess>(), dbAccessFactory: factory),
                Guid.Empty, string.Empty);
        }

        private long CountLoginRows(DatabaseType databaseType, Guid rowId)
        {
            var dbAccess = _fx.GetRequiredService<IDbAccessFactory>()
                .Create(TestDbConventions.GetDatabaseId(databaseType, "log"));
            var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT COUNT(*) FROM st_log_login WHERE sys_rowid={0}", rowId));
            return Convert.ToInt64(result.Scalar, CultureInfo.InvariantCulture);
        }

        private static LoginAuditEntry NewLoginEntry(Guid rowId) => new()
        {
            SysRowId = rowId,
            UserId = "demo",
            Event = LoginEvent.LoginSucceeded,
        };

        private void RunWriteBatchPersistsEveryEntry(DatabaseType databaseType)
        {
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();

            CreateWriteRepository(databaseType).WriteBatch([NewLoginEntry(first), NewLoginEntry(second)]);

            Assert.Equal(1L, CountLoginRows(databaseType, first));
            Assert.Equal(1L, CountLoginRows(databaseType, second));
        }

        private void RunWriteBatchFailureWritesNothing(DatabaseType databaseType)
        {
            var first = Guid.NewGuid();
            var duplicate = Guid.NewGuid();
            var repository = CreateWriteRepository(databaseType);

            // The second and third entries share a `sys_rowid`, so the third violates the unique index.
            Assert.ThrowsAny<DbException>(() => repository.WriteBatch(
                [NewLoginEntry(first), NewLoginEntry(duplicate), NewLoginEntry(duplicate)]));

            Assert.Equal(0L, CountLoginRows(databaseType, first));
            Assert.Equal(0L, CountLoginRows(databaseType, duplicate));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: WriteBatch persists every entry of the batch")]
        public void WriteBatch_SqlServer_PersistsEveryEntry() => RunWriteBatchPersistsEveryEntry(DatabaseType.SQLServer);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: WriteBatch persists every entry of the batch")]
        public void WriteBatch_Sqlite_PersistsEveryEntry() => RunWriteBatchPersistsEveryEntry(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a WriteBatch whose last entry fails leaves none of the batch's entries behind")]
        public void WriteBatch_SqlServer_FailureWritesNothing() => RunWriteBatchFailureWritesNothing(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a WriteBatch whose last entry fails leaves none of the batch's entries behind")]
        public void WriteBatch_PostgreSQL_FailureWritesNothing() => RunWriteBatchFailureWritesNothing(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a WriteBatch whose last entry fails leaves none of the batch's entries behind")]
        public void WriteBatch_Sqlite_FailureWritesNothing() => RunWriteBatchFailureWritesNothing(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a WriteBatch whose last entry fails leaves none of the batch's entries behind")]
        public void WriteBatch_MySql_FailureWritesNothing() => RunWriteBatchFailureWritesNothing(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a WriteBatch whose last entry fails leaves none of the batch's entries behind")]
        public void WriteBatch_Oracle_FailureWritesNothing() => RunWriteBatchFailureWritesNothing(DatabaseType.Oracle);

        /// <summary>
        /// Sends the repository's conventional <c>log</c> database id to the test log database of one dialect.
        /// </summary>
        private sealed class RedirectingDbAccessFactory(IDbAccessFactory inner, string databaseId) : IDbAccessFactory
        {
            public DbAccess Create(string requestedId) => inner.Create(databaseId);
        }

        // PostgreSQL refuses a NUL character in a text column, which an unauthenticated caller can put in a user id.
        private void RunBatchWithNulValue(DatabaseType databaseType)
        {
            var first = Guid.NewGuid();
            var withNul = Guid.NewGuid();
            var nul = new LoginAuditEntry { SysRowId = withNul, UserId = "a\0b", Event = LoginEvent.LoginFailed };

            CreateWriteRepository(databaseType).WriteBatch([NewLoginEntry(first), nul]);

            Assert.Equal(1L, CountLoginRows(databaseType, first));
            Assert.Equal(1L, CountLoginRows(databaseType, withNul));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a WriteBatch with a NUL character in a value persists every entry of the batch")]
        public void WriteBatch_SqlServer_NulValueKeepsTheBatch() => RunBatchWithNulValue(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a WriteBatch with a NUL character in a value persists every entry of the batch")]
        public void WriteBatch_PostgreSql_NulValueKeepsTheBatch() => RunBatchWithNulValue(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a WriteBatch with a NUL character in a value persists every entry of the batch")]
        public void WriteBatch_MySql_NulValueKeepsTheBatch() => RunBatchWithNulValue(DatabaseType.MySQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a WriteBatch with a NUL character in a value persists every entry of the batch")]
        public void WriteBatch_Sqlite_NulValueKeepsTheBatch() => RunBatchWithNulValue(DatabaseType.SQLite);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a WriteBatch with a NUL character in a value persists every entry of the batch")]
        public void WriteBatch_Oracle_NulValueKeepsTheBatch() => RunBatchWithNulValue(DatabaseType.Oracle);

        // PostgreSQL cannot encode a lone UTF-16 surrogate as UTF-8, and a caller can put one in a user id.
        private void RunBatchWithLoneSurrogate(DatabaseType databaseType)
        {
            var first = Guid.NewGuid();
            var withSurrogate = Guid.NewGuid();
            var entry = new LoginAuditEntry { SysRowId = withSurrogate, UserId = "a\uD83Db\uDE00", Event = LoginEvent.LoginFailed };

            CreateWriteRepository(databaseType).WriteBatch([NewLoginEntry(first), entry]);

            Assert.Equal(1L, CountLoginRows(databaseType, first));
            Assert.Equal(1L, CountLoginRows(databaseType, withSurrogate));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a WriteBatch with a lone surrogate in a value persists every entry of the batch")]
        public void WriteBatch_SqlServer_LoneSurrogateKeepsTheBatch() => RunBatchWithLoneSurrogate(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a WriteBatch with a lone surrogate in a value persists every entry of the batch")]
        public void WriteBatch_PostgreSql_LoneSurrogateKeepsTheBatch() => RunBatchWithLoneSurrogate(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a WriteBatch with a lone surrogate in a value persists every entry of the batch")]
        public void WriteBatch_MySql_LoneSurrogateKeepsTheBatch() => RunBatchWithLoneSurrogate(DatabaseType.MySQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a WriteBatch with a lone surrogate in a value persists every entry of the batch")]
        public void WriteBatch_Sqlite_LoneSurrogateKeepsTheBatch() => RunBatchWithLoneSurrogate(DatabaseType.SQLite);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a WriteBatch with a lone surrogate in a value persists every entry of the batch")]
        public void WriteBatch_Oracle_LoneSurrogateKeepsTheBatch() => RunBatchWithLoneSurrogate(DatabaseType.Oracle);

        // The batch the sink writes, with one entry whose user id an unauthenticated caller made too long.
        private void RunBatchWithOverlongValue(DatabaseType databaseType)
        {
            var first = Guid.NewGuid();
            var overlong = Guid.NewGuid();
            var last = Guid.NewGuid();
            var tooLong = new LoginAuditEntry { SysRowId = overlong, UserId = new string('x', 500), Event = LoginEvent.LoginFailed };

            CreateWriteRepository(databaseType).WriteBatch([NewLoginEntry(first), tooLong, NewLoginEntry(last)]);

            Assert.Equal(1L, CountLoginRows(databaseType, first));
            Assert.Equal(1L, CountLoginRows(databaseType, overlong));
            Assert.Equal(1L, CountLoginRows(databaseType, last));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a WriteBatch with one over-long value persists every entry of the batch")]
        public void WriteBatch_SqlServer_OverlongValueKeepsTheBatch() => RunBatchWithOverlongValue(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a WriteBatch with one over-long value persists every entry of the batch")]
        public void WriteBatch_PostgreSql_OverlongValueKeepsTheBatch() => RunBatchWithOverlongValue(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a WriteBatch with one over-long value persists every entry of the batch")]
        public void WriteBatch_MySql_OverlongValueKeepsTheBatch() => RunBatchWithOverlongValue(DatabaseType.MySQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a WriteBatch with one over-long value persists every entry of the batch")]
        public void WriteBatch_Sqlite_OverlongValueKeepsTheBatch() => RunBatchWithOverlongValue(DatabaseType.SQLite);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a WriteBatch with one over-long value persists every entry of the batch")]
        public void WriteBatch_Oracle_OverlongValueKeepsTheBatch() => RunBatchWithOverlongValue(DatabaseType.Oracle);

        // A sign-in's user id comes from an unauthenticated caller and st_log_login.user_id is 50 characters wide.
        private void RunOverlongValue(DatabaseType databaseType)
        {
            var databaseId = TestDbConventions.GetDatabaseId(databaseType, "log");
            var dbAccess = _fx.GetRequiredService<IDbAccessFactory>().Create(databaseId);
            var schema = _fx.GetRequiredService<IDefineAccess>().GetTableSchema("log", "st_log_login");
            var width = schema.Fields!["user_id"].Length;
            var rowId = Guid.NewGuid();
            var entry = new LoginAuditEntry
            {
                SysRowId = rowId,
                UserId = new string('x', width * 4),
                Event = LoginEvent.LoginFailed,
            };

            if (databaseType != DatabaseType.SQLite)
            {
                // Control: without the column widths the database refuses the row, which once took its whole batch.
                Assert.ThrowsAny<DbException>(() => dbAccess.Execute(AuditLogWriteRepository.BuildInsert(entry)));
            }

            dbAccess.Execute(AuditLogWriteRepository.BuildInsert(entry, schema));

            var stored = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT user_id FROM st_log_login WHERE sys_rowid={0}", rowId)).Scalar;
            Assert.Equal(new string('x', width), Convert.ToString(stored, CultureInfo.InvariantCulture));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: an audit value longer than its column is cut to the column's width and written")]
        public void OverlongValue_SqlServer_IsCutToWidth() => RunOverlongValue(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: an audit value longer than its column is cut to the column's width and written")]
        public void OverlongValue_PostgreSql_IsCutToWidth() => RunOverlongValue(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: an audit value longer than its column is cut to the column's width and written")]
        public void OverlongValue_MySql_IsCutToWidth() => RunOverlongValue(DatabaseType.MySQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: an audit value longer than its column is cut to the column's width and written")]
        public void OverlongValue_Sqlite_IsCutToWidth() => RunOverlongValue(DatabaseType.SQLite);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: an audit value longer than its column is cut to the column's width and written")]
        public void OverlongValue_Oracle_IsCutToWidth() => RunOverlongValue(DatabaseType.Oracle);
    }
}
