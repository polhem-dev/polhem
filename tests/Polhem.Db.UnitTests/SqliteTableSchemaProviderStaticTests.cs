using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.Sqlite;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Pure syntax tests covering the static parsing methods of <see cref="SqliteTableSchemaProvider"/>
    /// (<c>MapToFieldDbType</c> and <c>ParseDefaultValue</c>), symmetric with
    /// <see cref="PgTableSchemaProviderStaticTests"/>.
    /// </summary>
    public class SqliteTableSchemaProviderStaticTests
    {
        #region MapToFieldDbType

        [Theory]
        [InlineData("VARCHAR", false, FieldDbType.String)]
        [InlineData("CHAR", false, FieldDbType.String)]
        [InlineData("CHARACTER", false, FieldDbType.String)]
        [InlineData("NVARCHAR", false, FieldDbType.String)]
        [InlineData("TEXT", false, FieldDbType.Text)]
        [InlineData("CLOB", false, FieldDbType.Text)]
        [InlineData("BOOLEAN", false, FieldDbType.Boolean)]
        [InlineData("BOOL", false, FieldDbType.Boolean)]
        [InlineData("SMALLINT", false, FieldDbType.Short)]
        [InlineData("INT2", false, FieldDbType.Short)]
        [InlineData("INTEGER", false, FieldDbType.Integer)]
        [InlineData("INT", false, FieldDbType.Integer)]
        [InlineData("INT4", false, FieldDbType.Integer)]
        [InlineData("BIGINT", false, FieldDbType.Long)]
        [InlineData("INT8", false, FieldDbType.Long)]
        [InlineData("NUMERIC", false, FieldDbType.Decimal)]
        [InlineData("DECIMAL", false, FieldDbType.Decimal)]
        [InlineData("REAL", false, FieldDbType.Decimal)]
        [InlineData("DOUBLE", false, FieldDbType.Decimal)]
        [InlineData("FLOAT", false, FieldDbType.Decimal)]
        [InlineData("DATE", false, FieldDbType.Date)]
        [InlineData("DATETIME", false, FieldDbType.DateTime)]
        [InlineData("TIMESTAMP", false, FieldDbType.DateTime)]
        [InlineData("UUID", false, FieldDbType.Guid)]
        [InlineData("BLOB", false, FieldDbType.Binary)]
        [InlineData("BINARY", false, FieldDbType.Binary)]
        [InlineData("JSON", false, FieldDbType.Unknown)]
        [DisplayName("SQLite MapToFieldDbType maps each SQLite type")]
        public void MapToFieldDbType_VariousTypes_MapsCorrectly(string baseType, bool isPrimaryKey, FieldDbType expected)
        {
            Assert.Equal(expected, SqliteTableSchemaProvider.MapToFieldDbType(baseType, isPrimaryKey));
        }

        [Fact]
        [DisplayName("SQLite MapToFieldDbType maps an INTEGER PK to AutoIncrement (rowid alias)")]
        public void MapToFieldDbType_IntegerPrimaryKey_MapsToAutoIncrement()
        {
            Assert.Equal(FieldDbType.AutoIncrement,
                SqliteTableSchemaProvider.MapToFieldDbType("INTEGER", isPrimaryKey: true));
        }

        [Fact]
        [DisplayName("SQLite MapToFieldDbType maps a non-PK INTEGER to Integer")]
        public void MapToFieldDbType_IntegerNonPrimaryKey_MapsToInteger()
        {
            Assert.Equal(FieldDbType.Integer,
                SqliteTableSchemaProvider.MapToFieldDbType("INTEGER", isPrimaryKey: false));
        }

        [Fact]
        [DisplayName("SQLite MapToFieldDbType ignores the case of the input string")]
        public void MapToFieldDbType_CaseInsensitive()
        {
            Assert.Equal(FieldDbType.Boolean, SqliteTableSchemaProvider.MapToFieldDbType("boolean", false));
            Assert.Equal(FieldDbType.Decimal, SqliteTableSchemaProvider.MapToFieldDbType("Numeric", false));
            Assert.Equal(FieldDbType.Date, SqliteTableSchemaProvider.MapToFieldDbType("date", false));
        }

        [Fact]
        [DisplayName("SQLite MapToFieldDbType maps null or an empty string to Unknown")]
        public void MapToFieldDbType_NullOrEmpty_ReturnsUnknown()
        {
            Assert.Equal(FieldDbType.Unknown, SqliteTableSchemaProvider.MapToFieldDbType(null!, false));
            Assert.Equal(FieldDbType.Unknown, SqliteTableSchemaProvider.MapToFieldDbType(string.Empty, false));
        }

        #endregion

        #region ParseDefaultValue

        [Theory]
        [InlineData("0", FieldDbType.Integer, "0", "")]
        [InlineData("'hello'", FieldDbType.String, "", "hello")]
        [InlineData("'world'", FieldDbType.Text, "", "world")]
        [InlineData("CURRENT_TIMESTAMP", FieldDbType.DateTime, "CURRENT_TIMESTAMP", "")]
        [InlineData("42", FieldDbType.Integer, "0", "42")]
        [DisplayName("SQLite ParseDefaultValue strips the quotes and outer parentheses by type")]
        public void ParseDefaultValue_StripsQuotesAndParens(
            string raw, FieldDbType dbType, string original, string expected)
        {
            Assert.Equal(expected, SqliteTableSchemaProvider.ParseDefaultValue(raw, dbType, original));
        }

        [Theory]
        // The form PRAGMA table_info actually reports: SQLite has stripped the outer parentheses, while the built-in
        // default still has them.
        [InlineData("hex(randomblob(16))")]
        // To be safe, the parenthesized input as is must normalize to the same result.
        [InlineData("(hex(randomblob(16)))")]
        [DisplayName("SQLite ParseDefaultValue normalizes the built-in Guid default to an empty string (with or without outer parentheses)")]
        public void ParseDefaultValue_GuidBuiltinDefault_ReturnsEmpty(string raw)
        {
            // The built-in default (hex(randomblob(16))) needs the parentheses in DDL to be a valid SQLite expression
            // default, but PRAGMA table_info reports only the expression inside them. Only after normalizing both sides
            // can they be found equal to the built-in default; otherwise every schema comparison would mark the Guid
            // field for Upgrade and never converge.
            var result = SqliteTableSchemaProvider.ParseDefaultValue(
                raw, FieldDbType.Guid, "(hex(randomblob(16)))");
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("SQLite ParseDefaultValue keeps a non-built-in function default as the expression without its outer parentheses")]
        public void ParseDefaultValue_NonBuiltinFunctionDefault_ReturnsUnwrappedExpression()
        {
            var result = SqliteTableSchemaProvider.ParseDefaultValue(
                "(lower(hex(randomblob(16))))", FieldDbType.Guid, "(hex(randomblob(16)))");
            Assert.Equal("lower(hex(randomblob(16)))", result);
        }

        [Fact]
        [DisplayName("SQLite ParseDefaultValue does not strip leading and trailing parentheses that are not a pair")]
        public void ParseDefaultValue_UnpairedOuterParens_NotStripped()
        {
            var result = SqliteTableSchemaProvider.ParseDefaultValue(
                "(a)||(b)", FieldDbType.Text, string.Empty);
            Assert.Equal("(a)||(b)", result);
        }

        [Fact]
        [DisplayName("SQLite ParseDefaultValue unescapes doubled quotes for string types")]
        public void ParseDefaultValue_EscapedQuoteInString_Unescaped()
        {
            var result = SqliteTableSchemaProvider.ParseDefaultValue("'O''Brien'", FieldDbType.String, string.Empty);
            Assert.Equal("O'Brien", result);
        }

        [Fact]
        [DisplayName("SQLite ParseDefaultValue returns an empty string when the value equals the built-in default")]
        public void ParseDefaultValue_MatchesBuiltinDefault_ReturnsEmpty()
        {
            var result = SqliteTableSchemaProvider.ParseDefaultValue("0", FieldDbType.Integer, "0");
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        [DisplayName("SQLite ParseDefaultValue returns an empty string for empty input")]
        public void ParseDefaultValue_EmptyInput_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, SqliteTableSchemaProvider.ParseDefaultValue(string.Empty, FieldDbType.Integer, "0"));
        }

        [Fact]
        [DisplayName("SQLite ParseDefaultValue strips the outer parentheses (the usual wrapped form of hex(randomblob))")]
        public void ParseDefaultValue_OuterParens_AreStripped()
        {
            // SQLite usually stores a function-call default wrapped in (...), so the outer layer must be stripped to
            // restore the original expression.
            var result = SqliteTableSchemaProvider.ParseDefaultValue("(CURRENT_TIMESTAMP)", FieldDbType.DateTime, "CURRENT_TIMESTAMP");
            Assert.Equal(string.Empty, result);
        }

        #endregion
    }
}
