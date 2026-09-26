using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.SqlServer;

namespace Polhem.Db.UnitTests
{
    public class SqlTableSchemaProviderStaticTests
    {
        #region GetFieldDbType

        [Theory]
        [InlineData("NCHAR", 0, 0, 0, FieldDbType.String)]
        [InlineData("NVARCHAR", 0, 0, 50, FieldDbType.String)]
        [InlineData("NVARCHAR", 0, 0, -1, FieldDbType.Text)]
        [InlineData("BIT", 0, 0, 0, FieldDbType.Boolean)]
        [InlineData("SMALLINT", 0, 0, 0, FieldDbType.Short)]
        [InlineData("INT", 0, 0, 0, FieldDbType.Integer)]
        [InlineData("BIGINT", 0, 0, 0, FieldDbType.Long)]
        [InlineData("FLOAT", 0, 0, 0, FieldDbType.Decimal)]
        [InlineData("DECIMAL", 19, 4, 0, FieldDbType.Currency)]
        [InlineData("DECIMAL", 12, 3, 0, FieldDbType.Decimal)]
        [InlineData("DATE", 0, 0, 0, FieldDbType.Date)]
        [InlineData("DATETIME", 0, 3, 0, FieldDbType.DateTime)]
        [InlineData("DATETIME2", 0, 7, 0, FieldDbType.DateTime)]
        [InlineData("UNIQUEIDENTIFIER", 0, 0, 0, FieldDbType.Guid)]
        [InlineData("VARBINARY", 0, 0, 0, FieldDbType.Binary)]
        [InlineData("XML", 0, 0, 0, FieldDbType.Unknown)]
        [DisplayName("GetFieldDbType maps each SQL Server type")]
        public void GetFieldDbType_VariousSqlTypes_MapsCorrectly(
            string dataType, int precision, int scale, int length, FieldDbType expected)
        {
            var result = SqlTableSchemaProvider.GetFieldDbType(dataType, precision, scale, length);

            Assert.Equal(expected, result);
        }

        [Fact]
        [DisplayName("GetFieldDbType ignores the case of the input string")]
        public void GetFieldDbType_CaseInsensitive()
        {
            Assert.Equal(FieldDbType.Integer, SqlTableSchemaProvider.GetFieldDbType("int", 0, 0, 0));
            Assert.Equal(FieldDbType.Boolean, SqlTableSchemaProvider.GetFieldDbType("Bit", 0, 0, 0));
        }

        #endregion

        #region ParseDBDefaultValue

        [Theory]
        [InlineData("VARCHAR", "('hello')", "", "hello")]
        [InlineData("CHAR", "('A')", "", "A")]
        [InlineData("NVARCHAR", "(N'world')", "", "world")]
        [InlineData("NCHAR", "(N'X')", "", "X")]
        [InlineData("INT", "((42))", "", "42")]
        [InlineData("BIT", "((1))", "", "1")]
        [InlineData("DATE", "(getutcdate())", "", "getutcdate()")]
        [InlineData("DATETIME", "(getutcdate())", "", "getutcdate()")]
        [InlineData("DATETIME2", "(getutcdate())", "", "getutcdate()")]
        [InlineData("UNIQUEIDENTIFIER", "(newid())", "", "newid()")]
        [DisplayName("ParseDBDefaultValue strips the outer parentheses or the prefix by type")]
        public void ParseDBDefaultValue_StripsWrappers(
            string dataType, string defaultValue, string originalDefault, string expected)
        {
            var result = SqlTableSchemaProvider.ParseDBDefaultValue(dataType, defaultValue, originalDefault);

            Assert.Equal(expected, result);
        }

        [Fact]
        [DisplayName("ParseDBDefaultValue returns an empty string when the value equals the built-in default")]
        public void ParseDBDefaultValue_MatchesBuiltinDefault_ReturnsEmpty()
        {
            // An INT default is usually "0": ((0)) becomes "0" once unwrapped, equal to the built-in default.
            var result = SqlTableSchemaProvider.ParseDBDefaultValue("INT", "((0))", "0");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("ParseDBDefaultValue returns an empty string for an unsupported type")]
        public void ParseDBDefaultValue_UnknownType_ReturnsEmpty()
        {
            var result = SqlTableSchemaProvider.ParseDBDefaultValue("XML", "<root/>", "");

            Assert.Equal(string.Empty, result);
        }

        #endregion
    }
}
