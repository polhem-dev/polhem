using System.ComponentModel;
using System.Data.Common;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions;
using Polhem.Repository.Abstractions.Form;
using Polhem.Repository.Factories;
using Polhem.Repository.Form;

using Polhem.Tests.Shared;
namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// The dependency guards of the <see cref="RepositoryFactory"/> constructor, the failure semantics when the
    /// progId axis resolves a faulty definition, and the delegation contract between the factory and
    /// <see cref="IRepositoryTypeResolver"/>. Dependencies are isolated with stubs, so no database is needed. The
    /// normal resolution paths of both axes are in <see cref="RepositoryFactoryTests"/>; the resolution of the
    /// registry binding itself is in <see cref="ProgramSettingsRepositoryTypeResolverTests"/>.
    /// </summary>
    public class RepositoryFactoryGuardTests
    {
        #region Stubs

        private sealed class StubDefineAccess : IDefineAccess
        {
            public string CategoryId { get; set; } = DbCategoryIds.Common;

            public DatabaseSettings GetDatabaseSettings() => new();
            public FormSchema GetFormSchema(string progId) => new() { CategoryId = CategoryId };
            public object GetDefine(DefineType defineType, string[]? keys = null) => throw new NotImplementedException();
            public void SaveDefine(DefineType defineType, object defineObject, string[]? keys = null) => throw new NotImplementedException();
            public SystemSettings GetSystemSettings() => throw new NotImplementedException();
            public void SaveSystemSettings(SystemSettings settings) => throw new NotImplementedException();
            public void SaveDatabaseSettings(DatabaseSettings settings) => throw new NotImplementedException();
            // This file does not test the registry binding, so it always reports "no ProgramSettings.xml". That is
            // what the real `IDefineAccess` does when the file does not exist, and the factory then falls back to
            // the framework default repository. The binding itself is tested in
            // `ProgramSettingsRepositoryTypeResolverTests`.
            public ProgramSettings GetProgramSettings() => throw new FileNotFoundException("ProgramSettings.xml");
            public void SaveProgramSettings(ProgramSettings settings) => throw new NotImplementedException();
            public DbCategorySettings GetDbCategorySettings() => throw new NotImplementedException();
            public void SaveDbCategorySettings(DbCategorySettings settings) => throw new NotImplementedException();
            public TableSchema GetTableSchema(string categoryId, string tableName) => throw new NotImplementedException();
            public void SaveTableSchema(string categoryId, TableSchema tableSchema) => throw new NotImplementedException();
            public void SaveFormSchema(FormSchema formSchema) => throw new NotImplementedException();
            public FormLayout GetFormLayout(string layoutId) => throw new NotImplementedException();
            public void SaveFormLayout(FormLayout formLayout) => throw new NotImplementedException();
            public LanguageResource GetLanguage(string lang, string ns) => throw new NotImplementedException();
            public void SaveLanguage(LanguageResource resource) => throw new NotImplementedException();
        }

        private sealed class StubDbAccessFactory : IDbAccessFactory
        {
            public DbAccess Create(string databaseId) => throw new NotImplementedException();
        }

        private sealed class StubConnectionManager : IDbConnectionManager
        {
            public DbConnectionInfo GetConnectionInfo(string databaseId) => throw new NotImplementedException();
            public DbConnection CreateConnection(string databaseId) => throw new NotImplementedException();
            public bool Remove(string databaseId) => false;
            public void Clear() { }
            public bool Contains(string databaseId) => false;
            public int Count => 0;
        }

        private sealed class StubRouter : IRepositoryDatabaseRouter
        {
            public string Resolve(DbScope scope, Guid accessToken) => DbCategoryIds.Common;
        }

        /// <summary>Always returns the given type and records the arguments the factory passed in.</summary>
        private sealed class StubTypeResolver(Type type) : IRepositoryTypeResolver
        {
            public Guid? ReceivedAccessToken { get; private set; }
            public string? ReceivedProgId { get; private set; }

            public Type Resolve(Guid accessToken, string progId)
            {
                ReceivedAccessToken = accessToken;
                ReceivedProgId = progId;
                return type;
            }
        }

        /// <summary>The custom repository bound by the resolver.</summary>
        public class BoundRepository : DataFormRepository
        {
            public BoundRepository(IRepositoryContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId)
            {
            }
        }

        /// <summary>Not derived from <see cref="DataFormRepository"/>; used to check the factory's contract validation.</summary>
        public class NotARepository
        {
        }

        private static RepositoryFactory CreateFactory(
            StubDefineAccess? defineAccess = null,
            IDbAccessFactory? dbAccessFactory = null,
            IDbConnectionManager? connectionManager = null,
            IRepositoryDatabaseRouter? router = null,
            IRepositoryTypeResolver? typeResolver = null)
        {
            var define = defineAccess ?? new StubDefineAccess();
            return new(
                TestRepositoryContext.CreateServices(),
                define,
                dbAccessFactory ?? new StubDbAccessFactory(),
                connectionManager ?? new StubConnectionManager(),
                router ?? new StubRouter(),
                typeResolver ?? new ProgramSettingsRepositoryTypeResolver(define));
        }

        private static StubTypeResolver DefaultResolver() => new(typeof(DataFormRepository));

        #endregion

        [Fact]
        [DisplayName("RepositoryFactory constructor throws ArgumentNullException for a null defineAccess")]
        public void RepositoryFactory_NullDefineAccess_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RepositoryFactory(
                TestRepositoryContext.CreateServices(), null!, new StubDbAccessFactory(),
                new StubConnectionManager(), new StubRouter(), DefaultResolver()));
        }

        [Fact]
        [DisplayName("RepositoryFactory constructor throws ArgumentNullException for a null dbAccessFactory")]
        public void RepositoryFactory_NullDbAccessFactory_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RepositoryFactory(
                TestRepositoryContext.CreateServices(), new StubDefineAccess(), null!,
                new StubConnectionManager(), new StubRouter(), DefaultResolver()));
        }

        [Fact]
        [DisplayName("RepositoryFactory constructor throws ArgumentNullException for a null connectionManager")]
        public void RepositoryFactory_NullConnectionManager_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RepositoryFactory(
                TestRepositoryContext.CreateServices(), new StubDefineAccess(), new StubDbAccessFactory(),
                null!, new StubRouter(), DefaultResolver()));
        }

        [Fact]
        [DisplayName("RepositoryFactory constructor throws ArgumentNullException for a null router")]
        public void RepositoryFactory_NullRouter_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RepositoryFactory(
                TestRepositoryContext.CreateServices(), new StubDefineAccess(), new StubDbAccessFactory(),
                new StubConnectionManager(), null!, DefaultResolver()));
        }

        [Fact]
        [DisplayName("RepositoryFactory constructor throws ArgumentNullException for a null typeResolver")]
        public void RepositoryFactory_NullTypeResolver_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RepositoryFactory(
                TestRepositoryContext.CreateServices(), new StubDefineAccess(), new StubDbAccessFactory(),
                new StubConnectionManager(), new StubRouter(), null!));
        }

        [Fact]
        [DisplayName("The factory builds an instance of the type the resolver returns and passes the progId")]
        public void CreateFormRepository_ResolverBindsCustomType_BuildsThatTypeWithProgId()
        {
            var factory = CreateFactory(typeResolver: new StubTypeResolver(typeof(BoundRepository)));

            var repository = factory.CreateFormRepository<IDataFormRepository>(Guid.NewGuid(), "Employee");

            var typed = Assert.IsType<BoundRepository>(repository);
            Assert.Equal("Employee", typed.ProgId);
        }

        [Fact]
        [DisplayName("The factory passes the caller's accessToken and progId to the resolver unchanged (tenant customization finds the session by token)")]
        public void CreateFormRepository_ForwardsAccessTokenAndProgIdToResolver()
        {
            // If the token does not reach the resolver, the customization ID cannot be read and every tenant
            // Repository override silently stops working, while every request still succeeds. None of the
            // default-path tests would notice.
            var resolver = DefaultResolver();
            var factory = CreateFactory(typeResolver: resolver);
            var token = Guid.NewGuid();

            factory.CreateFormRepository<IDataFormRepository>(token, "Employee");

            Assert.Equal(token, resolver.ReceivedAccessToken);
            Assert.Equal("Employee", resolver.ReceivedProgId);
        }

        [Fact]
        [DisplayName("When the resolver returns a type not derived from DataFormRepository, the factory throws naming the progId and the type")]
        public void CreateFormRepository_ResolverReturnsNonRepositoryType_ThrowsNamingProgIdAndType()
        {
            var factory = CreateFactory(typeResolver: new StubTypeResolver(typeof(NotARepository)));

            var ex = Assert.Throws<InvalidOperationException>(
                () => factory.CreateFormRepository<IDataFormRepository>(Guid.NewGuid(), "Employee"));

            Assert.Contains("Employee", ex.Message, StringComparison.Ordinal);
            Assert.Contains(typeof(NotARepository).FullName!, ex.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(DataFormRepository), ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("CreateFormRepository throws ArgumentException for a blank progId")]
        public void CreateFormRepository_WhitespaceProgId_ThrowsArgumentException()
        {
            var factory = CreateFactory();
            Assert.Throws<ArgumentException>(() => factory.CreateFormRepository<IDataFormRepository>(Guid.NewGuid(), "   "));
        }

        [Fact]
        [DisplayName("CreateFormRepository throws InvalidOperationException mentioning CategoryId when the schema has no CategoryId")]
        public void CreateFormRepository_EmptyCategoryId_ThrowsInvalidOperationException()
        {
            var stub = new StubDefineAccess { CategoryId = string.Empty };
            var factory = CreateFactory(defineAccess: stub);
            var ex = Assert.Throws<InvalidOperationException>(
                () => factory.CreateFormRepository<IDataFormRepository>(Guid.NewGuid(), "Employee"));
            Assert.Contains("CategoryId", ex.Message);
        }

        [Fact]
        [DisplayName("CreateFormRepository throws InvalidOperationException containing the unknown value for an unknown CategoryId")]
        public void CreateFormRepository_UnknownCategoryId_ThrowsInvalidOperationException()
        {
            var stub = new StubDefineAccess { CategoryId = "unknown_db" };
            var factory = CreateFactory(defineAccess: stub);
            var ex = Assert.Throws<InvalidOperationException>(
                () => factory.CreateFormRepository<IDataFormRepository>(Guid.NewGuid(), "Employee"));
            Assert.Contains("unknown_db", ex.Message);
        }

        [Theory]
        [InlineData(DbCategoryIds.Common)]
        [InlineData(DbCategoryIds.Company)]
        [InlineData(DbCategoryIds.Log)]
        [DisplayName("CreateFormRepository returns a DataFormRepository for a valid CategoryId")]
        public void CreateFormRepository_ValidCategoryId_ReturnsDataFormRepository(string categoryId)
        {
            var stub = new StubDefineAccess { CategoryId = categoryId };
            var factory = CreateFactory(defineAccess: stub);
            var repo = factory.CreateFormRepository<IDataFormRepository>(Guid.NewGuid(), "Employee");
            Assert.IsType<DataFormRepository>(repo);
        }
    }
}
