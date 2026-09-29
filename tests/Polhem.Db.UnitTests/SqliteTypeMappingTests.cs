using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Providers.Sqlite;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Pure syntax tests covering how <see cref="SqliteTypeMapping"/> maps each <see cref="FieldDbType"/> to a type
    /// string. AutoIncrement → "INTEGER" is a SQLite special rule; the CREATE TABLE side then inlines
    /// PRIMARY KEY AUTOINCREMENT.
    /// </summary>
    public class SqliteTypeMappingTests
    {
        [Fact]
        [DisplayName("SQLite GetSqliteType returns VARCHAR(N) for String")]
        public void GetSqliteType_String_UsesVarchar()
        {
            var field = new DbField("v", "V", FieldDbType.String) { Length = 50 };
            Assert.Equal("VARCHAR(50)", SqliteTypeMapping.GetSqliteType(field));
        }

        [Theory]
        [InlineData(FieldDbType.Text, "TEXT")]
        [InlineData(FieldDbType.Boolean, "BOOLEAN")]
        [InlineData(FieldDbType.AutoIncrement, "INTEGER")]
        [InlineData(FieldDbType.Short, "SMALLINT")]
        [InlineData(FieldDbType.Integer, "INTEGER")]
        [InlineData(FieldDbType.Long, "BIGINT")]
        [InlineData(FieldDbType.Currency, "NUMERIC(19,4)")]
        [InlineData(FieldDbType.Date, "DATE")]
        [InlineData(FieldDbType.DateTime, "DATETIME")]
        [InlineData(FieldDbType.Guid, "UUID")]
        [InlineData(FieldDbType.Binary, "BLOB")]
        [DisplayName("SQLite GetSqliteType maps each type to its SQLite type string")]
        public void GetSqliteType_VariousTypes_MapsCorrectly(FieldDbType dbType, string expected)
        {
            var field = new DbField("v", "V", dbType);
            Assert.Equal(expected, SqliteTypeMapping.GetSqliteType(field));
        }

        [Fact]
        [DisplayName("SQLite GetSqliteType uses the default precision 18,0 for Decimal")]
        public void GetSqliteType_DecimalDefaults_Returns18_0()
        {
            var field = new DbField("v", "V", FieldDbType.Decimal) { Precision = 0, Scale = 0 };
            Assert.Equal("NUMERIC(18,0)", SqliteTypeMapping.GetSqliteType(field));
        }

        [Fact]
        [DisplayName("SQLite GetSqliteType reflects a custom Decimal precision and scale in the type string")]
        public void GetSqliteType_DecimalCustom_AppliesPrecisionScale()
        {
            var field = new DbField("v", "V", FieldDbType.Decimal) { Precision = 12, Scale = 3 };
            Assert.Equal("NUMERIC(12,3)", SqliteTypeMapping.GetSqliteType(field));
        }

        [Fact]
        [DisplayName("SQLite GetSqliteType throws InvalidOperationException for Unknown")]
        public void GetSqliteType_Unknown_Throws()
        {
            var field = new DbField("v", "V", FieldDbType.Unknown);
            Assert.Throws<InvalidOperationException>(() => SqliteTypeMapping.GetSqliteType(field));
        }
    }
}
