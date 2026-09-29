using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Providers.SqlServer;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;
using Polhem.Db.Manager;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Additional edge paths of <c>NormalizeDataTypeName</c> in <see cref="SqlTableSchemaProvider"/>: null/empty input.
    /// </summary>
    public class SqlTableSchemaProviderNullTests
    {
        [Fact]
        [DisplayName("SQL Server GetFieldDbType returns Unknown for null input")]
        public void GetFieldDbType_NullDataType_ReturnsUnknown()
        {
            Assert.Equal(FieldDbType.Unknown, SqlTableSchemaProvider.GetFieldDbType(null!, 0, 0, 0));
        }

        [Fact]
        [DisplayName("SQL Server GetFieldDbType returns Unknown for an empty string")]
        public void GetFieldDbType_EmptyDataType_ReturnsUnknown()
        {
            Assert.Equal(FieldDbType.Unknown, SqlTableSchemaProvider.GetFieldDbType(string.Empty, 0, 0, 0));
        }
    }

    /// <summary>
    /// Integration tests for <see cref="SqlTableSchemaProvider"/>: the Decimal branch of <c>ParseDbField</c>
    /// (assigning Precision/Scale) and the early return of <c>ParsePrimaryKey</c> when there is no primary key.
    /// Requires a SQL Server connection; skipped automatically when the environment variable is not set.
    /// </summary>
    public class SqlTableSchemaProviderDecimalAndNoPkTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public SqlTableSchemaProviderDecimalAndNoPkTests(SharedDbFixture fx) { _fx = fx; }


        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server SchemaProvider sets Precision and Scale when reading back a DECIMAL(15,3) field (ParseDbField Decimal branch)")]
        public void GetTableSchema_DecimalField_ReturnsPrecisionAndScale()
        {
            const string tableName = "tb_ex_decimal";
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            DropSqlTable(dbAccess, tableName);

            try
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "CREATE TABLE [tb_ex_decimal] " +
                    "([id] UNIQUEIDENTIFIER NOT NULL, [amount] DECIMAL(15,3) NOT NULL, " +
                    "CONSTRAINT [PK_TB_EX_DECIMAL] PRIMARY KEY ([id]))"));

                var provider = new SqlTableSchemaProvider("common_sqlserver", _fx.GetRequiredService<IDbConnectionManager>());
                var schema = provider.GetTableSchema(tableName);

                Assert.NotNull(schema);
                Assert.True(schema!.Fields!.Contains("amount"));
                var field = schema.Fields["amount"];
                Assert.Equal(FieldDbType.Decimal, field.DbType);
                Assert.Equal(15, field.Precision);
                Assert.Equal(3, field.Scale);
            }
            finally
            {
                DropSqlTable(dbAccess, tableName);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server SchemaProvider reading a table with only a unique index (no PK) returns early from ParsePrimaryKey and still parses the unique index")]
        public void GetTableSchema_TableWithUniqueIndexNoPk_ParsePrimaryKeyReturnsEarlyAndIndexPresent()
        {
            const string tableName = "tb_ex_nopk";
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            DropSqlTable(dbAccess, tableName);

            try
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "CREATE TABLE [tb_ex_nopk] ([code] NVARCHAR(20) NOT NULL)"));
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "CREATE UNIQUE INDEX [UX_TB_EX_NOPK] ON [tb_ex_nopk] ([code])"));

                var provider = new SqlTableSchemaProvider("common_sqlserver", _fx.GetRequiredService<IDbConnectionManager>());
                var schema = provider.GetTableSchema(tableName);

                Assert.NotNull(schema);
                Assert.True(schema!.Fields!.Contains("code"));
                Assert.NotEmpty(schema.Indexes!);
                Assert.DoesNotContain(schema.Indexes!, idx => idx.PrimaryKey);
            }
            finally
            {
                DropSqlTable(dbAccess, tableName);
            }
        }

        private static void DropSqlTable(DbAccess dbAccess, string tableName)
        {
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                "IF OBJECT_ID(N'[" + tableName + "]', N'U') IS NOT NULL DROP TABLE [" + tableName + "]"));
        }
    }
}
