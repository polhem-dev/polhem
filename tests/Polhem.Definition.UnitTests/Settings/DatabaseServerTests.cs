using System.ComponentModel;
using Polhem.Definition.Settings;
using Polhem.Definition.Database;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// Tests for the DatabaseServer data class.
    /// </summary>
    public class DatabaseServerTests
    {
        [Fact]
        [DisplayName("DatabaseServer defaults to empty strings and SQLServer")]
        public void DatabaseServer_Default_HasExpectedDefaults()
        {
            var server = new DatabaseServer();

            Assert.Equal(string.Empty, server.Id);
            Assert.Equal(string.Empty, server.DisplayName);
            Assert.Equal(DatabaseType.SQLServer, server.DatabaseType);
            Assert.Equal(string.Empty, server.ConnectionString);
            Assert.Equal(string.Empty, server.UserId);
            Assert.Equal(string.Empty, server.Password);
        }

        [Fact]
        [DisplayName("DatabaseServer.Id maps to Key")]
        public void DatabaseServer_Id_MapsToKey()
        {
            var server = new DatabaseServer { Id = "main" };

            Assert.Equal("main", server.Key);
            Assert.Equal("main", server.Id);
        }

        [Fact]
        [DisplayName("DatabaseServer.Clone produces an independent equal copy")]
        public void DatabaseServer_Clone_ProducesEqualCopy()
        {
            var server = new DatabaseServer
            {
                Id = "main",
                DisplayName = "主資料庫",
                DatabaseType = DatabaseType.SQLServer,
                ConnectionString = "Server=.;Database=Test;",
                UserId = "sa",
                Password = "pw"
            };

            var clone = server.Clone();

            Assert.NotSame(server, clone);
            Assert.Equal(server.Id, clone.Id);
            Assert.Equal(server.DisplayName, clone.DisplayName);
            Assert.Equal(server.DatabaseType, clone.DatabaseType);
            Assert.Equal(server.ConnectionString, clone.ConnectionString);
            Assert.Equal(server.UserId, clone.UserId);
            Assert.Equal(server.Password, clone.Password);
        }

        [Fact]
        [DisplayName("DatabaseServer.ToString returns 'Id - DisplayName'")]
        public void DatabaseServer_ToString_ReturnsFormatted()
        {
            var server = new DatabaseServer
            {
                Id = "main",
                DisplayName = "主資料庫"
            };

            Assert.Equal("main - 主資料庫", server.ToString());
        }
    }
}
