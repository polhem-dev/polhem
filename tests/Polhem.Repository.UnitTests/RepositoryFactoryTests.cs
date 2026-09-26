using System.ComponentModel;
using Polhem.Repository.Abstractions;
using Polhem.Repository.Abstractions.AuditLog;
using Polhem.Repository.Abstractions.Form;
using Polhem.Repository.Abstractions.System;
using Polhem.Repository.Factories;
using Polhem.Repository.Form;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// The two axes of <see cref="RepositoryFactory"/>: the progId axis (the type varies with the progId) and the
    /// framework axis (the type is fixed and named by its interface).
    /// </summary>
    public class RepositoryFactoryTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public RepositoryFactoryTests(SharedDbFixture fx) { _fx = fx; }

        private RepositoryFactory CreateFactory()
        {
            var defineAccess = _fx.GetRequiredService<Definition.Storage.IDefineAccess>();
            // No customization reader and no session: only the base registry is consulted, and an uncached token
            // never causes a read of `st_session`.
            return new(
                _fx.Provider,
                defineAccess,
                _fx.GetRequiredService<Db.IDbAccessFactory>(),
                _fx.GetRequiredService<Db.Manager.IDbConnectionManager>(),
                new StubRouter(),
                new ProgramSettingsRepositoryTypeResolver(defineAccess));
        }

        /// <summary>
        /// Keeps the production router's fixed rules for Common and Log, and returns a test ID for Company.
        /// </summary>
        /// <remarks>
        /// Every FormSchema under tests/Define is company scope, and the production router can only resolve it with a
        /// session that has entered a company. This file tests the factory itself (type mapping, contract checks,
        /// scope declarations); session routing is covered by the RepositoryDatabaseRouter tests.
        /// </remarks>
        private sealed class StubRouter : IRepositoryDatabaseRouter
        {
            public string Resolve(Definition.DbScope scope, Guid accessToken) => scope switch
            {
                Definition.DbScope.Common => Definition.Database.DbCategoryIds.Common,
                Definition.DbScope.Log => Definition.Database.DbCategoryIds.Log,
                _ => "testdb",
            };
        }

        // ---- Framework axis ----

        [Theory]
        [InlineData(typeof(ISessionRepository))]
        [InlineData(typeof(ICompanyRepository))]
        [InlineData(typeof(IUserCompanyRepository))]
        [InlineData(typeof(IUserRepository))]
        [InlineData(typeof(IApiKeyRepository))]
        [InlineData(typeof(IDatabaseRepository))]
        [InlineData(typeof(IRolePermissionRepository))]
        [InlineData(typeof(IDepartmentRepository))]
        [InlineData(typeof(IEmployeeRepository))]
        [InlineData(typeof(IAuditLogRepository))]
        [InlineData(typeof(IAuditLogWriteRepository))]
        [DisplayName("Create<T> builds every framework repository")]
        public void Create_EveryFrameworkRepository_Resolves(Type contract)
        {
            var factory = CreateFactory();
            var method = typeof(RepositoryFactory).GetMethod(nameof(RepositoryFactory.Create))!
                .MakeGenericMethod(contract);

            var repository = method.Invoke(factory, [Guid.Empty]);

            Assert.NotNull(repository);
            Assert.IsType(contract, repository, exactMatch: false);
        }

        [Fact]
        [DisplayName("Create<T> throws NotSupportedException naming the interface for an unregistered interface")]
        public void Create_UnregisteredContract_Throws()
        {
            var factory = CreateFactory();

            var ex = Assert.Throws<NotSupportedException>(() => factory.Create<IDisposable>());

            Assert.Contains(nameof(IDisposable), ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Each Create<T> returns a new instance (a repository carries per-call state and must not be shared)")]
        public void Create_ReturnsNewInstanceEachTime()
        {
            var factory = CreateFactory();

            Assert.NotSame(factory.Create<ISessionRepository>(), factory.Create<ISessionRepository>());
        }

        [Fact]
        [DisplayName("A repository that declares the Common scope resolves to the common database")]
        public void Create_CommonScopeRepository_RoutesToCommon()
        {
            var repository = (RepositoryBase)CreateFactory().Create<ISessionRepository>();

            Assert.Equal(Definition.Database.DbCategoryIds.Common, GetDatabaseId(repository));
        }

        [Fact]
        [DisplayName("A repository whose methods each take a databaseId does not resolve routing at construction")]
        public void Create_CallerRoutedRepository_ResolvesNoDatabase()
        {
            // The callers of these repositories are the cache providers and the session bootstrap. They are told which
            // company to read and hold no token. Resolving from the session at construction would read the caller's
            // company instead of the one requested.
            foreach (var repository in new RepositoryBase[]
            {
                (RepositoryBase)CreateFactory().Create<IRolePermissionRepository>(),
                (RepositoryBase)CreateFactory().Create<IDepartmentRepository>(),
                (RepositoryBase)CreateFactory().Create<IEmployeeRepository>(),
            })
            {
                Assert.Equal(string.Empty, GetDatabaseId(repository));
            }
        }

        [Fact]
        [DisplayName("Creating a Common scope repository without a session does not throw")]
        public void Create_CommonScopeWithoutSession_DoesNotThrow()
        {
            var factory = CreateFactory();

            var exception = Record.Exception(() => factory.Create<IUserRepository>(Guid.Empty));

            Assert.Null(exception);
        }

        // ---- progId axis ----

        [Fact]
        [DisplayName("CreateFormRepository returns a repository bound to the progId")]
        public void CreateFormRepository_ReturnsRepositoryBoundToProgId()
        {
            var repository = CreateFactory()
                .CreateFormRepository<IDataFormRepository>(Guid.Empty, "Employee");

            var typed = Assert.IsType<DataFormRepository>(repository);
            Assert.Equal("Employee", typed.ProgId);
        }

        [Fact]
        [DisplayName("CreateFormRepository throws naming both when the resolved type does not implement the requested interface")]
        public void CreateFormRepository_UnimplementedContract_Throws()
        {
            var factory = CreateFactory();

            var ex = Assert.Throws<InvalidOperationException>(
                () => factory.CreateFormRepository<IUnimplementedFormRepository>(Guid.Empty, "Employee"));

            Assert.Contains("Employee", ex.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(IUnimplementedFormRepository), ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [DisplayName("CreateFormRepository throws ArgumentException for an empty progId")]
        public void CreateFormRepository_BlankProgId_Throws(string? progId)
        {
            var factory = CreateFactory();

            Assert.ThrowsAny<ArgumentException>(
                () => factory.CreateFormRepository<IDataFormRepository>(Guid.Empty, progId!));
        }

        /// <summary>An interface no repository implements, used to check the error message for a type mismatch.</summary>
        public interface IUnimplementedFormRepository : IDataFormRepository { }

        private static string GetDatabaseId(RepositoryBase repository)
            => (string)typeof(RepositoryBase)
                .GetProperty("DatabaseId", global::System.Reflection.BindingFlags.NonPublic | global::System.Reflection.BindingFlags.Instance)!
                .GetValue(repository)!;
    }
}
