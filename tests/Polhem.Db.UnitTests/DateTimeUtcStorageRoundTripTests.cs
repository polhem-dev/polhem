using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Verifies ADR-032 D1: every provider stores UTC values as they are in a **column without a time zone**, and
    /// reads them back identical to the tick.
    /// </summary>
    /// <remarks>
    /// This test guards the premise that the database does not take part in time zones. If a provider or its ADO.NET
    /// driver converts implicitly by the server or client time zone on write or read, the conversion chain gains one
    /// extra offset, and the only symptom is times that are a few hours off, with no error message at all.
    /// PostgreSQL's <c>timestamptz</c> was excluded by D1 precisely because it converts like that; this confirms that
    /// the chosen <c>timestamp</c> does not.
    ///
    /// The test value is deliberately a moment that falls on a different date in UTC than in common development and
    /// CI time zones, so any time zone conversion changes the date and not only the time of day; the latter cannot
    /// be detected where the offset happens to be 0.
    /// </remarks>
    public class DateTimeUtcStorageRoundTripTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public DateTimeUtcStorageRoundTripTests(SharedDbFixture fx) { _fx = fx; }

        /// <summary>
        /// 23:30 UTC: 07:30 the next day in Taipei and 18:30 the same day in New York, so any conversion shifts the
        /// date or the hour.
        /// </summary>
        private static readonly DateTime s_utcValue =
            new DateTime(2026, 3, 15, 23, 30, 45, DateTimeKind.Unspecified);

        private const string TableName = "dt_utc_storage_test";

        private DateTime WriteThenRead(string databaseId, string createSql, string dropSql)
        {
            var dbAccess = _fx.NewDbAccess(databaseId);
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, dropSql));
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, createSql));
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                $"INSERT INTO {TableName} (dt) VALUES ({{0}})", s_utcValue));
            var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable,
                $"SELECT dt FROM {TableName}"));
            return result.Table!.Rows[0].GetFieldValue<DateTime>("dt");
        }

        private void RunRoundTrip(string databaseId, string createSql, string dropSql)
        {
            try
            {
                var readBack = WriteThenRead(databaseId, createSql, dropSql);

                Assert.Equal(s_utcValue.Ticks, readBack.Ticks);
            }
            finally
            {
                _fx.NewDbAccess(databaseId).Execute(new DbCommandSpec(DbCommandKind.NonQuery, dropSql));
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server datetime2 stores a UTC value as is and reads it back identical to the tick")]
        public void RoundTrip_SqlServer_StoresUtcVerbatim()
        {
            RunRoundTrip("common_sqlserver",
                $"CREATE TABLE [{TableName}] ([dt] [datetime2](7) NOT NULL);",
                $"IF OBJECT_ID(N'{TableName}', N'U') IS NOT NULL DROP TABLE [{TableName}];");
        }

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL timestamp (without time zone) stores a UTC value as is with no implicit conversion")]
        public void RoundTrip_PostgreSQL_StoresUtcVerbatim()
        {
            RunRoundTrip("common_postgresql",
                $"CREATE TABLE {TableName} (dt timestamp NOT NULL);",
                $"DROP TABLE IF EXISTS {TableName};");
        }

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL DATETIME(6) stores a UTC value as is and reads it back identical to the tick")]
        public void RoundTrip_MySQL_StoresUtcVerbatim()
        {
            RunRoundTrip("common_mysql",
                $"CREATE TABLE {TableName} (dt DATETIME(6) NOT NULL);",
                $"DROP TABLE IF EXISTS {TableName};");
        }

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle TIMESTAMP stores a UTC value as is and reads it back identical to the tick")]
        public void RoundTrip_Oracle_StoresUtcVerbatim()
        {
            RunRoundTrip("common_oracle",
                $"CREATE TABLE {TableName} (dt TIMESTAMP(7) NOT NULL)",
                $"BEGIN EXECUTE IMMEDIATE 'DROP TABLE {TableName}'; EXCEPTION WHEN OTHERS THEN NULL; END;");
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite TEXT column stores a UTC value as is and reads it back identical to the tick")]
        public void RoundTrip_SQLite_StoresUtcVerbatim()
        {
            RunRoundTrip("common_sqlite",
                $"CREATE TABLE {TableName} (dt TEXT NOT NULL);",
                $"DROP TABLE IF EXISTS {TableName};");
        }
    }
}
