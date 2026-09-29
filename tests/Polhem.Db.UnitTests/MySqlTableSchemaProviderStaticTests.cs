using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Providers.MySql;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Static-only tests for <see cref="MySqlTableSchemaProvider"/>: validates the type-
    /// mapping and default-value parsing helpers that don't require a live MySQL connection.
    /// The full integration coverage (live INFORMATION_SCHEMA reads, GetTableSchema
    /// round-trip) is gated by <c>POLHEM_TEST_CONNSTR_MYSQL</c> and runs in MySqlIntegrationTests.
    /// </summary>
    public class MySqlTableSchemaProviderStaticTests
    {
        #region GetFieldDbType

        [Theory]
        [InlineData("char", 0, 0, 36, FieldDbType.Guid)]
        [InlineData("char", 0, 0, 10, FieldDbType.String)]
        [InlineData("CHAR", 0, 0, 36, FieldDbType.Guid)]
        [InlineData("varchar", 0, 0, 100, FieldDbType.String)]
        [InlineData("VARCHAR", 0, 0, 50, FieldDbType.String)]
        [InlineData("text", 0, 0, 0, FieldDbType.Text)]
        [InlineData("tinytext", 0, 0, 0, FieldDbType.Text)]
        [InlineData("mediumtext", 0, 0, 0, FieldDbType.Text)]
        [InlineData("longtext", 0, 0, 0, FieldDbType.Text)]
        [InlineData("tinyint", 0, 0, 0, FieldDbType.Boolean)]
        [InlineData("TINYINT", 0, 0, 0, FieldDbType.Boolean)]
        [InlineData("smallint", 0, 0, 0, FieldDbType.Short)]
        [InlineData("int", 0, 0, 0, FieldDbType.Integer)]
        [InlineData("integer", 0, 0, 0, FieldDbType.Integer)]
        [InlineData("mediumint", 0, 0, 0, FieldDbType.Integer)]
        [InlineData("bigint", 0, 0, 0, FieldDbType.Long)]
        [InlineData("decimal", 19, 4, 0, FieldDbType.Currency)]
        [InlineData("decimal", 12, 3, 0, FieldDbType.Decimal)]
        [InlineData("numeric", 19, 4, 0, FieldDbType.Currency)]
        [InlineData("numeric", 10, 2, 0, FieldDbType.Decimal)]
        [InlineData("float", 0, 0, 0, FieldDbType.Decimal)]
        [InlineData("double", 0, 0, 0, FieldDbType.Decimal)]
        [InlineData("real", 0, 0, 0, FieldDbType.Decimal)]
        [InlineData("date", 0, 0, 0, FieldDbType.Date)]
        [InlineData("datetime", 0, 0, 0, FieldDbType.DateTime)]
        [InlineData("timestamp", 0, 0, 0, FieldDbType.DateTime)]
        [InlineData("binary", 0, 0, 0, FieldDbType.Binary)]
        [InlineData("varbinary", 0, 0, 0, FieldDbType.Binary)]
        [InlineData("blob", 0, 0, 0, FieldDbType.Binary)]
        [InlineData("tinyblob", 0, 0, 0, FieldDbType.Binary)]
        [InlineData("mediumblob", 0, 0, 0, FieldDbType.Binary)]
        [InlineData("longblob", 0, 0, 0, FieldDbType.Binary)]
        [InlineData("json", 0, 0, 0, FieldDbType.Unknown)]
        [DisplayName("MySQL GetFieldDbType maps each MySQL type")]
        public void GetFieldDbType_VariousMySqlTypes_MapsCorrectly(
            string dataType, int precision, int scale, int length, FieldDbType expected)
        {
            var result = MySqlTableSchemaProvider.GetFieldDbType(dataType, precision, scale, length);

            Assert.Equal(expected, result);
        }

        [Fact]
        [DisplayName("MySQL GetFieldDbType ignores the case of the input string")]
        public void GetFieldDbType_CaseInsensitive()
        {
            Assert.Equal(FieldDbType.Integer, MySqlTableSchemaProvider.GetFieldDbType("INT", 0, 0, 0));
            Assert.Equal(FieldDbType.Long, MySqlTableSchemaProvider.GetFieldDbType("BIGINT", 0, 0, 0));
            Assert.Equal(FieldDbType.Text, MySqlTableSchemaProvider.GetFieldDbType("TEXT", 0, 0, 0));
        }

        [Fact]
        [DisplayName("MySQL GetFieldDbType maps CHAR(36) to Guid and other lengths to String")]
        public void GetFieldDbType_CharLength36_IsGuid_OtherLengthIsString()
        {
            Assert.Equal(FieldDbType.Guid, MySqlTableSchemaProvider.GetFieldDbType("char", 0, 0, 36));
            Assert.Equal(FieldDbType.String, MySqlTableSchemaProvider.GetFieldDbType("char", 0, 0, 35));
            Assert.Equal(FieldDbType.String, MySqlTableSchemaProvider.GetFieldDbType("char", 0, 0, 10));
        }

        [Fact]
        [DisplayName("MySQL GetFieldDbType returns Unknown for NULL or an empty string")]
        public void GetFieldDbType_NullOrEmpty_ReturnsUnknown()
        {
            Assert.Equal(FieldDbType.Unknown, MySqlTableSchemaProvider.GetFieldDbType(null!, 0, 0, 0));
            Assert.Equal(FieldDbType.Unknown, MySqlTableSchemaProvider.GetFieldDbType(string.Empty, 0, 0, 0));
        }

        [Fact]
        [DisplayName("MySQL GetFieldDbType maps DECIMAL(19,4) to Currency")]
        public void GetFieldDbType_Decimal19_4_IsCurrency()
        {
            Assert.Equal(FieldDbType.Currency, MySqlTableSchemaProvider.GetFieldDbType("decimal", 19, 4, 0));
        }

        #endregion

        #region ParseDBDefaultValue

        [Theory]
        [InlineData("int", "0", "0", "")]
        [InlineData("int", "42", "0", "42")]
        [InlineData("varchar", "hello", "", "hello")]
        [InlineData("varchar", "  world  ", "", "world")]
        // The second column is what INFORMATION_SCHEMA actually reports (MySQL strips the outer parentheses and
        // lowercases), and the third is the literal the framework emits. That difference is why this parser exists.
        [InlineData("datetime", "utc_timestamp(6)", "(UTC_TIMESTAMP(6))", "")]
        [InlineData("date", "utc_date()", "(UTC_DATE())", "")]
        [InlineData("char", "uuid()", "(UUID())", "")]
        [InlineData("char", "UUID()", "(UUID())", "")]
        [InlineData("bigint", "100", "0", "100")]
        [InlineData("tinyint", "1", "0", "1")]
        [DisplayName("MySQL ParseDBDefaultValue trims whitespace and compares with the built-in default")]
        public void ParseDBDefaultValue_VariousCases_ReturnsExpected(
            string dataType, string defaultValue, string originalDefault, string expected)
        {
            var result = MySqlTableSchemaProvider.ParseDBDefaultValue(dataType, defaultValue, originalDefault);

            Assert.Equal(expected, result);
        }

        [Fact]
        [DisplayName("MySQL ParseDBDefaultValue returns an empty string for empty input")]
        public void ParseDBDefaultValue_EmptyInput_ReturnsEmpty()
        {
            var result = MySqlTableSchemaProvider.ParseDBDefaultValue("int", string.Empty, "0");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("MySQL ParseDBDefaultValue treats a built-in default that differs only in case as equal (uuid)")]
        public void ParseDBDefaultValue_MatchesBuiltinDefaultCaseInsensitive_ReturnsEmpty()
        {
            // MySQL normalizes (UUID()) to uuid() (lower case, no outer parentheses).
            var result = MySqlTableSchemaProvider.ParseDBDefaultValue("char", "uuid()", "(UUID())");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("MySQL ParseDBDefaultValue strips the outer parentheses of a built-in default before comparing")]
        public void ParseDBDefaultValue_OuterParensInOriginal_StrippedBeforeCompare()
        {
            // The framework emits (UUID()), while MySQL's INFORMATION_SCHEMA returns uuid() with the parentheses
            // stripped. StripOuterParens((UUID())) gives UUID(), which equals uuid() case-insensitively, so the
            // result is an empty string.
            var result = MySqlTableSchemaProvider.ParseDBDefaultValue("char", "uuid()", "(UUID())");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("MySQL ParseDBDefaultValue returns the trimmed value when it differs from the built-in default")]
        public void ParseDBDefaultValue_DifferentFromBuiltin_ReturnsTrimmedValue()
        {
            var result = MySqlTableSchemaProvider.ParseDBDefaultValue("varchar", "  active  ", string.Empty);

            Assert.Equal("active", result);
        }

        [Fact]
        [DisplayName("MySQL ParseDBDefaultValue returns a custom numeric default that differs from 0")]
        public void ParseDBDefaultValue_NumericCustomDefault_ReturnsValue()
        {
            var result = MySqlTableSchemaProvider.ParseDBDefaultValue("int", "99", "0");

            Assert.Equal("99", result);
        }

        #endregion
    }
}
