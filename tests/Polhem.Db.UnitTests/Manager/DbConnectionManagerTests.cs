using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Settings;
using Polhem.Definition.Database;
using Polhem.Db.Manager;

namespace Polhem.Db.UnitTests.Manager
{
    /// <summary>
    /// Tests for the cache and the connection info assembly of <c>DbConnectionManager</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class carries its own isolated <see cref="DatabaseSettings"/> (see
    /// <see cref="IsolatedDatabaseSettingsProvider"/>) and <b>does not touch the process-wide definition cache</b>.
    /// It and <c>DbAccessFactoryTests</c> used to call <c>Items.Add/Remove</c> on the cached instance, which was
    /// measured to throw <c>ArgumentOutOfRangeException</c> under parallel execution.
    /// </para>
    /// <para>
    /// What is tested here is connection string assembly, which never needed a database, so
    /// <c>SharedDbFixture</c> was removed as well.
    /// </para>
    /// </remarks>
    public sealed class DbConnectionManagerTests : IDisposable
    {
        private readonly IsolatedDatabaseSettingsProvider _provider = new();
        private readonly DbConnectionManagerService _manager;

        public DbConnectionManagerTests()
        {
            TestDbProviders.EnsureSqlServerRegistered();
            _manager = new DbConnectionManagerService(_provider);
        }

        /// <summary>
        /// Unsubscribes from <c>GlobalEvents.DatabaseSettingsChanged</c>. A static event holds on to its subscribers,
        /// so if each test class left a live subscriber, the next test's event would clear it.
        /// </summary>
        public void Dispose() => _manager.Dispose();

        private static string NewId(string label) => $"polhem_dcm_{label}_{Guid.NewGuid():N}";

        private DatabaseItem AddItem(string id, Action<DatabaseItem> configure)
        {
            var item = new DatabaseItem { Id = id, DatabaseType = DatabaseType.SQLServer };
            configure(item);
            _provider.Settings.Items!.Add(item);
            return item;
        }

        private void RemoveItem(string id)
        {
            if (_provider.Settings.Items!.Contains(id))
                _provider.Settings.Items!.Remove(_provider.Settings.Items[id]!);
            _manager.Remove(id);
        }

        private DatabaseServer AddServer(string id, Action<DatabaseServer> configure)
        {
            var server = new DatabaseServer { Id = id, DatabaseType = DatabaseType.SQLServer };
            configure(server);
            _provider.Settings.Servers!.Add(server);
            return server;
        }

        private void RemoveServer(string id)
        {
            if (_provider.Settings.Servers!.Contains(id))
                _provider.Settings.Servers!.Remove(_provider.Settings.Servers[id]!);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("GetConnectionInfo throws ArgumentNullException for a blank databaseId")]
        public void GetConnectionInfo_EmptyId_ThrowsArgumentNullException(string? id)
        {
            Assert.Throws<ArgumentNullException>(() => _manager.GetConnectionInfo(id!));
        }

        [Fact]
        [DisplayName("GetConnectionInfo throws KeyNotFoundException for an undefined databaseId")]
        public void GetConnectionInfo_UnknownId_ThrowsKeyNotFoundException()
        {
            // The `KeyedCollection` indexer throws `KeyNotFoundException` itself when the key is missing, so the
            // null check in the source is never actually reached.
            var id = NewId("unknown");
            Assert.Throws<KeyNotFoundException>(() => _manager.GetConnectionInfo(id));
        }

        [Fact]
        [DisplayName("GetConnectionInfo throws InvalidOperationException for an empty connection string")]
        public void GetConnectionInfo_EmptyConnectionString_ThrowsInvalidOperationException()
        {
            var id = NewId("emptyconn");
            AddItem(id, i => i.ConnectionString = string.Empty);
            try
            {
                Assert.Throws<InvalidOperationException>(() => _manager.GetConnectionInfo(id));
            }
            finally
            {
                RemoveItem(id);
            }
        }

        [Fact]
        [DisplayName("GetConnectionInfo replaces the {@DbName}/{@UserId}/{@Password} placeholders")]
        public void GetConnectionInfo_ReplacesAllPlaceholders()
        {
            var id = NewId("placeholder");
            AddItem(id, i =>
            {
                i.ConnectionString = "Server=x;Database={@DbName};User Id={@UserId};Password={@Password};";
                i.DbName = "db_v";
                i.UserId = "user_v";
                i.Password = "pwd_v";
            });
            try
            {
                var info = _manager.GetConnectionInfo(id);
                Assert.Contains("db_v", info.ConnectionString);
                Assert.Contains("user_v", info.ConnectionString);
                Assert.Contains("pwd_v", info.ConnectionString);
                Assert.DoesNotContain("{@DbName}", info.ConnectionString);
                Assert.DoesNotContain("{@UserId}", info.ConnectionString);
                Assert.DoesNotContain("{@Password}", info.ConnectionString);
            }
            finally
            {
                RemoveItem(id);
            }
        }

        [Fact]
        [DisplayName("GetConnectionInfo keeps a password containing ; as one value instead of adding connection options")]
        public void GetConnectionInfo_PasswordWithSemicolon_StaysOneValue()
        {
            const string password = "pa;ss;Integrated Security=true";
            var id = NewId("semicolon");
            AddItem(id, i =>
            {
                i.ConnectionString = "Server=x;Database={@DbName};User Id={@UserId};Password={@Password};";
                i.DbName = "db_v";
                i.UserId = "user_v";
                i.Password = password;
            });
            try
            {
                var info = _manager.GetConnectionInfo(id);

                var parsed = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = info.ConnectionString };
                Assert.Equal(password, parsed["Password"]);
                Assert.False(parsed.ContainsKey("Integrated Security"));
            }
            finally
            {
                RemoveItem(id);
            }
        }

        [Fact]
        [DisplayName("GetConnectionInfo throws KeyNotFoundException for a ServerId that does not exist")]
        public void GetConnectionInfo_ServerIdNotFound_ThrowsKeyNotFoundException()
        {
            // The `Servers` indexer behaves like a `KeyedCollection` too: an unregistered ServerId throws
            // `KeyNotFoundException` directly.
            var id = NewId("missingserver");
            AddItem(id, i =>
            {
                i.ServerId = "non_existent_server_" + Guid.NewGuid().ToString("N");
                i.ConnectionString = "Server=x;";
            });
            try
            {
                Assert.Throws<KeyNotFoundException>(() => _manager.GetConnectionInfo(id));
            }
            finally
            {
                RemoveItem(id);
            }
        }

        [Fact]
        [DisplayName("GetConnectionInfo through a ServerId uses the server's connection string and DatabaseType")]
        public void GetConnectionInfo_ServerId_UsesServerSettings()
        {
            var serverId = NewId("svr");
            var itemId = NewId("itemref");
            AddServer(serverId, s =>
            {
                s.ConnectionString = "Server=srv_host;UserId={@UserId};";
                s.DatabaseType = DatabaseType.SQLServer;
                s.UserId = "srv_user";
                s.Password = "srv_pwd";
            });
            AddItem(itemId, i =>
            {
                i.ServerId = serverId;
                // The ConnectionString does not matter, because the server overrides it.
                i.ConnectionString = "ignored";
            });
            try
            {
                var info = _manager.GetConnectionInfo(itemId);
                Assert.Contains("srv_host", info.ConnectionString);
                Assert.Contains("srv_user", info.ConnectionString);
                Assert.Equal(DatabaseType.SQLServer, info.DatabaseType);
            }
            finally
            {
                RemoveItem(itemId);
                RemoveServer(serverId);
            }
        }

        [Fact]
        [DisplayName("GetConnectionInfo in ServerId mode lets the DatabaseItem's UserId and Password override the server's")]
        public void GetConnectionInfo_ServerId_ItemOverridesServerUserPassword()
        {
            var serverId = NewId("svr2");
            var itemId = NewId("override");
            AddServer(serverId, s =>
            {
                s.ConnectionString = "Server=x;User Id={@UserId};Password={@Password};";
                s.UserId = "srv_user";
                s.Password = "srv_pwd";
            });
            AddItem(itemId, i =>
            {
                i.ServerId = serverId;
                i.UserId = "item_user";
                i.Password = "item_pwd";
            });
            try
            {
                var info = _manager.GetConnectionInfo(itemId);
                Assert.Contains("item_user", info.ConnectionString);
                Assert.Contains("item_pwd", info.ConnectionString);
                Assert.DoesNotContain("srv_user", info.ConnectionString);
                Assert.DoesNotContain("srv_pwd", info.ConnectionString);
            }
            finally
            {
                RemoveItem(itemId);
                RemoveServer(serverId);
            }
        }

        [Fact]
        [DisplayName("GetConnectionInfo returns the same cached instance for repeated calls with the same databaseId")]
        public void GetConnectionInfo_RepeatedCall_ReturnsCachedInstance()
        {
            var id = NewId("cache");
            AddItem(id, i => i.ConnectionString = "Server=abc;");
            try
            {
                var first = _manager.GetConnectionInfo(id);
                var second = _manager.GetConnectionInfo(id);
                Assert.Same(first, second);
                Assert.True(_manager.Contains(id));
            }
            finally
            {
                RemoveItem(id);
            }
        }

        [Fact]
        [DisplayName("Remove of a cached entry returns true and Contains no longer finds it")]
        public void Remove_CachedItem_RemovesFromCache()
        {
            var id = NewId("remove");
            AddItem(id, i => i.ConnectionString = "Server=abc;");
            try
            {
                _manager.GetConnectionInfo(id);
                Assert.True(_manager.Contains(id));

                var removed = _manager.Remove(id);

                Assert.True(removed);
                Assert.False(_manager.Contains(id));
            }
            finally
            {
                RemoveItem(id);
            }
        }

        [Fact]
        [DisplayName("Remove of an uncached entry returns false")]
        public void Remove_NotCachedItem_ReturnsFalse()
        {
            var id = NewId("notcached");
            Assert.False(_manager.Remove(id));
        }

        [Fact]
        [DisplayName("Clear removes every cached entry")]
        public void Clear_EmptiesAllCachedEntries()
        {
            var id1 = NewId("clr1");
            var id2 = NewId("clr2");
            AddItem(id1, i => i.ConnectionString = "Server=a;");
            AddItem(id2, i => i.ConnectionString = "Server=b;");
            try
            {
                _manager.GetConnectionInfo(id1);
                _manager.GetConnectionInfo(id2);

                _manager.Clear();

                Assert.False(_manager.Contains(id1));
                Assert.False(_manager.Contains(id2));
                Assert.Equal(0, _manager.Count);
            }
            finally
            {
                RemoveItem(id1);
                RemoveItem(id2);
            }
        }

        [Fact]
        [DisplayName("The DatabaseSettingsChanged event clears the cache")]
        public void RaiseDatabaseSettingsChanged_ClearsCache()
        {
            var id = NewId("event");
            AddItem(id, i => i.ConnectionString = "Server=a;");
            try
            {
                _manager.GetConnectionInfo(id);
                Assert.True(_manager.Contains(id));

                GlobalEvents.RaiseDatabaseSettingsChanged();

                Assert.False(_manager.Contains(id));
            }
            finally
            {
                RemoveItem(id);
            }
        }
    }
}
