using System.ComponentModel;
using System.Globalization;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;
using Polhem.Db.Providers.MySql;
using Polhem.Db.Providers.Oracle;
using Polhem.Db.Providers.PostgreSql;
using Polhem.Db.Providers.Sqlite;
using Polhem.Db.Providers.SqlServer;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// The literal escaping rules of <c>EscapeSqlString</c> in each dialect.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These values (field Caption, table DisplayName, DefaultValue) are free text in the definition files, and they
    /// are spliced into <c>'...'</c> literals in the DDL. Insufficient escaping is not only an injection surface:
    /// <b>ordinary descriptive text that ends with a backslash</b> is enough to make the generated DDL a syntax error
    /// on MySQL.
    /// </para>
    /// <para>
    /// MySQL is the only dialect that has to handle backslashes: in SQL Server, Oracle, SQLite, and PostgreSQL with
    /// <c>standard_conforming_strings</c> on by default, a backslash is an ordinary character. This pins down which
    /// dialects escape backslashes, so that nobody later "unifies" them in passing and breaks one side.
    /// </para>
    /// </remarks>
    public class SchemaSyntaxEscapingTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SchemaSyntaxEscapingTests(SharedDbFixture fx) { _fx = fx; }

        [Theory]
        [InlineData("plain", "plain")]
        [InlineData("O'Brien", "O''Brien")]
        [InlineData("''", "''''")]
        [DisplayName("Every dialect doubles single quotes")]
        public void AllDialects_DoubleSingleQuotes(string input, string expected)
        {
            Assert.Equal(expected, SqlSchemaSyntax.EscapeSqlString(input));
            Assert.Equal(expected, PgSchemaSyntax.EscapeSqlString(input));
            Assert.Equal(expected, OracleSchemaSyntax.EscapeSqlString(input));
            Assert.Equal(expected, SqliteSchemaSyntax.EscapeSqlString(input));
            Assert.Equal(expected, MySqlSchemaSyntax.EscapeSqlString(input));
        }

        [Theory]
        [InlineData(@"ends with backslash \", @"ends with backslash \\")]
        [InlineData(@"a\' , (SELECT 1) , '", @"a\\'' , (SELECT 1) , ''")]
        [InlineData(@"C:\temp\file", @"C:\\temp\\file")]
        [DisplayName("MySQL also escapes backslashes (it treats \\ as an escape character by default)")]
        public void MySql_EscapesBackslash(string input, string expected)
        {
            Assert.Equal(expected, MySqlSchemaSyntax.EscapeSqlString(input));
        }

        [Theory]
        [InlineData(@"ends with backslash \")]
        [InlineData(@"C:\temp\file")]
        [DisplayName("The other dialects do not escape backslashes (an ordinary character there, so touching it changes the value)")]
        public void OtherDialects_LeaveBackslashAlone(string input)
        {
            // The opposite mistake is just as harmful: an extra backslash on these dialects would make the stored
            // description differ from the original text.
            Assert.Equal(input, SqlSchemaSyntax.EscapeSqlString(input));
            Assert.Equal(input, PgSchemaSyntax.EscapeSqlString(input));
            Assert.Equal(input, OracleSchemaSyntax.EscapeSqlString(input));
            Assert.Equal(input, SqliteSchemaSyntax.EscapeSqlString(input));
        }
    
        [DbTheory(DatabaseType.MySQL)]
        [InlineData(@"ends with backslash \")]
        [InlineData(@"a\' , (SELECT 1) , '")]
        [InlineData("O'Brien")]
        [DisplayName("MySQL creates a table whose field descriptions contain backslashes and quotes and reads them back unchanged")]
        public void MySql_ColumnComment_WithBackslashOrQuote_SurvivesRealDdl(string caption)
        {
            // The unit tests only check the string rules and cannot prove that
            // MySQL accepts the result; this sends it through real DDL.
            var dbAccess = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(DatabaseType.MySQL));
            string table = "tb_esc_" + Guid.NewGuid().ToString("N")[..8];
            string quoted = DatabaseType.MySQL.QuoteIdentifier(table);

            string comment = MySqlSchemaSyntax.EscapeSqlString(caption);
            dbAccess.ExecuteNonQuery(
                $"CREATE TABLE {quoted} (id INT NOT NULL COMMENT '{comment}')");
            try
            {
                var stored = Convert.ToString(dbAccess.ExecuteScalar(
                    "SELECT column_comment FROM information_schema.columns " +
                    $"WHERE table_schema = DATABASE() AND table_name = {{0}} AND column_name = 'id'",
                    table), CultureInfo.InvariantCulture);

                Assert.Equal(caption, stored);
            }
            finally
            {
                dbAccess.ExecuteNonQuery($"DROP TABLE {quoted}");
            }
        }
    }
}
