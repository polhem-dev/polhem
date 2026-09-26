using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.Oracle;
using Polhem.Tests.Shared;
using Polhem.Definition.Database;
using Polhem.Db.Manager;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Additional edge paths of <c>NormalizeDataTypeName</c> in <see cref="OracleTableSchemaProvider"/>: null/empty
    /// input, trailing text after the parentheses, and an opening parenthesis without a closing one.
    /// </summary>
    public class OracleTableSchemaProviderNullTests
    {
        [Fact]
        [DisplayName("Oracle GetFieldDbType returns Unknown for null input")]
        public void GetFieldDbType_NullDataType_ReturnsUnknown()
        {
            Assert.Equal(FieldDbType.Unknown, OracleTableSchemaProvider.GetFieldDbType(null!, 0, 0, 0));
        }

        [Fact]
        [DisplayName("Oracle GetFieldDbType returns Unknown for an empty string")]
        public void GetFieldDbType_EmptyDataType_ReturnsUnknown()
        {
            Assert.Equal(FieldDbType.Unknown, OracleTableSchemaProvider.GetFieldDbType(string.Empty, 0, 0, 0));
        }

        [Fact]
        [DisplayName("Oracle GetFieldDbType returns Unknown for TIMESTAMP(6) WITH TIME ZONE (the after branch of NormalizeDataTypeName with text after the parentheses)")]
        public void GetFieldDbType_TimestampWithTimeZoneQualifier_ReturnsUnknown()
        {
            // NormalizeDataTypeName("TIMESTAMP(6) WITH TIME ZONE"):
            // lower="timestamp(6) with time zone", parenStart=9, parenEnd=11
            // before="timestamp", after=" with time zone" → result="timestamp with time zone"
            // → not in the switch → Unknown
            Assert.Equal(FieldDbType.Unknown,
                OracleTableSchemaProvider.GetFieldDbType("TIMESTAMP(6) WITH TIME ZONE", 0, 0, 0));
        }

        [Fact]
        [DisplayName("Oracle GetFieldDbType returns Unknown for an opening parenthesis without a closing one (the parenEnd<0 branch of NormalizeDataTypeName)")]
        public void GetFieldDbType_TypeWithOpenParenNoCloseParen_ReturnsUnknown()
        {
            // NormalizeDataTypeName("FLOAT("):
            // lower="float(", parenStart=5, parenEnd=lower.IndexOf(')', 5)=-1
            // → if (parenEnd < 0) return lower; → returns "float(" → switch default → Unknown
            Assert.Equal(FieldDbType.Unknown,
                OracleTableSchemaProvider.GetFieldDbType("FLOAT(", 0, 0, 0));
        }
    }

    /// <summary>
    /// Integration tests for <see cref="OracleTableSchemaProvider"/>: the Decimal branch of <c>ParseDbField</c>
    /// (assigning Precision/Scale) and the early return of <c>ParsePrimaryKey</c> when there is no primary key.
    /// Requires an Oracle connection; skipped automatically when the environment variable is not set.
    /// </summary>
    public class OracleTableSchemaProviderDecimalAndNoPkTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public OracleTableSchemaProviderDecimalAndNoPkTests(SharedDbFixture fx) { _fx = fx; }


        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle SchemaProvider sets Precision and Scale when reading back a NUMBER(15,3) field (ParseDbField Decimal branch)")]
        public void GetTableSchema_DecimalField_ReturnsPrecisionAndScale()
        {
            const string tableName = "tb_ex_decimal";
            string databaseId = TestDbConventions.GetDatabaseId(DatabaseType.Oracle);
            var dbAccess = _fx.NewDbAccess(databaseId);
            DropOracleTable(dbAccess, tableName);

            try
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "CREATE TABLE \"TB_EX_DECIMAL\" " +
                    "(\"id\" RAW(16) NOT NULL, \"amount\" NUMBER(15,3) NOT NULL, " +
                    "CONSTRAINT \"PK_TB_EX_DECIMAL\" PRIMARY KEY (\"id\"))"));

                var provider = new OracleTableSchemaProvider(databaseId, _fx.GetRequiredService<IDbConnectionManager>());
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
                DropOracleTable(dbAccess, tableName);
            }
        }

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle SchemaProvider reading a table with only a unique index (no PK) returns early from ParsePrimaryKey and still parses the unique index")]
        public void GetTableSchema_TableWithUniqueIndexNoPk_ParsePrimaryKeyReturnsEarlyAndIndexPresent()
        {
            const string tableName = "tb_ex_nopk";
            string databaseId = TestDbConventions.GetDatabaseId(DatabaseType.Oracle);
            var dbAccess = _fx.NewDbAccess(databaseId);
            DropOracleTable(dbAccess, tableName);

            try
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "CREATE TABLE \"TB_EX_NOPK\" (\"code\" VARCHAR2(20 CHAR) NOT NULL)"));
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "CREATE UNIQUE INDEX \"UX_TB_EX_NOPK\" ON \"TB_EX_NOPK\" (\"code\")"));

                var provider = new OracleTableSchemaProvider(databaseId, _fx.GetRequiredService<IDbConnectionManager>());
                var schema = provider.GetTableSchema(tableName);

                Assert.NotNull(schema);
                Assert.True(schema!.Fields!.Contains("code"));
                Assert.NotEmpty(schema.Indexes!);
                Assert.DoesNotContain(schema.Indexes!, idx => idx.PrimaryKey);
            }
            finally
            {
                DropOracleTable(dbAccess, tableName);
            }
        }

        private static void DropOracleTable(DbAccess dbAccess, string tableName)
        {
            string storageName = tableName.ToUpperInvariant();
            string ddl =
                "BEGIN " +
                "  EXECUTE IMMEDIATE 'DROP TABLE \"" + storageName + "\" CASCADE CONSTRAINTS'; " +
                "EXCEPTION WHEN OTHERS THEN IF SQLCODE != -942 THEN RAISE; END IF; " +
                "END;";
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, ddl));
        }
    }
}
