using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Providers.MySql;
using Polhem.Db.Providers.Oracle;
using Polhem.Db.Providers.PostgreSql;
using Polhem.Db.Providers.Sqlite;
using Polhem.Db.Providers.SqlServer;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// A definition's <see cref="DbField.DefaultValue"/> reaches the DDL as a literal: quoted and escaped for a string
    /// column, and for any other column only when it parses as a literal of the column's type.
    /// </summary>
    /// <remarks>
    /// Non-string defaults are written into the <c>DEFAULT</c> clause unquoted, so before the check a numeric default
    /// such as <c>0) ; DROP TABLE x --</c> ran as SQL during schema creation.
    /// </remarks>
    public class DefaultValueLiteralTests
    {
        private static readonly Func<DbField, string>[] s_dialects =
        [
            SqlSchemaSyntax.GetDefaultExpression,
            PgSchemaSyntax.GetDefaultExpression,
            MySqlSchemaSyntax.GetDefaultExpression,
            SqliteSchemaSyntax.GetDefaultExpression,
            OracleSchemaSyntax.GetDefaultExpression,
        ];

        [Fact]
        [DisplayName("SQL Server doubles a single quote inside a String default, like the other dialects")]
        public void SqlServer_StringDefaultWithQuote_IsEscaped()
        {
            var field = new DbField("name", "Name", FieldDbType.String) { Length = 50, DefaultValue = "O'Brien" };

            Assert.Equal("N'O''Brien'", SqlSchemaSyntax.GetDefaultExpression(field));
        }

        [Theory]
        [InlineData(FieldDbType.Integer, "0) ; DROP TABLE st_user --")]
        [InlineData(FieldDbType.Integer, "1 + 1")]
        [InlineData(FieldDbType.Short, "70000")]
        [InlineData(FieldDbType.Long, "1e3")]
        [InlineData(FieldDbType.Decimal, "1,5")]
        [InlineData(FieldDbType.Currency, "(SELECT 1)")]
        [InlineData(FieldDbType.Boolean, "true")]
        [InlineData(FieldDbType.Boolean, "2")]
        [InlineData(FieldDbType.DateTime, "getdate()")]
        [InlineData(FieldDbType.Date, "2026-01-01")]
        [InlineData(FieldDbType.Guid, "newid()")]
        [DisplayName("Every dialect refuses a non-string default that is not a literal of the column type")]
        public void AllDialects_NonLiteralDefault_Throws(FieldDbType dbType, string defaultValue)
        {
            var field = new DbField("col", "Col", dbType) { DefaultValue = defaultValue };

            foreach (var dialect in s_dialects)
                Assert.Throws<InvalidOperationException>(() => dialect(field));
        }

        [Theory]
        [InlineData(FieldDbType.Integer, "42")]
        [InlineData(FieldDbType.Integer, "-7")]
        [InlineData(FieldDbType.Short, "3")]
        [InlineData(FieldDbType.Long, "9000000000")]
        [InlineData(FieldDbType.Decimal, "1.25")]
        [InlineData(FieldDbType.Currency, "-0.5")]
        [DisplayName("Control case: a numeric literal default is emitted unchanged by every dialect")]
        public void AllDialects_NumericLiteralDefault_IsEmittedAsIs(FieldDbType dbType, string defaultValue)
        {
            var field = new DbField("col", "Col", dbType) { DefaultValue = defaultValue };

            foreach (var dialect in s_dialects)
                Assert.Equal(defaultValue, dialect(field));
        }

        [Theory]
        [InlineData("1")]
        [InlineData("0")]
        [DisplayName("Control case: a Boolean default of 0 or 1 is accepted by every dialect")]
        public void AllDialects_BooleanLiteralDefault_IsAccepted(string defaultValue)
        {
            var field = new DbField("flag", "Flag", FieldDbType.Boolean) { DefaultValue = defaultValue };

            foreach (var dialect in s_dialects)
                Assert.False(string.IsNullOrEmpty(dialect(field)));
        }
    }
}
