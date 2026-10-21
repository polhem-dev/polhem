using System.ComponentModel;

namespace Polhem.DbManager.UnitTests
{
    /// <summary>
    /// Which SQLite connection strings the tool refuses as relative.
    /// </summary>
    public class SqlitePathCheckTests
    {
        [Theory]
        [DisplayName("A relative file path is reported, whether plain or as a file: name")]
        [InlineData("Data Source=northwind.db;Cache=Shared", "northwind.db")]
        [InlineData("Data Source=data/app.db", "data/app.db")]
        [InlineData("Data Source=file:app.db?cache=shared", "app.db")]
        public void RelativePath_Relative_IsReported(string connectionString, string expected)
        {
            Assert.Equal(expected, SqlitePathCheck.RelativePath(connectionString));
        }

        [Theory]
        [DisplayName("An absolute path and every in-memory form are accepted")]
        [InlineData("Data Source=:memory:")]
        [InlineData("Data Source=shared;Mode=Memory;Cache=Shared")]
        [InlineData("Data Source=file:memdb1?mode=memory&cache=shared")]
        [InlineData("Data Source=file:///var/data/app.db")]
        public void RelativePath_AbsoluteOrMemory_IsAccepted(string connectionString)
        {
            Assert.Null(SqlitePathCheck.RelativePath(connectionString));
        }

        [Fact]
        [DisplayName("An absolute file path is accepted")]
        public void RelativePath_AbsolutePath_IsAccepted()
        {
            var path = Path.Combine(Path.GetTempPath(), "app.db");

            Assert.Null(SqlitePathCheck.RelativePath($"Data Source={path}"));
        }
    }
}
