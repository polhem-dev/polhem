using System.ComponentModel;
using System.Data;
using System.Data.Common;

namespace Polhem.Db.UnitTests
{
    public class DbConnectionScopeTests
    {
        [Fact]
        [DisplayName("Create throws ArgumentNullException when externalConnection and factory are both null")]
        public void Create_NullFactoryAndNullExternal_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                DbConnectionScope.Create(null, null!, "irrelevant"));
        }

        [Fact]
        [DisplayName("CreateAsync throws ArgumentNullException when externalConnection and factory are both null")]
        public async Task CreateAsync_NullFactoryAndNullExternal_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await DbConnectionScope.CreateAsync(null, null!, "irrelevant"));
        }

        [Fact]
        [DisplayName("Create does not reopen an open external connection, and Dispose does not close it")]
        public void Create_ExternalOpenConnection_NotReopenedNotClosed()
        {
            var fake = new FakeDbConnection { CurrentState = ConnectionState.Open };

            using (var scope = DbConnectionScope.Create(fake, null!, "ignored"))
            {
                Assert.Same(fake, scope.Connection);
                Assert.Equal(0, fake.OpenCount);
            }

            Assert.Equal(0, fake.DisposeCount);
            Assert.Equal(ConnectionState.Open, fake.State);
        }

        [Fact]
        [DisplayName("Create opens a closed external connection, but Dispose does not close it")]
        public void Create_ExternalClosedConnection_OpenedButNotDisposed()
        {
            var fake = new FakeDbConnection { CurrentState = ConnectionState.Closed };

            using (var scope = DbConnectionScope.Create(fake, null!, "ignored"))
            {
                Assert.Same(fake, scope.Connection);
                Assert.Equal(1, fake.OpenCount);
            }

            Assert.Equal(0, fake.DisposeCount);
        }

        [Fact]
        [DisplayName("CreateAsync calls OpenAsync on a closed external connection")]
        public async Task CreateAsync_ExternalClosedConnection_OpensAsync()
        {
            var fake = new FakeDbConnection { CurrentState = ConnectionState.Closed };

            using (var scope = await DbConnectionScope.CreateAsync(fake, null!, "ignored"))
            {
                Assert.Same(fake, scope.Connection);
                Assert.Equal(1, fake.OpenAsyncCount);
            }

            Assert.Equal(0, fake.DisposeCount);
        }

        [Fact]
        [DisplayName("Create without an external connection creates and opens a new connection through the factory and closes it on Dispose")]
        public void Create_NoExternal_CreatesAndOwnsConnection()
        {
            var fake = new FakeDbConnection { CurrentState = ConnectionState.Closed };
            var factory = new FakeDbProviderFactory(fake);

            using (var scope = DbConnectionScope.Create(null, factory, "conn-str"))
            {
                Assert.Same(fake, scope.Connection);
                Assert.Equal(1, fake.OpenCount);
                Assert.Equal("conn-str", fake.ConnectionString);
            }

            Assert.Equal(1, fake.DisposeCount);
        }

        [Fact]
        [DisplayName("CreateAsync without an external connection creates a new connection through the factory, opens it with OpenAsync and closes it on Dispose")]
        public async Task CreateAsync_NoExternal_CreatesAndOwnsConnection()
        {
            var fake = new FakeDbConnection { CurrentState = ConnectionState.Closed };
            var factory = new FakeDbProviderFactory(fake);

            using (var scope = await DbConnectionScope.CreateAsync(null, factory, "conn-str"))
            {
                Assert.Same(fake, scope.Connection);
                Assert.Equal(1, fake.OpenAsyncCount);
                Assert.Equal("conn-str", fake.ConnectionString);
            }

            Assert.Equal(1, fake.DisposeCount);
        }

        [Fact]
        [DisplayName("Create throws InvalidOperationException when factory.CreateConnection returns null")]
        public void Create_FactoryReturnsNull_ThrowsInvalidOperation()
        {
            var factory = new FakeDbProviderFactory(null);

            Assert.Throws<InvalidOperationException>(() =>
                DbConnectionScope.Create(null, factory, "irrelevant"));
        }

        [Fact]
        [DisplayName("Create disposes the new connection and rethrows when Open fails")]
        public void Create_OpenThrows_DisposesAndRethrows()
        {
            var fake = new FakeDbConnection
            {
                CurrentState = ConnectionState.Closed,
                ThrowOnOpen = true
            };
            var factory = new FakeDbProviderFactory(fake);

            Assert.Throws<InvalidOperationException>(() =>
                DbConnectionScope.Create(null, factory, "conn-str"));

            Assert.Equal(1, fake.DisposeCount);
        }

        [Fact]
        [DisplayName("CreateAsync disposes the new connection and rethrows when OpenAsync fails")]
        public async Task CreateAsync_OpenAsyncThrows_DisposesAndRethrows()
        {
            var fake = new FakeDbConnection
            {
                CurrentState = ConnectionState.Closed,
                ThrowOnOpenAsync = true
            };
            var factory = new FakeDbProviderFactory(fake);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await DbConnectionScope.CreateAsync(null, factory, "conn-str"));

            Assert.Equal(1, fake.DisposeCount);
        }

        private sealed class FakeDbProviderFactory : DbProviderFactory
        {
            private readonly DbConnection? _connection;
            public FakeDbProviderFactory(DbConnection? connection) => _connection = connection;
            public override DbConnection? CreateConnection() => _connection;
        }

        // A minimal `DbConnection` implementation that simulates State and the Open/Dispose behavior.
        private sealed class FakeDbConnection : DbConnection
        {
            public ConnectionState CurrentState { get; set; } = ConnectionState.Closed;
            public int OpenCount { get; private set; }
            public int OpenAsyncCount { get; private set; }
            public int DisposeCount { get; private set; }
            public bool ThrowOnOpen { get; set; }
            public bool ThrowOnOpenAsync { get; set; }

            [System.Diagnostics.CodeAnalysis.AllowNull]
            public override string ConnectionString { get; set; } = string.Empty;
            public override string Database => string.Empty;
            public override string DataSource => string.Empty;
            public override string ServerVersion => "0.0";
            public override ConnectionState State => CurrentState;

            public override void ChangeDatabase(string databaseName) { }
            public override void Close() => CurrentState = ConnectionState.Closed;
            public override void Open()
            {
                OpenCount++;
                if (ThrowOnOpen) throw new InvalidOperationException("fake-open-failure");
                CurrentState = ConnectionState.Open;
            }

            public override Task OpenAsync(System.Threading.CancellationToken cancellationToken)
            {
                OpenAsyncCount++;
                if (ThrowOnOpenAsync) throw new InvalidOperationException("fake-open-async-failure");
                CurrentState = ConnectionState.Open;
                return Task.CompletedTask;
            }

            protected override DbCommand CreateDbCommand() => throw new NotSupportedException();

            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
                => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing) DisposeCount++;
                base.Dispose(disposing);
            }
        }
    }
}
