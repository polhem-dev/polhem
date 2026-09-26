using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.Oracle;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Static-only tests for <see cref="OracleTableSchemaProvider"/>: validates the type-
    /// mapping and default-value parsing helpers that don't require a live Oracle connection.
    /// The full integration coverage (live <c>USER_*</c> dictionary reads, GetTableSchema
    /// round-trip) is gated by <c>POLHEM_TEST_CONNSTR_ORACLE</c> and runs in Phase D.
    /// </summary>
    public class OracleTableSchemaProviderStaticTests
    {
        #region GetFieldDbType

        [Theory]
        [InlineData("VARCHAR2", 0, 0, 50, FieldDbType.String)]
        [InlineData("varchar2", 0, 0, 100, FieldDbType.String)]
        [InlineData("NVARCHAR2", 0, 0, 50, FieldDbType.String)]
        [InlineData("CHAR", 0, 0, 10, FieldDbType.String)]
        [InlineData("CLOB", 0, 0, 0, FieldDbType.Text)]
        [InlineData("NCLOB", 0, 0, 0, FieldDbType.Text)]
        [InlineData("LONG", 0, 0, 0, FieldDbType.Text)]
        [InlineData("NUMBER", 1, 0, 0, FieldDbType.Boolean)]
        [InlineData("NUMBER", 5, 0, 0, FieldDbType.Short)]
        [InlineData("NUMBER", 10, 0, 0, FieldDbType.Integer)]
        [InlineData("NUMBER", 19, 0, 0, FieldDbType.Long)]
        [InlineData("NUMBER", 19, 4, 0, FieldDbType.Currency)]
        [InlineData("NUMBER", 12, 3, 0, FieldDbType.Decimal)]
        [InlineData("NUMBER", 18, 2, 0, FieldDbType.Decimal)]
        [InlineData("FLOAT", 0, 0, 0, FieldDbType.Decimal)]
        [InlineData("BINARY_FLOAT", 0, 0, 0, FieldDbType.Decimal)]
        [InlineData("BINARY_DOUBLE", 0, 0, 0, FieldDbType.Decimal)]
        [InlineData("DATE", 0, 0, 0, FieldDbType.Date)]
        [InlineData("TIMESTAMP(6)", 0, 0, 0, FieldDbType.DateTime)]
        [InlineData("TIMESTAMP", 0, 0, 0, FieldDbType.DateTime)]
        [InlineData("RAW", 0, 0, 16, FieldDbType.Guid)]
        [InlineData("RAW", 0, 0, 100, FieldDbType.Binary)]
        [InlineData("BLOB", 0, 0, 0, FieldDbType.Binary)]
        [InlineData("LONG RAW", 0, 0, 0, FieldDbType.Binary)]
        [InlineData("XMLTYPE", 0, 0, 0, FieldDbType.Unknown)]
        [DisplayName("Oracle GetFieldDbType maps each Oracle type")]
        public void GetFieldDbType_VariousOracleTypes_MapsCorrectly(
            string dataType, int precision, int scale, int length, FieldDbType expected)
        {
            var result = OracleTableSchemaProvider.GetFieldDbType(dataType, precision, scale, length);

            Assert.Equal(expected, result);
        }

        [Fact]
        [DisplayName("Oracle GetFieldDbType ignores the case of the input string")]
        public void GetFieldDbType_CaseInsensitive()
        {
            Assert.Equal(FieldDbType.String, OracleTableSchemaProvider.GetFieldDbType("varchar2", 0, 0, 50));
            Assert.Equal(FieldDbType.Integer, OracleTableSchemaProvider.GetFieldDbType("Number", 10, 0, 0));
            Assert.Equal(FieldDbType.Guid, OracleTableSchemaProvider.GetFieldDbType("Raw", 0, 0, 16));
        }

        [Fact]
        [DisplayName("Oracle GetFieldDbType recognizes TIMESTAMP with a parenthesized precision (such as TIMESTAMP(6) WITH TIME ZONE) as DateTime")]
        public void GetFieldDbType_TimestampWithQualifier_StillMapsToDateTime()
        {
            Assert.Equal(FieldDbType.DateTime, OracleTableSchemaProvider.GetFieldDbType("TIMESTAMP(6)", 0, 0, 0));
        }

        #endregion

        #region ParseDBDefaultValue

        [Theory]
        [InlineData("VARCHAR2", "'hello'", "", "hello")]
        [InlineData("VARCHAR2", "'world' ", "", "world")] // With a trailing space (an Oracle LONG convention).
        [InlineData("CLOB", "'foo'", "", "foo")]
        [InlineData("NUMBER", "0", "", "0")]
        [InlineData("NUMBER", "42", "", "42")]
        [InlineData("DATE", "SYS_EXTRACT_UTC(SYSTIMESTAMP)", "", "SYS_EXTRACT_UTC(SYSTIMESTAMP)")]
        [InlineData("TIMESTAMP(6)", "SYS_EXTRACT_UTC(SYSTIMESTAMP)", "", "SYS_EXTRACT_UTC(SYSTIMESTAMP)")]
        [InlineData("RAW", "SYS_GUID()", "", "SYS_GUID()")]
        [DisplayName("Oracle ParseDBDefaultValue strips the string quotes by type and trims whitespace")]
        public void ParseDBDefaultValue_StripsQuotesAndTrims(
            string dataType, string defaultValue, string originalDefault, string expected)
        {
            var result = OracleTableSchemaProvider.ParseDBDefaultValue(dataType, defaultValue, originalDefault);

            Assert.Equal(expected, result);
        }

        [Fact]
        [DisplayName("Oracle ParseDBDefaultValue returns an empty string when the value equals the built-in default")]
        public void ParseDBDefaultValue_MatchesBuiltinDefault_ReturnsEmpty()
        {
            // A NUMBER default is usually "0"; after trimming it equals
            // the built-in one, so the result is an empty string.
            var result = OracleTableSchemaProvider.ParseDBDefaultValue("NUMBER", "0", "0");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("Oracle ParseDBDefaultValue treats a built-in default that differs only in case as equal (function names such as SYS_GUID)")]
        public void ParseDBDefaultValue_MatchesBuiltinDefaultCaseInsensitive_ReturnsEmpty()
        {
            // The Oracle data dictionary may return function names in lower case.
            var result = OracleTableSchemaProvider.ParseDBDefaultValue("RAW", "sys_guid()", "SYS_GUID()");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("Oracle ParseDBDefaultValue restores escaped doubled quotes of a string type ('O''Brien' → O'Brien)")]
        public void ParseDBDefaultValue_EscapedQuoteInString_Unescaped()
        {
            var result = OracleTableSchemaProvider.ParseDBDefaultValue("VARCHAR2", "'O''Brien'", "");

            Assert.Equal("O'Brien", result);
        }

        [Fact]
        [DisplayName("Oracle ParseDBDefaultValue returns an empty string for empty input")]
        public void ParseDBDefaultValue_EmptyInput_ReturnsEmpty()
        {
            var result = OracleTableSchemaProvider.ParseDBDefaultValue("NUMBER", string.Empty, "0");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("Oracle ParseDBDefaultValue restores the empty string literal '' to an empty string")]
        public void ParseDBDefaultValue_EmptyStringLiteral_ParsesToEmpty()
        {
            // The built-in default of a string column is `string.Empty`. The database stores '', which also parses to
            // "", equal to `originalDefaultValue` (`string.Empty`), so the result is `string.Empty`.
            var result = OracleTableSchemaProvider.ParseDBDefaultValue("VARCHAR2", "''", "");

            Assert.Equal(string.Empty, result);
        }

        #endregion
    }
}
