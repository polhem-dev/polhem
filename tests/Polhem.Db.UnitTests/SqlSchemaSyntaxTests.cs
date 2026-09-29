using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Providers.SqlServer;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Pure syntax tests covering <see cref="SqlSchemaSyntax"/>: identifier quoting, string escaping, data type
    /// conversion, default value expressions and column definition assembly.
    /// </summary>
    public class SqlSchemaSyntaxTests
    {
        #region QuoteName

        [Theory]
        [InlineData("st_user", "[st_user]")]
        [InlineData("name", "[name]")]
        [InlineData("col]with bracket", "[col]]with bracket]")]
        [InlineData("", "[]")]
        [DisplayName("SQL Server QuoteName wraps in square brackets and escapes an inner ]")]
        public void QuoteName_VariousIdentifiers_QuotesProperly(string identifier, string expected)
        {
            Assert.Equal(expected, SqlSchemaSyntax.QuoteName(identifier));
        }

        #endregion

        #region EscapeSqlString

        [Theory]
        [InlineData("hello", "hello")]
        [InlineData("O'Brien", "O''Brien")]
        [InlineData("it's a 'test'", "it''s a ''test''")]
        [InlineData("", "")]
        [DisplayName("SQL Server EscapeSqlString doubles single quotes so the literal stays intact")]
        public void EscapeSqlString_DoublesSingleQuotes(string input, string expected)
        {
            Assert.Equal(expected, SqlSchemaSyntax.EscapeSqlString(input));
        }

        #endregion

        #region ConvertDbType

        [Theory]
        [InlineData(FieldDbType.Text, "[nvarchar](max)")]
        [InlineData(FieldDbType.Boolean, "[bit]")]
        [InlineData(FieldDbType.AutoIncrement, "[int] IDENTITY(1,1)")]
        [InlineData(FieldDbType.Short, "[smallint]")]
        [InlineData(FieldDbType.Integer, "[int]")]
        [InlineData(FieldDbType.Long, "[bigint]")]
        [InlineData(FieldDbType.Currency, "[decimal](19,4)")]
        [InlineData(FieldDbType.Date, "[date]")]
        [InlineData(FieldDbType.DateTime, "[datetime2](7)")]
        [InlineData(FieldDbType.Time, "[nchar](5)")]
        [InlineData(FieldDbType.Guid, "[uniqueidentifier]")]
        [InlineData(FieldDbType.Binary, "[varbinary](max)")]
        [DisplayName("SQL Server ConvertDbType returns the SQL Server type string of each type")]
        public void ConvertDbType_VariousTypes_ReturnsExpectedSql(FieldDbType dbType, string expected)
        {
            var field = new DbField("f", "F", dbType);
            Assert.Equal(expected, SqlSchemaSyntax.ConvertDbType(field));
        }

        [Fact]
        [DisplayName("SQL Server ConvertDbType includes the Length for String")]
        public void ConvertDbType_String_IncludesLength()
        {
            var field = new DbField("name", "Name", FieldDbType.String) { Length = 50 };
            Assert.Equal("[nvarchar](50)", SqlSchemaSyntax.ConvertDbType(field));
        }

        [Fact]
        [DisplayName("SQL Server ConvertDbType uses the default precision 18,0 for Decimal")]
        public void ConvertDbType_DecimalDefault_ReturnsDecimal18_0()
        {
            var field = new DbField("f", "F", FieldDbType.Decimal);
            Assert.Equal("[decimal](18,0)", SqlSchemaSyntax.ConvertDbType(field));
        }

        [Fact]
        [DisplayName("SQL Server ConvertDbType outputs a custom Decimal precision")]
        public void ConvertDbType_DecimalCustom_ReturnsCorrectPrecision()
        {
            var field = new DbField("f", "F", FieldDbType.Decimal) { Precision = 12, Scale = 3 };
            Assert.Equal("[decimal](12,3)", SqlSchemaSyntax.ConvertDbType(field));
        }

        [Fact]
        [DisplayName("SQL Server ConvertDbType throws InvalidOperationException for Unknown")]
        public void ConvertDbType_UnknownType_ThrowsInvalidOperationException()
        {
            var field = new DbField("f", "F", FieldDbType.Unknown);
            Assert.Throws<InvalidOperationException>(() => SqlSchemaSyntax.ConvertDbType(field));
        }

        #endregion

        #region GetDefaultValueExpression

        [Theory]
        [InlineData(FieldDbType.String, "")]
        [InlineData(FieldDbType.Text, "")]
        [InlineData(FieldDbType.Boolean, "0")]
        [InlineData(FieldDbType.Short, "0")]
        [InlineData(FieldDbType.Integer, "0")]
        [InlineData(FieldDbType.Long, "0")]
        [InlineData(FieldDbType.Decimal, "0")]
        [InlineData(FieldDbType.Currency, "0")]
        [InlineData(FieldDbType.Date, "getutcdate()")]
        [InlineData(FieldDbType.DateTime, "getutcdate()")]
        [InlineData(FieldDbType.Guid, "newid()")]
        [InlineData(FieldDbType.AutoIncrement, "")]
        [InlineData(FieldDbType.Binary, "")]
        [InlineData(FieldDbType.Unknown, "")]
        [DisplayName("SQL Server GetDefaultValueExpression returns the SQL Server default expression of each type")]
        public void GetDefaultValueExpression_VariousTypes_ReturnsExpected(FieldDbType dbType, string expected)
        {
            Assert.Equal(expected, SqlSchemaSyntax.GetDefaultValueExpression(dbType));
        }

        #endregion

        #region GetDefaultExpression

        [Fact]
        [DisplayName("SQL Server GetDefaultExpression returns an empty string for an AllowNull field (no DEFAULT clause)")]
        public void GetDefaultExpression_AllowNull_ReturnsEmpty()
        {
            var field = new DbField("f", "F", FieldDbType.Integer) { AllowNull = true };
            Assert.Equal(string.Empty, SqlSchemaSyntax.GetDefaultExpression(field));
        }

        [Fact]
        [DisplayName("SQL Server GetDefaultExpression returns an empty string for an AutoIncrement field")]
        public void GetDefaultExpression_AutoIncrement_ReturnsEmpty()
        {
            var field = new DbField("pk", "PK", FieldDbType.AutoIncrement);
            Assert.Equal(string.Empty, SqlSchemaSyntax.GetDefaultExpression(field));
        }

        [Fact]
        [DisplayName("SQL Server GetDefaultExpression returns N'' for a String without a custom default")]
        public void GetDefaultExpression_StringNoCustom_ReturnsEmptyNLiteral()
        {
            var field = new DbField("name", "Name", FieldDbType.String) { Length = 50 };
            Assert.Equal("N''", SqlSchemaSyntax.GetDefaultExpression(field));
        }

        [Fact]
        [DisplayName("SQL Server GetDefaultExpression wraps a custom String default in N'...'")]
        public void GetDefaultExpression_StringCustom_ReturnsNLiteralWrapped()
        {
            var field = new DbField("name", "Name", FieldDbType.String) { Length = 50, DefaultValue = "hello" };
            Assert.Equal("N'hello'", SqlSchemaSyntax.GetDefaultExpression(field));
        }

        [Fact]
        [DisplayName("SQL Server GetDefaultExpression returns the built-in default 0 for an Integer without a custom default")]
        public void GetDefaultExpression_IntegerNoCustom_ReturnsBuiltinZero()
        {
            var field = new DbField("age", "Age", FieldDbType.Integer);
            Assert.Equal("0", SqlSchemaSyntax.GetDefaultExpression(field));
        }

        [Fact]
        [DisplayName("SQL Server GetDefaultExpression outputs a custom Integer default as is")]
        public void GetDefaultExpression_IntegerCustom_ReturnsRaw()
        {
            var field = new DbField("age", "Age", FieldDbType.Integer) { DefaultValue = "42" };
            Assert.Equal("42", SqlSchemaSyntax.GetDefaultExpression(field));
        }

        [Fact]
        [DisplayName("SQL Server GetDefaultExpression returns getutcdate() for a DateTime without a custom default")]
        public void GetDefaultExpression_DateTimeNoCustom_ReturnsGetDate()
        {
            var field = new DbField("created_at", "Created", FieldDbType.DateTime);
            Assert.Equal("getutcdate()", SqlSchemaSyntax.GetDefaultExpression(field));
        }

        [Fact]
        [DisplayName("SQL Server GetDefaultExpression returns newid() for a Guid without a custom default")]
        public void GetDefaultExpression_GuidNoCustom_ReturnsNewId()
        {
            var field = new DbField("sys_rowid", "Row ID", FieldDbType.Guid);
            Assert.Equal("newid()", SqlSchemaSyntax.GetDefaultExpression(field));
        }

        [Fact]
        [DisplayName("SQL Server GetDefaultExpression returns N'' for a Text without a custom default")]
        public void GetDefaultExpression_TextNoCustom_ReturnsEmptyNLiteral()
        {
            var field = new DbField("content", "Content", FieldDbType.Text);
            Assert.Equal("N''", SqlSchemaSyntax.GetDefaultExpression(field));
        }

        #endregion

        #region GetColumnDefinition

        [Fact]
        [DisplayName("SQL Server GetColumnDefinition for a NOT NULL String includes the type, NOT NULL and DEFAULT")]
        public void GetColumnDefinition_StringNotNull_IncludesTypeNullabilityAndDefault()
        {
            var field = new DbField("name", "Name", FieldDbType.String) { Length = 50 };
            var sql = SqlSchemaSyntax.GetColumnDefinition(field);
            Assert.Contains("[name]", sql);
            Assert.Contains("[nvarchar](50)", sql);
            Assert.Contains("NOT NULL", sql);
            Assert.Contains("DEFAULT (N'')", sql);
        }

        [Fact]
        [DisplayName("SQL Server GetColumnDefinition for a NOT NULL Integer includes DEFAULT 0")]
        public void GetColumnDefinition_IntegerNotNull_IncludesDefaultZero()
        {
            var field = new DbField("count", "Count", FieldDbType.Integer);
            var sql = SqlSchemaSyntax.GetColumnDefinition(field);
            Assert.Equal("[count] [int] NOT NULL DEFAULT (0)", sql);
        }

        [Fact]
        [DisplayName("SQL Server GetColumnDefinition for AllowNull has no DEFAULT clause")]
        public void GetColumnDefinition_AllowNull_OmitsDefaultClause()
        {
            var field = new DbField("remark", "Remark", FieldDbType.Integer) { AllowNull = true };
            var sql = SqlSchemaSyntax.GetColumnDefinition(field);
            Assert.Equal("[remark] [int] NULL", sql);
        }

        [Fact]
        [DisplayName("SQL Server GetColumnDefinition for a NOT NULL Guid includes DEFAULT (newid())")]
        public void GetColumnDefinition_GuidNotNull_IncludesNewId()
        {
            var field = new DbField("sys_rowid", "Row ID", FieldDbType.Guid);
            var sql = SqlSchemaSyntax.GetColumnDefinition(field);
            Assert.Equal("[sys_rowid] [uniqueidentifier] NOT NULL DEFAULT (newid())", sql);
        }

        [Fact]
        [DisplayName("SQL Server GetColumnDefinition escapes a field name containing ]")]
        public void GetColumnDefinition_IdentifierWithBracket_EscapesProperly()
        {
            var field = new DbField("col]name", "Col", FieldDbType.Integer);
            var sql = SqlSchemaSyntax.GetColumnDefinition(field);
            Assert.StartsWith("[col]]name]", sql);
        }

        #endregion
    }
}
