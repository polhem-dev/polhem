using Polhem.Business.Form;
using Polhem.Business.UnitTests.Fakes;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Logging;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Repository.Form;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// Per-test wiring shared by the new <c>FormBusinessObject</c> CRUD tests
    /// (<c>GetNewData</c> / <c>GetData</c> / <c>Save</c> / <c>Delete</c>).
    /// Binds the BO to a <see cref="DataFormRepository"/> constructed against
    /// the test-specific <c>{categoryId}_{dbtype}</c> databaseId, mirroring
    /// the GetList tests' pattern.
    /// </summary>
    internal sealed class CrudTestContext
    {
        public const string CategoryId = "company";
        public const string ProgId = "Employee";
        public const string CompanyId = "CRUDTEST";
        public const string CompanyName = "CRUD test company";
        public const string UserId = "crud_test";

        private readonly SharedDbFixture _fx;
        private readonly string _databaseId;
        private readonly IDataFormRepository _repository;

        public CrudTestContext(SharedDbFixture fx, DatabaseType dbType)
        {
            _fx = fx;
            DbType = dbType;
            _databaseId = TestDbConventions.GetDatabaseId(dbType, CategoryId);
            DbAccess = fx.NewDbAccess(_databaseId);

            var defineAccess = fx.GetRequiredService<IDefineAccess>();
            EmployeeSchema = defineAccess.GetFormSchema(ProgId);

            _repository = new DataFormRepository(TestRepositoryContext.Create(fx.GetRequiredService<IDbConnectionManager>(), defineAccess: defineAccess, dbAccessFactory: fx.GetRequiredService<IDbAccessFactory>()), ProgId, EmployeeSchema, _databaseId);
        }

        public DatabaseType DbType { get; }
        public DbAccess DbAccess { get; }
        public FormSchema EmployeeSchema { get; }
        public IDataFormRepository Repository => _repository;

        /// <summary>
        /// Plants a session bound to <see cref="CompanyId"/> in the fixture's session cache and returns its token.
        /// </summary>
        /// <remarks>
        /// A bare <see cref="Guid.NewGuid"/> token is not in the cache, so the BO would rebuild it from
        /// <c>st_session</c> in <c>common</c>, which is always SQL Server. A test gated on SQLite or Oracle would then
        /// need SQL Server without saying so, and it would only ever exercise the no-session path.
        /// </remarks>
        public Guid CreateSessionToken()
        {
            var accessToken = Guid.NewGuid();
            _fx.GetRequiredService<ISessionInfoService>().Set(new SessionInfo
            {
                AccessToken = accessToken,
                UserId = UserId,
                UserName = UserId,
                CompanyId = CompanyId,
                ExpiredAt = DateTime.UtcNow.AddHours(1),
                ApiEncryptionKey = [],
            });
            return accessToken;
        }

        /// <summary>
        /// Builds a business object bound to the test repository, running under a planted company session.
        /// </summary>
        /// <param name="pluginResolver">
        /// Optional plugin chain resolver. Supplied by the plugin integration tests to bind a
        /// chain without writing a customization definition file; omitted elsewhere, in which case
        /// the fixture's own resolver applies and no plugin is bound.
        /// </param>
        public FormBusinessObject CreateBo(IFormPluginResolver? pluginResolver = null)
            => CreateBoWithSession(CreateSessionToken(), pluginResolver);

        /// <summary>
        /// Builds a business object bound to the test repository, with additional service
        /// overrides layered on top. Used by the audit tests to enable
        /// <c>AuditLogOptions</c> and capture what <c>IAuditLogWriter</c> receives.
        /// </summary>
        public FormBusinessObject CreateBoWithOverrides(params (Type ServiceType, object? Instance)[] overrides)
            => CreateBoWithSession(CreateSessionToken(), null, overrides);

        /// <summary>
        /// Builds the context <see cref="CreateBoWithOverrides"/> uses, for tests that construct a
        /// <see cref="FormBusinessObject"/> subclass of their own.
        /// </summary>
        /// <remarks>
        /// Besides the repository stub, it stubs <see cref="ICompanyInfoService"/> (knowing only
        /// <see cref="CompanyId"/>) and <see cref="IAuditRuleService"/> (no rules). Both real services read
        /// <c>common</c>, which is SQL Server whatever provider the test targets. Caller overrides win.
        /// </remarks>
        public IBusinessObjectContext CreateContextWithOverrides(params (Type ServiceType, object? Instance)[] overrides)
        {
            var all = new List<(Type, object?)>
            {
                (typeof(IRepositoryFactory), new StubFactory(_repository)),
                (typeof(ICompanyInfoService), new StubCompanyInfoService(
                    new CompanyInfo { CompanyId = CompanyId, CompanyName = CompanyName })),
                (typeof(IAuditRuleService), new NoAuditRuleService()),
            };
            all.AddRange(overrides);
            return TestBusinessObjectContext.CreateWithOverrides(_fx, [.. all]);
        }

        /// <summary>
        /// Builds a business object bound to the test repository under a caller-supplied access
        /// token, optionally with a plugin chain and extra service overrides.
        /// </summary>
        /// <param name="accessToken">The token the BO runs under, usually from <see cref="CreateSessionToken"/>.</param>
        /// <param name="pluginResolver">Optional plugin chain resolver.</param>
        /// <param name="overrides">Service overrides layered on the fixture's provider.</param>
        public FormBusinessObject CreateBoWithSession(
            Guid accessToken,
            IFormPluginResolver? pluginResolver,
            params (Type ServiceType, object? Instance)[] overrides)
        {
            var all = new List<(Type, object?)>();
            if (pluginResolver != null)
            {
                all.Add((typeof(IFormPluginResolver), pluginResolver));
            }
            all.AddRange(overrides);
            return new FormBusinessObject(CreateContextWithOverrides([.. all]), accessToken, ProgId);
        }

        private sealed class StubFactory : IRepositoryFactory
        {
            private readonly IDataFormRepository _repository;
            public StubFactory(IDataFormRepository repository) => _repository = repository;
            public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository => (T)_repository;
            public T Create<T>(Guid accessToken = default) where T : class
                => throw new NotSupportedException();
        }
    }
}
