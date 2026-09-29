using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Providers.PostgreSql;

namespace Polhem.Db.UnitTests
{
    public class PgTableSchemaProviderStaticTests
    {
        #region GetFieldDbType

        [Theory]
        [InlineData("character varying", 0, 0, 50, FieldDbType.String)]
        [InlineData("varchar", 0, 0, 100, FieldDbType.String)]
        [InlineData("character", 0, 0, 10, FieldDbType.String)]
        [InlineData("character varying", 0, 0, 0, FieldDbType.Text)]
        [InlineData("text", 0, 0, 0, FieldDbType.Text)]
        [InlineData("boolean", 0, 0, 0, FieldDbType.Boolean)]
        [InlineData("smallint", 0, 0, 0, FieldDbType.Short)]
        [InlineData("integer", 0, 0, 0, FieldDbType.Integer)]
        [InlineData("bigint", 0, 0, 0, FieldDbType.Long)]
        [InlineData("numeric", 19, 4, 0, FieldDbType.Currency)]
        [InlineData("numeric", 12, 3, 0, FieldDbType.Decimal)]
        [InlineData("decimal", 18, 2, 0, FieldDbType.Decimal)]
        [InlineData("real", 0, 0, 0, FieldDbType.Decimal)]
        [InlineData("double precision", 0, 0, 0, FieldDbType.Decimal)]
        [InlineData("date", 0, 0, 0, FieldDbType.Date)]
        [InlineData("timestamp", 0, 0, 0, FieldDbType.DateTime)]
        [InlineData("timestamp without time zone", 0, 0, 0, FieldDbType.DateTime)]
        [InlineData("timestamp with time zone", 0, 0, 0, FieldDbType.DateTime)]
        [InlineData("uuid", 0, 0, 0, FieldDbType.Guid)]
        [InlineData("bytea", 0, 0, 0, FieldDbType.Binary)]
        [InlineData("json", 0, 0, 0, FieldDbType.Unknown)]
        [DisplayName("PG GetFieldDbType maps each PostgreSQL type")]
        public void GetFieldDbType_VariousPgTypes_MapsCorrectly(
            string dataType, int precision, int scale, int length, FieldDbType expected)
        {
            var result = PgTableSchemaProvider.GetFieldDbType(dataType, precision, scale, length);

            Assert.Equal(expected, result);
        }

        [Fact]
        [DisplayName("PG GetFieldDbType ignores the case of the input string")]
        public void GetFieldDbType_CaseInsensitive()
        {
            Assert.Equal(FieldDbType.Integer, PgTableSchemaProvider.GetFieldDbType("INTEGER", 0, 0, 0));
            Assert.Equal(FieldDbType.Boolean, PgTableSchemaProvider.GetFieldDbType("Boolean", 0, 0, 0));
        }

        #endregion

        #region ParseDBDefaultValue

        [Theory]
        [InlineData("character varying", "'hello'::character varying", "", "hello")]
        [InlineData("varchar", "'world'::character varying", "", "world")]
        [InlineData("text", "'foo'::text", "", "foo")]
        [InlineData("integer", "0", "", "0")]
        [InlineData("integer", "42", "", "42")]
        [InlineData("boolean", "true", "", "1")]
        [InlineData("boolean", "false", "", "0")]
        [InlineData("boolean", "TRUE", "", "1")]
        [InlineData("boolean", "FALSE", "", "0")]
        [InlineData("bool", "true", "", "1")]
        [InlineData("date", "(NOW() AT TIME ZONE 'UTC')", "", "(NOW() AT TIME ZONE 'UTC')")]
        [InlineData("timestamp", "(NOW() AT TIME ZONE 'UTC')", "", "(NOW() AT TIME ZONE 'UTC')")]
        [InlineData("uuid", "gen_random_uuid()", "", "gen_random_uuid()")]
        [DisplayName("PG ParseDBDefaultValue strips the ::cast and the string quotes by type")]
        public void ParseDBDefaultValue_StripsCastAndQuotes(
            string dataType, string defaultValue, string originalDefault, string expected)
        {
            var result = PgTableSchemaProvider.ParseDBDefaultValue(dataType, defaultValue, originalDefault);

            Assert.Equal(expected, result);
        }

        [Fact]
        [DisplayName("PG ParseDBDefaultValue returns an empty string when the value equals the built-in default")]
        public void ParseDBDefaultValue_MatchesBuiltinDefault_ReturnsEmpty()
        {
            // An integer default is usually "0"; after stripping any cast it is "0", equal to the built-in one, so the
            // result is an empty string.
            var result = PgTableSchemaProvider.ParseDBDefaultValue("integer", "0", "0");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("PG ParseDBDefaultValue normalizes boolean false to 0 and returns an empty string when it equals the built-in default")]
        public void ParseDBDefaultValue_BooleanFalseMatchesBuiltin_ReturnsEmpty()
        {
            // The built-in PostgreSQL boolean default is "0" (the canonical form). The database returns "false", which
            // normalizes to "0", so the result is an empty string.
            var result = PgTableSchemaProvider.ParseDBDefaultValue("boolean", "false", "0");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("PG ParseDBDefaultValue restores escaped doubled quotes of a string type")]
        public void ParseDBDefaultValue_EscapedQuoteInString_Unescaped()
        {
            var result = PgTableSchemaProvider.ParseDBDefaultValue(
                "character varying", "'O''Brien'::character varying", "");

            Assert.Equal("O'Brien", result);
        }

        [Fact]
        [DisplayName("PG ParseDBDefaultValue returns an empty string for empty input")]
        public void ParseDBDefaultValue_EmptyInput_ReturnsEmpty()
        {
            var result = PgTableSchemaProvider.ParseDBDefaultValue("integer", string.Empty, "0");

            Assert.Equal(string.Empty, result);
        }

        #endregion
    }
}
