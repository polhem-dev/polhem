using System.ComponentModel;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    public class DatabaseTypeExtensionsTests
    {
        #region QuoteIdentifier escaping

        [Theory]
        [InlineData(DatabaseType.SQLServer, "Name", "[Name]")]
        [InlineData(DatabaseType.SQLServer, "Col]umn", "[Col]]umn]")]
        [InlineData(DatabaseType.SQLServer, "A]]B", "[A]]]]B]")]
        [DisplayName("QuoteIdentifier for SQL Server escapes the ] character")]
        public void QuoteIdentifier_SqlServer_EscapesBracket(DatabaseType dbType, string identifier, string expected)
        {
            var result = dbType.QuoteIdentifier(identifier);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(DatabaseType.MySQL, "Name", "`Name`")]
        [InlineData(DatabaseType.MySQL, "Col`umn", "`Col``umn`")]
        [DisplayName("QuoteIdentifier for MySQL escapes the ` character")]
        public void QuoteIdentifier_MySql_EscapesBacktick(DatabaseType dbType, string identifier, string expected)
        {
            var result = dbType.QuoteIdentifier(identifier);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(DatabaseType.SQLite, "Name", "\"Name\"")]
        [InlineData(DatabaseType.SQLite, "Col\"umn", "\"Col\"\"umn\"")]
        [InlineData(DatabaseType.PostgreSQL, "Name", "\"Name\"")]
        [InlineData(DatabaseType.PostgreSQL, "Col\"umn", "\"Col\"\"umn\"")]
        [DisplayName("QuoteIdentifier for SQLite and PostgreSQL escapes double quotes and keeps the original case")]
        public void QuoteIdentifier_SqlitePg_EscapesDoubleQuote(DatabaseType dbType, string identifier, string expected)
        {
            var result = dbType.QuoteIdentifier(identifier);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Name", "\"NAME\"")]
        [InlineData("col", "\"COL\"")]
        [InlineData("Col\"umn", "\"COL\"\"UMN\"")]
        [DisplayName("QuoteIdentifier for Oracle escapes double quotes and uppercases the identifier (adapter boundary policy)")]
        public void QuoteIdentifier_Oracle_UppercasesAndEscapes(string identifier, string expected)
        {
            // Oracle uses the quoted-UPPERCASE policy: the framework uppercases Oracle identifiers at emit time before
            // quoting them, matching how Oracle stores unquoted names (folded to upper case). SQL Server and MySQL are
            // case-insensitive and need no such handling; PostgreSQL and SQLite store names case-sensitively as given.
            var result = DatabaseType.Oracle.QuoteIdentifier(identifier);
            Assert.Equal(expected, result);
        }

        [Fact]
        [DisplayName("QuoteIdentifier throws NotSupportedException for an unsupported database type")]
        public void QuoteIdentifier_UnsupportedType_Throws()
        {
            Assert.Throws<NotSupportedException>(() =>
                ((DatabaseType)999).QuoteIdentifier("Test"));
        }

        #endregion

        #region GetParameterPrefix

        [Theory]
        [InlineData(DatabaseType.SQLServer, "@")]
        [InlineData(DatabaseType.MySQL, "@")]
        [InlineData(DatabaseType.SQLite, "@")]
        [InlineData(DatabaseType.Oracle, ":")]
        [InlineData(DatabaseType.PostgreSQL, "@")]
        [DisplayName("GetParameterPrefix returns the parameter prefix of each database")]
        public void GetParameterPrefix_ReturnsCorrectPrefix(DatabaseType dbType, string expected)
        {
            var result = dbType.GetParameterPrefix();
            Assert.Equal(expected, result);
        }

        [Fact]
        [DisplayName("GetParameterPrefix throws NotSupportedException for an unsupported database type")]
        public void GetParameterPrefix_UnsupportedType_Throws()
        {
            Assert.Throws<NotSupportedException>(() =>
                ((DatabaseType)999).GetParameterPrefix());
        }

        #endregion

        #region GetParameterName

        [Theory]
        [InlineData(DatabaseType.SQLServer, "Id", "@Id")]
        [InlineData(DatabaseType.MySQL, "Id", "@Id")]
        [InlineData(DatabaseType.SQLite, "Id", "@Id")]
        [InlineData(DatabaseType.Oracle, "Id", ":Id")]
        [InlineData(DatabaseType.PostgreSQL, "Id", "@Id")]
        [DisplayName("GetParameterName adds the prefix of each database type")]
        public void GetParameterName_AppendsPrefix(DatabaseType dbType, string name, string expected)
        {
            var result = dbType.GetParameterName(name);
            Assert.Equal(expected, result);
        }

        #endregion
    }
}
