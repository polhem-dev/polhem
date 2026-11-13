using System.ComponentModel;
using Polhem.Db.CacheNotify;
using Polhem.Db.Manager;
using Polhem.Db.Storage;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Tests for the singleton settings round trips, the argument checks, the change source and the deferred
    /// dependency resolution of <see cref="DbDefineStorage"/>.
    /// </summary>
    public class DbDefineStorageSettingsTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public DbDefineStorageSettingsTests(SharedDbFixture fx) { _fx = fx; }

        private DbDefineStorage NewStorage(DatabaseType databaseType)
            => new(_fx.GetRequiredService<IDbConnectionManager>(), _fx.GetRequiredService<ICacheNotifyService>(),
                TestDbConventions.GetDatabaseId(databaseType));

        /// <summary>A storage that must never reach its dependencies.</summary>
        private static DbDefineStorage UnreachableStorage() => new(EmptyServiceProvider.Instance);

        private void RunSettingsRoundTrip(DatabaseType databaseType)
        {
            var storage = NewStorage(databaseType);

            storage.SaveDbCategorySettings(new DbCategorySettings());
            Assert.NotNull(storage.GetDbCategorySettings());

            storage.SaveCurrencySettings([]);
            Assert.NotNull(storage.GetCurrencySettings());

            storage.SaveUnitSettings([]);
            Assert.NotNull(storage.GetUnitSettings());

            storage.SaveMenuSettings(new MenuSettings());
            Assert.NotNull(storage.GetMenuSettings());

            string customizeId = "cust_" + Guid.NewGuid().ToString("N");
            Assert.Null(storage.GetCustomizeMenuSettings(customizeId));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite DbDefineStorage round-trips the singleton settings and reads no tenant menu for a new tenant")]
        public void SettingsRoundTrip_Sqlite() => RunSettingsRoundTrip(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server DbDefineStorage round-trips the singleton settings and reads no tenant menu for a new tenant")]
        public void SettingsRoundTrip_SqlServer() => RunSettingsRoundTrip(DatabaseType.SQLServer);

        private void RunDefinitionRoundTrip(DatabaseType databaseType)
        {
            var storage = NewStorage(databaseType);

            string progId = "RTS_" + Guid.NewGuid().ToString("N");
            storage.SaveFormSchema(new FormSchema(progId, "Settings Form"));
            Assert.Equal("Settings Form", storage.GetFormSchema(progId)!.DisplayName);
            Assert.Contains(progId, storage.GetFormSchemaIds());

            string tableName = "rts_" + Guid.NewGuid().ToString("N");
            storage.SaveTableSchema("common", new TableSchema { TableName = tableName });
            Assert.Equal(tableName, storage.GetTableSchema("common", tableName)!.TableName);

            string layoutId = "RTSL_" + Guid.NewGuid().ToString("N");
            Assert.Null(storage.GetFormLayout(layoutId));
            storage.SaveFormLayout(new FormLayout { LayoutId = layoutId });
            Assert.Equal(layoutId, storage.GetFormLayout(layoutId)!.LayoutId);

            string lang = $"rs-{Guid.NewGuid().ToString("N")[..8]}";
            storage.SaveLanguage(new LanguageResource { Lang = lang, Namespace = "common" });
            Assert.Equal(lang, storage.GetLanguage(lang, "common")!.Lang);

            storage.SaveProgramSettings(new ProgramSettings());
            Assert.NotNull(storage.GetProgramSettings());

            storage.SavePluginSettings(new PluginSettings());
            Assert.NotNull(storage.GetPluginSettings());

            string customizeId = "cust_" + Guid.NewGuid().ToString("N");
            storage.SaveCustomizePluginSettings(customizeId, new PluginSettings());
            Assert.NotNull(storage.GetCustomizePluginSettings(customizeId));
            Assert.Null(storage.GetCustomizeFormLayout(customizeId, layoutId));
            Assert.Null(storage.GetCustomizeLanguage(customizeId, lang, "common"));
            Assert.Null(storage.GetCustomizeProgramSettings(customizeId));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite DbDefineStorage round-trips each definition type through its upsert")]
        public void DefinitionRoundTrip_Sqlite() => RunDefinitionRoundTrip(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite Get of a missing required definition throws DefinitionNotFoundException")]
        public void GetFormSchema_MissingRow_Sqlite_ThrowsDefinitionNotFound()
        {
            var storage = NewStorage(DatabaseType.SQLite);

            Assert.Throws<DefinitionNotFoundException>(() => storage.GetFormSchema("Missing_" + Guid.NewGuid().ToString("N")));
        }

        [Fact]
        [DisplayName("The explicit constructor rejects a null connection manager")]
        public void Constructor_NullConnectionManager_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new DbDefineStorage(null!, _fx.GetRequiredService<ICacheNotifyService>()));
        }

        [Fact]
        [DisplayName("The explicit constructor rejects a null cache-notify service")]
        public void Constructor_NullCacheNotify_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new DbDefineStorage(_fx.GetRequiredService<IDbConnectionManager>(), null!));
        }

        [Fact]
        [DisplayName("The explicit constructor rejects a blank database id")]
        public void Constructor_BlankDatabaseId_Throws()
        {
            Assert.Throws<ArgumentException>(() => new DbDefineStorage(_fx.GetRequiredService<IDbConnectionManager>(), _fx.GetRequiredService<ICacheNotifyService>(), " "));
        }

        [Fact]
        [DisplayName("The service-provider constructor rejects a null provider")]
        public void Constructor_NullServiceProvider_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new DbDefineStorage((IServiceProvider)null!));
        }

        [Fact]
        [DisplayName("A read through a provider that has no connection manager names the missing service")]
        public void GetFormSchema_ProviderWithoutConnectionManager_ThrowsInvalidOperation()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => UnreachableStorage().GetFormSchema("Any"));

            Assert.Contains(nameof(IDbConnectionManager), ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Every Save method rejects a null definition before touching the database")]
        public void SaveMethods_NullDefinition_ThrowArgumentNull()
        {
            var storage = UnreachableStorage();

            Assert.Throws<ArgumentNullException>(() => storage.SaveDbCategorySettings(null!));
            Assert.Throws<ArgumentNullException>(() => storage.SaveCurrencySettings(null!));
            Assert.Throws<ArgumentNullException>(() => storage.SaveUnitSettings(null!));
            Assert.Throws<ArgumentNullException>(() => storage.SaveProgramSettings(null!));
            Assert.Throws<ArgumentNullException>(() => storage.SaveMenuSettings(null!));
            Assert.Throws<ArgumentNullException>(() => storage.SavePluginSettings(null!));
            Assert.Throws<ArgumentNullException>(() => storage.SaveTableSchema("common", null!));
            Assert.Throws<ArgumentNullException>(() => storage.SaveFormSchema(null!));
            Assert.Throws<ArgumentNullException>(() => storage.SaveFormLayout(null!));
            Assert.Throws<ArgumentNullException>(() => storage.SaveLanguage(null!));
            Assert.Throws<ArgumentNullException>(() => storage.SaveCustomizePluginSettings("tenant", null!));
        }

        [Theory]
        [InlineData(DefineType.DbCategorySettings, "DbCategorySettings:*")]
        [InlineData(DefineType.CurrencySettings, "CurrencySettings:*")]
        [InlineData(DefineType.UnitSettings, "UnitSettings:*")]
        [InlineData(DefineType.ProgramSettings, "ProgramSettings:*")]
        [InlineData(DefineType.MenuSettings, "MenuSettings:*")]
        [DisplayName("GetChangeSource reports the singleton notify key a save of that settings type touches")]
        public void GetChangeSource_SingletonType_ReturnsSingletonKey(DefineType defineType, string expected)
        {
            Assert.Equal(expected, UnreachableStorage().GetChangeSource(defineType).NotifyKey);
        }

        [Fact]
        [DisplayName("GetChangeSource reports keyed notify keys for the form schema, layout, table schema and language")]
        public void GetChangeSource_KeyedTypes_ReturnKeyedNotifyKeys()
        {
            var storage = UnreachableStorage();

            Assert.Equal("FormSchema:Order", storage.GetChangeSource(DefineType.FormSchema, "Order").NotifyKey);
            Assert.Equal("FormLayout:OrderCard", storage.GetChangeSource(DefineType.FormLayout, "OrderCard").NotifyKey);
            Assert.Equal("TableSchema:company.ft_order",
                storage.GetChangeSource(DefineType.TableSchema, "company", "ft_order").NotifyKey);
            Assert.Equal("LanguageResource:en-US.Order",
                storage.GetChangeSource(DefineType.Language, "en-US", "Order").NotifyKey);
        }

        [Theory]
        [InlineData(DefineType.FormSchema)]
        [InlineData(DefineType.FormLayout)]
        [InlineData(DefineType.TableSchema)]
        [InlineData(DefineType.Language)]
        [DisplayName("GetChangeSource without keys reports no change source for a keyed type")]
        public void GetChangeSource_NoKeys_ReturnsNone(DefineType defineType)
        {
            Assert.Null(UnreachableStorage().GetChangeSource(defineType).NotifyKey);
        }

        [Theory]
        [InlineData(DefineType.TableSchema)]
        [InlineData(DefineType.Language)]
        [DisplayName("GetChangeSource with one key reports no change source for a type keyed by two")]
        public void GetChangeSource_OneKeyForTwoKeyType_ReturnsNone(DefineType defineType)
        {
            Assert.Null(UnreachableStorage().GetChangeSource(defineType, "first").NotifyKey);
        }

        [Fact]
        [DisplayName("GetChangeSource for a type this storage does not hold reports no change source")]
        public void GetChangeSource_FileOnlyType_ReturnsNone()
        {
            Assert.Null(UnreachableStorage().GetChangeSource(DefineType.SystemSettings).NotifyKey);
        }
    }
}
