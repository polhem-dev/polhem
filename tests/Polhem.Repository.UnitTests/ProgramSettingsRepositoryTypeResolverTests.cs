using System.ComponentModel;
using Polhem.Base;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Repository.Factories;
using Polhem.Repository.Form;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// <see cref="ProgramSettingsRepositoryTypeResolver"/>: the resolution paths of <c>ProgramItem.Repository</c>
    /// (set, empty, type cannot be loaded, type not derived) and the replacement behavior of the tenant customization
    /// overlay.
    /// </summary>
    /// <remarks>
    /// These tests used to target the factory (<c>ProgramItemRepositoryBindingTests</c>), which meant preparing
    /// stubs for database access, connections and routing that were never called, just to build the factory. Once
    /// resolution was extracted into its own type, the tests target it directly. The exception message assertions
    /// were carried over unchanged; the type assertions changed from "the built instance is of some type" to "the
    /// resolved type is some type", because the subject now returns a <see cref="Type"/>. The other half, where the
    /// factory builds an instance from the resolved type and passes the progId, is in
    /// <see cref="RepositoryFactoryGuardTests"/>.
    /// </remarks>
    public class ProgramSettingsRepositoryTypeResolverTests
    {
        private const string ProgId = "Employee";
        private const string CustomizeId = "acme";

        #region Stubs and test repositories

        /// <summary>The custom repository resolved when the binding succeeds.</summary>
        public class CustomEmployeeRepository : DataFormRepository
        {
            public CustomEmployeeRepository(IRepositoryContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId)
            {
            }
        }

        /// <summary>The repository resolved from the tenant customization layer, proving that a customized item replaces the base item entirely.</summary>
        public class TenantEmployeeRepository : DataFormRepository
        {
            public TenantEmployeeRepository(IRepositoryContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId)
            {
            }
        }

        /// <summary>Not derived from <see cref="DataFormRepository"/>; used to check the contract validation.</summary>
        public class NotARepository
        {
        }

        private sealed class StubDefineAccess : IDefineAccess
        {
            public ProgramSettings? Programs { get; set; }

            public ProgramSettings GetProgramSettings()
                => Programs ?? throw new FileNotFoundException("ProgramSettings.xml");

            public FormSchema GetFormSchema(string progId) => throw new NotImplementedException();
            public DatabaseSettings GetDatabaseSettings() => throw new NotImplementedException();
            public object GetDefine(DefineType defineType, string[]? keys = null) => throw new NotImplementedException();
            public void SaveDefine(DefineType defineType, object defineObject, string[]? keys = null) => throw new NotImplementedException();
            public SystemSettings GetSystemSettings() => throw new NotImplementedException();
            public void SaveSystemSettings(SystemSettings settings) => throw new NotImplementedException();
            public void SaveDatabaseSettings(DatabaseSettings settings) => throw new NotImplementedException();
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

        /// <summary>Knows only one (customizeId → ProgramSettings) pair.</summary>
        private sealed class StubCustomizeReader : ICustomizeDefineReader
        {
            private readonly string _customizeId;
            private readonly ProgramSettings _settings;

            public StubCustomizeReader(string customizeId, ProgramSettings settings)
            {
                _customizeId = customizeId;
                _settings = settings;
            }

            public ProgramSettings? GetCustomizeProgramSettings(string customizeId)
                => StringUtilities.IsEquals(customizeId, _customizeId) ? _settings : null;

            public LanguageResource? GetCustomizeLanguage(string customizeId, string lang, string ns) => null;
            public MenuSettings? GetCustomizeMenuSettings(string customizeId) => null;
            public PluginSettings? GetCustomizePluginSettings(string customizeId) => null;
            public FormLayout? GetCustomizeFormLayout(string customizeId, string layoutId) => null;
        }

        /// <summary>Returns a session with a CustomizeId for the given token, and null for any other.</summary>
        private sealed class StubSessionInfoService : ISessionInfoService
        {
            private readonly Guid _token;
            private readonly string _customizeId;

            public StubSessionInfoService(Guid token, string customizeId)
            {
                _token = token;
                _customizeId = customizeId;
            }

            public SessionInfo Get(Guid accessToken)
                => accessToken == _token
                    ? new SessionInfo { AccessToken = accessToken, CustomizeId = _customizeId }
                    : null!;

            public void Set(SessionInfo sessionInfo) => throw new NotSupportedException();
            public void Remove(Guid accessToken) => throw new NotSupportedException();
        }

        private static ProgramSettings Registry(string? repositoryTypeName)
        {
            var settings = new ProgramSettings();
            var item = settings.Items.Add(ProgId, "員工");
            item.Repository = repositoryTypeName ?? string.Empty;
            return settings;
        }

        private static string TypeNameOf<T>() => $"{typeof(T).FullName}, {typeof(T).Assembly.GetName().Name}";

        private static ProgramSettingsRepositoryTypeResolver CreateResolver(
            ProgramSettings? programs,
            ICustomizeDefineReader? customizeReader = null,
            ISessionInfoService? sessionInfoService = null)
            => new(new StubDefineAccess { Programs = programs }, customizeReader, sessionInfoService);

        #endregion

        [Fact]
        [DisplayName("Constructor throws ArgumentNullException for a null defineAccess")]
        public void Constructor_NullDefineAccess_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ProgramSettingsRepositoryTypeResolver(null!));
        }

        [Fact]
        [DisplayName("A set Repository resolves to the registered type")]
        public void Resolve_BoundRepository_ReturnsRegisteredType()
        {
            var resolver = CreateResolver(Registry(TypeNameOf<CustomEmployeeRepository>()));

            var type = resolver.Resolve(Guid.Empty, ProgId);

            Assert.Equal(typeof(CustomEmployeeRepository), type);
        }

        [Fact]
        [DisplayName("An empty Repository falls back to the framework default DataFormRepository")]
        public void Resolve_EmptyRepository_FallsBackToDefault()
        {
            var resolver = CreateResolver(Registry(repositoryTypeName: null));

            var type = resolver.Resolve(Guid.Empty, ProgId);

            Assert.Equal(typeof(DataFormRepository), type);
        }

        [Fact]
        [DisplayName("A progId missing from the registry falls back to the framework default and is not an error")]
        public void Resolve_ProgIdNotRegistered_FallsBackToDefault()
        {
            var resolver = CreateResolver(new ProgramSettings());

            var type = resolver.Resolve(Guid.Empty, "Department");

            Assert.Equal(typeof(DataFormRepository), type);
        }

        [Fact]
        [DisplayName("Without ProgramSettings.xml it falls back to the framework default and is not an error")]
        public void Resolve_NoRegistryFile_FallsBackToDefault()
        {
            var resolver = CreateResolver(programs: null);

            var type = resolver.Resolve(Guid.Empty, ProgId);

            Assert.Equal(typeof(DataFormRepository), type);
        }

        [Fact]
        [DisplayName("A Repository type that cannot be loaded throws with a message naming the progId and the type name")]
        public void Resolve_UnloadableType_ThrowsNamingBoth()
        {
            const string TypeName = "Nowhere.NoSuchRepository, Nowhere.Assembly";
            var resolver = CreateResolver(Registry(TypeName));

            var ex = Assert.Throws<InvalidOperationException>(
                () => resolver.Resolve(Guid.Empty, ProgId));

            Assert.Contains(ProgId, ex.Message, StringComparison.Ordinal);
            Assert.Contains(TypeName, ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A Repository type with a Bee.NET name that cannot be loaded says it looks like a Bee.NET name")]
        public void Resolve_UnloadableBeeType_MessageHasBeeHint()
        {
            const string TypeName = "Bee.Repository.Form.OrderRepository, Bee.Repository";
            var resolver = CreateResolver(Registry(TypeName));

            var ex = Assert.Throws<InvalidOperationException>(
                () => resolver.Resolve(Guid.Empty, ProgId));

            Assert.Contains("Bee.NET", ex.Message, StringComparison.Ordinal);
            Assert.Contains("Migrating from Bee.NET", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A Repository type not derived from DataFormRepository throws with a message naming the progId and the type name")]
        public void Resolve_NotDerivedFromDataFormRepository_ThrowsNamingBoth()
        {
            string typeName = TypeNameOf<NotARepository>();
            var resolver = CreateResolver(Registry(typeName));

            var ex = Assert.Throws<InvalidOperationException>(
                () => resolver.Resolve(Guid.Empty, ProgId));

            Assert.Contains(ProgId, ex.Message, StringComparison.Ordinal);
            Assert.Contains(typeName, ex.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(DataFormRepository), ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A tenant customization layer that declares the progId replaces the base binding entirely")]
        public void Resolve_CustomizationDeclaresProgId_ReplacesBaseBinding()
        {
            var token = Guid.NewGuid();
            var resolver = CreateResolver(
                Registry(TypeNameOf<CustomEmployeeRepository>()),
                new StubCustomizeReader(CustomizeId, Registry(TypeNameOf<TenantEmployeeRepository>())),
                new StubSessionInfoService(token, CustomizeId));

            var type = resolver.Resolve(token, ProgId);

            Assert.Equal(typeof(TenantEmployeeRepository), type);
        }

        [Fact]
        [DisplayName("A session without a customization ID resolves the base binding")]
        public void Resolve_SessionWithoutCustomizeId_UsesBaseBinding()
        {
            var token = Guid.NewGuid();
            var resolver = CreateResolver(
                Registry(TypeNameOf<CustomEmployeeRepository>()),
                new StubCustomizeReader(CustomizeId, Registry(TypeNameOf<TenantEmployeeRepository>())),
                new StubSessionInfoService(token, customizeId: string.Empty));

            var type = resolver.Resolve(token, ProgId);

            Assert.Equal(typeof(CustomEmployeeRepository), type);
        }

        [Fact]
        [DisplayName("A customization layer that does not declare the progId falls back to the base binding")]
        public void Resolve_CustomizationSilentOnProgId_FallsBackToBase()
        {
            var token = Guid.NewGuid();
            var resolver = CreateResolver(
                Registry(TypeNameOf<CustomEmployeeRepository>()),
                new StubCustomizeReader(CustomizeId, new ProgramSettings()),
                new StubSessionInfoService(token, CustomizeId));

            var type = resolver.Resolve(token, ProgId);

            Assert.Equal(typeof(CustomEmployeeRepository), type);
        }
    }
}
