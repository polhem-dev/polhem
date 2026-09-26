using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.Oracle;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Additional coverage for the static methods of <see cref="OracleTableSchemaProvider"/>: the NCHAR, NVARCHAR2,
    /// CHAR and NCLOB type paths that <see cref="OracleTableSchemaProviderStaticTests"/> does not reach yet.
    /// </summary>
    public class OracleTableSchemaProviderAdditionalTests
    {
        #region GetFieldDbType additional types

        [Fact]
        [DisplayName("Oracle GetFieldDbType maps NCHAR to String")]
        public void GetFieldDbType_Nchar_ReturnsString()
        {
            Assert.Equal(FieldDbType.String, OracleTableSchemaProvider.GetFieldDbType("NCHAR", 0, 0, 10));
            Assert.Equal(FieldDbType.String, OracleTableSchemaProvider.GetFieldDbType("nchar", 0, 0, 5));
        }

        [Theory]
        [InlineData("NVARCHAR2", 0, 0, 100, FieldDbType.String)]
        [InlineData("nvarchar2", 0, 0, 50, FieldDbType.String)]
        [InlineData("NCHAR", 0, 0, 10, FieldDbType.String)]
        [InlineData("NCLOB", 0, 0, 0, FieldDbType.Text)]
        [DisplayName("Oracle GetFieldDbType maps the N-prefixed types")]
        public void GetFieldDbType_NPrefixTypes_MapsCorrectly(
            string dataType, int precision, int scale, int length, FieldDbType expected)
        {
            var result = OracleTableSchemaProvider.GetFieldDbType(dataType, precision, scale, length);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("NUMBER", 0, 1, 0, FieldDbType.Decimal)]
        [InlineData("NUMBER", 0, 0, 0, FieldDbType.Decimal)]
        [InlineData("NUMBER", 18, 2, 0, FieldDbType.Decimal)]
        [DisplayName("Oracle GetFieldDbType returns Decimal for NUMBER when the precision and scale match no known mapping")]
        public void GetFieldDbType_NumberUncoveredBranches_ReturnsDecimal(
            string dataType, int precision, int scale, int length, FieldDbType expected)
        {
            var result = OracleTableSchemaProvider.GetFieldDbType(dataType, precision, scale, length);

            Assert.Equal(expected, result);
        }

        #endregion

        #region ParseDBDefaultValue additional types

        [Theory]
        [InlineData("NVARCHAR2", "'hello'", "", "hello")]
        [InlineData("CHAR", "'A'", "", "A")]
        [InlineData("NCHAR", "'X'", "", "X")]
        [InlineData("NCLOB", "'foo'", "", "foo")]
        [DisplayName("Oracle ParseDBDefaultValue strips the string quotes for N-prefixed types")]
        public void ParseDBDefaultValue_NPrefixTypes_StripsStringLiteral(
            string dataType, string defaultValue, string originalDefault, string expected)
        {
            var result = OracleTableSchemaProvider.ParseDBDefaultValue(dataType, defaultValue, originalDefault);

            Assert.Equal(expected, result);
        }

        [Fact]
        [DisplayName("Oracle ParseDBDefaultValue returns an empty string when an NVARCHAR2 default equals the built-in default")]
        public void ParseDBDefaultValue_Nvarchar2MatchesBuiltin_ReturnsEmpty()
        {
            var result = OracleTableSchemaProvider.ParseDBDefaultValue("NVARCHAR2", "''", "");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("Oracle ParseDBDefaultValue returns an unquoted string-type default as is")]
        public void ParseDBDefaultValue_StringTypeNotQuoted_ReturnsAsIs()
        {
            // When Oracle's DATA_DEFAULT is not quoted (such as a referencing
            // default), `StripStringLiteral` returns it unchanged.
            var result = OracleTableSchemaProvider.ParseDBDefaultValue("NVARCHAR2", "(NOW() AT TIME ZONE 'UTC')", "");

            Assert.Equal("(NOW() AT TIME ZONE 'UTC')", result);
        }

        [Fact]
        [DisplayName("Oracle ParseDBDefaultValue returns the stripped value when an NCLOB default differs from the built-in default")]
        public void ParseDBDefaultValue_NclobCustomDefault_ReturnsStrippedValue()
        {
            var result = OracleTableSchemaProvider.ParseDBDefaultValue("NCLOB", "'my default'", "");

            Assert.Equal("my default", result);
        }

        #endregion
    }
}
