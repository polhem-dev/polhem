using System.ComponentModel;
using System.Data.Common;
using Polhem.Db.Providers.Sqlite;

namespace Polhem.Db.UnitTests.Manager
{
    /// <summary>
    /// Pins down the <see cref="DbDataAdapter.UpdateBatchSize"/> support of each provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>DbAccess.ApplySpec</c> tries to enable batched writes and falls back to row by row when the provider does
    /// not support them. It uses <b>capability detection</b> rather than a hard-coded list of providers: a list
    /// drifts, and it cannot cover whatever factory a host registered for a <c>DatabaseType</c> itself.
    /// </para>
    /// <para>
    /// But the implementation's comment says "measured today: SQL Server / MySQL / Oracle accept it, Npgsql and the
    /// framework's own SQLite adapter throw". With nothing guarding it, that sentence becomes the next outdated claim,
    /// so this test pins it down. <b>If a provider changes its behavior one day, this goes red</b>; the thing to do
    /// then is to measure again and update the comment, not to change the test.
    /// </para>
    /// <para>
    /// The factories are created directly on purpose instead of through <c>DbProviderRegistry</c>: this checks the
    /// capability of the packages themselves, unrelated to what a deployment registered, so no container is needed.
    /// </para>
    /// </remarks>
    public class ProviderBatchingSupportTests
    {
        public static TheoryData<string, bool> Providers() => new()
        {
            { "SQLServer", true },
            { "MySQL", true },
            { "Oracle", true },
            { "PostgreSQL", false },
            { "SQLite", false },
        };

        private static DbProviderFactory CreateFactory(string name) => name switch
        {
            "SQLServer" => Microsoft.Data.SqlClient.SqlClientFactory.Instance,
            "MySQL" => MySqlConnector.MySqlConnectorFactory.Instance,
            "Oracle" => Oracle.ManagedDataAccess.Client.OracleClientFactory.Instance,
            "PostgreSQL" => Npgsql.NpgsqlFactory.Instance,
            "SQLite" => new SqliteProviderFactory(Microsoft.Data.Sqlite.SqliteFactory.Instance),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown provider."),
        };

        [Theory]
        [MemberData(nameof(Providers))]
        [DisplayName("The UpdateBatchSize support of each provider's adapter matches the measured baseline")]
        public void Adapter_UpdateBatchSizeSupport_MatchesMeasuredBaseline(string provider, bool expectedSupport)
        {
            using var adapter = CreateFactory(provider).CreateDataAdapter();
            Assert.NotNull(adapter);

            var exception = Record.Exception(() => adapter!.UpdateBatchSize = 100);

            if (expectedSupport)
            {
                Assert.Null(exception);
                Assert.Equal(100, adapter!.UpdateBatchSize);
            }
            else
            {
                // The base setter throws `NotSupportedException`, which is exactly the signal `ApplySpec` detects.
                Assert.IsType<NotSupportedException>(exception);
                Assert.Equal(1, adapter!.UpdateBatchSize);
            }
        }

        [Fact]
        [DisplayName("The provider list is not empty (guards against a vacuous pass)")]
        public void Providers_AreNotEmpty()
        {
            Assert.NotEmpty(Providers());
        }
    }
}
