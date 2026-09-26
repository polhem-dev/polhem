using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.Sqlite;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Pure syntax tests covering <see cref="SqliteSchemaSyntax"/>: identifier quoting, string escaping, default value
    /// expressions, and column / AutoIncrement column definition assembly.
    /// </summary>
    public class SqliteSchemaSyntaxTests
    {
        #region QuoteName

        [Theory]
        [InlineData("st_user", "\"st_user\"")]
        [InlineData("name", "\"name\"")]
        [InlineData("col\"with quote", "\"col\"\"with quote\"")]
        [DisplayName("SQLite QuoteName wraps in double quotes and escapes inner double quotes")]
        public void QuoteName_VariousIdentifiers_QuotesProperly(string identifier, string expected)
        {
            Assert.Equal(expected, SqliteSchemaSyntax.QuoteName(identifier));
        }

        #endregion

        #region EscapeSqlString

        [Theory]
        [InlineData("hello", "hello")]
        [InlineData("O'Brien", "O''Brien")]
        [InlineData("''", "''''")]
        [DisplayName("SQLite EscapeSqlString doubles single quotes so the literal stays intact")]
        public void EscapeSqlString_DoublesSingleQuotes(string input, string expected)
        {
            Assert.Equal(expected, SqliteSchemaSyntax.EscapeSqlString(input));
        }

        #endregion

        #region GetDefaultValueExpression

        [Theory]
        [InlineData(FieldDbType.String, "")]
        [InlineData(FieldDbType.Text, "")]
        [InlineData(FieldDbType.Time, "")]
        [InlineData(FieldDbType.Boolean, "0")]
        [InlineData(FieldDbType.Short, "0")]
        [InlineData(FieldDbType.Integer, "0")]
        [InlineData(FieldDbType.Long, "0")]
        [InlineData(FieldDbType.Decimal, "0")]
        [InlineData(FieldDbType.Currency, "0")]
        [InlineData(FieldDbType.Date, "CURRENT_TIMESTAMP")]
        [InlineData(FieldDbType.DateTime, "CURRENT_TIMESTAMP")]
        [InlineData(FieldDbType.Guid, "(hex(randomblob(16)))")]
        [InlineData(FieldDbType.Binary, "")]
        [InlineData(FieldDbType.AutoIncrement, "")]
        [DisplayName("SQLite GetDefaultValueExpression returns the SQLite default expression of each type")]
        public void GetDefaultValueExpression_VariousTypes_ReturnsExpected(FieldDbType dbType, string expected)
        {
            Assert.Equal(expected, SqliteSchemaSyntax.GetDefaultValueExpression(dbType));
        }

        #endregion

        #region GetDefaultExpression

        [Fact]
        [DisplayName("SQLite GetDefaultExpression returns an empty string for an AllowNull field (no DEFAULT clause)")]
        public void GetDefaultExpression_AllowNull_ReturnsEmpty()
        {
            var field = new DbField("v", "V", FieldDbType.Integer) { AllowNull = true };
            Assert.Equal(string.Empty, SqliteSchemaSyntax.GetDefaultExpression(field));
        }

        [Fact]
        [DisplayName("SQLite GetDefaultExpression returns an empty string for an AutoIncrement field (the inline PK needs no DEFAULT)")]
        public void GetDefaultExpression_AutoIncrement_ReturnsEmpty()
        {
            var field = new DbField("v", "V", FieldDbType.AutoIncrement);
            Assert.Equal(string.Empty, SqliteSchemaSyntax.GetDefaultExpression(field));
        }

        [Fact]
        [DisplayName("SQLite GetDefaultExpression returns '' for a String without a custom default")]
        public void GetDefaultExpression_StringNoCustom_ReturnsEmptyLiteral()
        {
            var field = new DbField("v", "V", FieldDbType.String) { Length = 50 };
            Assert.Equal("''", SqliteSchemaSyntax.GetDefaultExpression(field));
        }

        [Fact]
        [DisplayName("SQLite GetDefaultExpression wraps a custom String default in single quotes and escapes inner quotes")]
        public void GetDefaultExpression_StringCustomWithQuote_EscapesAndWraps()
        {
            var field = new DbField("v", "V", FieldDbType.String)
            {
                Length = 50,
                DefaultValue = "O'Brien"
            };
            Assert.Equal("'O''Brien'", SqliteSchemaSyntax.GetDefaultExpression(field));
        }

        [Fact]
        [DisplayName("SQLite GetDefaultExpression outputs a custom Integer default as is")]
        public void GetDefaultExpression_IntegerCustom_ReturnsRaw()
        {
            var field = new DbField("v", "V", FieldDbType.Integer) { DefaultValue = "42" };
            Assert.Equal("42", SqliteSchemaSyntax.GetDefaultExpression(field));
        }

        [Fact]
        [DisplayName("SQLite GetDefaultExpression returns the built-in 0 for an Integer without a custom default")]
        public void GetDefaultExpression_IntegerNoCustom_ReturnsBuiltinZero()
        {
            var field = new DbField("v", "V", FieldDbType.Integer);
            Assert.Equal("0", SqliteSchemaSyntax.GetDefaultExpression(field));
        }

        #endregion

        #region GetColumnDefinition

        [Fact]
        [DisplayName("SQLite GetColumnDefinition for a String field carries COLLATE NOCASE and DEFAULT")]
        public void GetColumnDefinition_String_IncludesCollateAndDefault()
        {
            var field = new DbField("name", "Name", FieldDbType.String) { Length = 50 };
            var sql = SqliteSchemaSyntax.GetColumnDefinition(field);
            Assert.Contains("\"name\" VARCHAR(50) COLLATE NOCASE NOT NULL DEFAULT ''", sql);
        }

        [Fact]
        [DisplayName("SQLite GetColumnDefinition for a Guid field carries COLLATE NOCASE (GUID comparison ignores case)")]
        public void GetColumnDefinition_Guid_IncludesCollateNoCase()
        {
            var field = new DbField("sys_rowid", "RowId", FieldDbType.Guid);
            var sql = SqliteSchemaSyntax.GetColumnDefinition(field);
            Assert.Contains("\"sys_rowid\" UUID COLLATE NOCASE NOT NULL", sql);
        }

        [Fact]
        [DisplayName("SQLite GetColumnDefinition for a NOT NULL Integer carries DEFAULT 0")]
        public void GetColumnDefinition_IntegerNotNull_IncludesDefaultZero()
        {
            var field = new DbField("count", "Count", FieldDbType.Integer);
            var sql = SqliteSchemaSyntax.GetColumnDefinition(field);
            Assert.Contains("\"count\" INTEGER NOT NULL DEFAULT 0", sql);
        }

        [Fact]
        [DisplayName("SQLite GetColumnDefinition for AllowNull has no DEFAULT clause")]
        public void GetColumnDefinition_AllowNull_OmitsDefault()
        {
            var field = new DbField("count", "Count", FieldDbType.Integer) { AllowNull = true };
            var sql = SqliteSchemaSyntax.GetColumnDefinition(field);
            Assert.Equal("\"count\" INTEGER NULL", sql);
        }

        #endregion

        #region GetAutoIncrementColumnDefinition

        [Fact]
        [DisplayName("SQLite GetAutoIncrementColumnDefinition inlines INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL")]
        public void GetAutoIncrementColumnDefinition_InlinesPrimaryKey()
        {
            var field = new DbField("sys_no", "Seq", FieldDbType.AutoIncrement);
            var sql = SqliteSchemaSyntax.GetAutoIncrementColumnDefinition(field);
            Assert.Equal("\"sys_no\" INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL", sql);
        }

        #endregion
    }
}
