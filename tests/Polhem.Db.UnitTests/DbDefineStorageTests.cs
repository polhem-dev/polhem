using System.ComponentModel;
using System.Globalization;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Db.CacheNotify;
using Polhem.Db.Manager;
using Polhem.Db.Storage;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Integration tests for <see cref="DbDefineStorage"/> against a live database per dialect.
    /// Constructs the storage directly with the fixture's connection manager + cache-notify service
    /// (avoiding the DI activation cycle), and exercises round-trip Save/Get plus the same-transaction
    /// notification bump for single-key, composite-key, and optional definition types. Tests skip when
    /// the dialect's <c>POLHEM_TEST_CONNSTR_*</c> env var is unset.
    /// </summary>
    public class DbDefineStorageTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public DbDefineStorageTests(SharedDbFixture fx) { _fx = fx; }

        private DbDefineStorage NewStorage(DatabaseType databaseType)
        {
            var connectionManager = _fx.GetRequiredService<IDbConnectionManager>();
            var cacheNotify = _fx.GetRequiredService<ICacheNotifyService>();
            var databaseId = TestDbConventions.GetDatabaseId(databaseType);
            return new DbDefineStorage(connectionManager, cacheNotify, databaseId);
        }

        // Reads the notification version for a cache key; -1 when no row exists yet.
        private long CacheVersion(DatabaseType databaseType, string cacheKey)
        {
            var dbAccess = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(databaseType));
            string tbl = databaseType.QuoteIdentifier("st_cache_notify");
            string keyCol = databaseType.QuoteIdentifier("cache_key");
            string verCol = databaseType.QuoteIdentifier("cache_version");
            var scalar = dbAccess.ExecuteScalar($"SELECT {verCol} FROM {tbl} WHERE {keyCol} = {{0}}", cacheKey);
            if (scalar is null || scalar is DBNull) return -1;
            return Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
        }

        private void RunRoundTrip(DatabaseType databaseType)
        {
            var storage = NewStorage(databaseType);

            // --- FormSchema (single key) round-trip + overwrite bumps the same key ---
            string progId = "RT_" + Guid.NewGuid().ToString("N");
            storage.SaveFormSchema(new FormSchema(progId, "RT Form"));

            var form = storage.GetFormSchema(progId);
            Assert.NotNull(form);
            Assert.Equal(progId, form!.ProgId);
            Assert.Equal("RT Form", form.DisplayName);

            Assert.Contains(progId, storage.GetFormSchemaIds());

            long v1 = CacheVersion(databaseType, $"FormSchema:{progId}");
            Assert.True(v1 >= 1, $"expected bump version >= 1, got {v1}");

            // Overwrite: content updates and the notification version advances.
            storage.SaveFormSchema(new FormSchema(progId, "RT Form v2"));
            Assert.Equal("RT Form v2", storage.GetFormSchema(progId)!.DisplayName);
            Assert.True(CacheVersion(databaseType, $"FormSchema:{progId}") > v1);

            // --- TableSchema (composite key "category.table") ---
            string tableName = "rt_" + Guid.NewGuid().ToString("N");
            storage.SaveTableSchema("common", new TableSchema { TableName = tableName, DisplayName = "RT Table" });

            var table = storage.GetTableSchema("common", tableName);
            Assert.NotNull(table);
            Assert.Equal(tableName, table!.TableName);
            // define_key uses the dot separator the cache keys on, so the bump key aligns.
            Assert.True(CacheVersion(databaseType, $"TableSchema:common.{tableName}") >= 1);

            // --- FormLayout (single key) ---
            string layoutId = "RTL_" + Guid.NewGuid().ToString("N");
            storage.SaveFormLayout(new FormLayout { LayoutId = layoutId });
            Assert.Equal(layoutId, storage.GetFormLayout(layoutId)!.LayoutId);

            // --- Language (optional: missing returns null; then round-trips) ---
            string lang = $"rt-{Guid.NewGuid().ToString("N")[..8]}";
            const string ns = "common";
            Assert.Null(storage.GetLanguage(lang, ns));

            storage.SaveLanguage(new LanguageResource { Lang = lang, Namespace = ns });
            var resource = storage.GetLanguage(lang, ns);
            Assert.NotNull(resource);
            Assert.Equal(lang, resource!.Lang);
            // LanguageResource cache keys on "{lang}.{ns}", so the bump key matches.
            Assert.True(CacheVersion(databaseType, $"LanguageResource:{lang}.{ns}") >= 1);

            // --- ProgramSettings (singleton key "*") round-trip + bump ---
            storage.SaveProgramSettings(new ProgramSettings());
            Assert.NotNull(storage.GetProgramSettings());
            Assert.True(CacheVersion(databaseType, "ProgramSettings:*") >= 1);

            // --- PluginSettings (optional singleton: missing returns null; then round-trips) ---
            var plugins = new PluginSettings();
            plugins.Items!.Add("Order").Plugins!.Add("Pkg.Audit, Pkg", PluginStage.AfterSave);
            storage.SavePluginSettings(plugins);
            Assert.Equal(new PluginBinding("Pkg.Audit, Pkg", PluginStage.AfterSave),
                Assert.Single(storage.GetPluginSettings()!.GetPluginBindings("Order")));
            Assert.True(CacheVersion(databaseType, "PluginSettings:*") >= 1);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server DbDefineStorage round-trips each type and bumps the version in the same transaction")]
        public void RoundTrip_SqlServer() => RunRoundTrip(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL DbDefineStorage round-trips each type and bumps the version in the same transaction")]
        public void RoundTrip_PostgreSQL() => RunRoundTrip(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL DbDefineStorage round-trips each type and bumps the version in the same transaction")]
        public void RoundTrip_MySQL() => RunRoundTrip(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle DbDefineStorage round-trips each type and bumps the version in the same transaction")]
        public void RoundTrip_Oracle() => RunRoundTrip(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server Get of a missing required definition throws")]
        public void GetRequired_Missing_Throws()
        {
            var storage = NewStorage(DatabaseType.SQLServer);
            Assert.Throws<InvalidOperationException>(
                () => storage.GetFormSchema("RT_missing_" + Guid.NewGuid().ToString("N")));
        }

        // IServiceProvider that fails if asked to resolve anything — proves the DI ctor defers
        // resolution (otherwise the DB-storage activation would dead-lock the construction cycle).
        private sealed class ThrowingServiceProvider : IServiceProvider
        {
            public object GetService(Type serviceType)
                => throw new InvalidOperationException("Dependencies must not be resolved at construction.");
        }

        [Fact]
        [DisplayName("Constructing with an IServiceProvider does not resolve dependencies at construction (breaks the DI construction cycle)")]
        public void Constructor_ServiceProvider_DefersDependencyResolution()
        {
            var exception = Record.Exception(() => new DbDefineStorage(new ThrowingServiceProvider()));
            Assert.Null(exception);
        }

        // Inserts a customization-override row (customize_id != base) directly. The only customize-write API,
        // `SaveCustomizePluginSettings`, covers plugin settings alone, and seeding the row by hand keeps the
        // reader tests independent of the writer.
        private void SeedCustomizeRow(DatabaseType databaseType, string defineType, string customizeId, string defineKey, string contentXml)
        {
            var dbAccess = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(databaseType));
            string now = DbDialectRegistry.Get(databaseType).GetDefaultValueExpression(FieldDbType.DateTime);
            string tbl = databaseType.QuoteIdentifier("st_define");
            string type = databaseType.QuoteIdentifier("define_type");
            string cust = databaseType.QuoteIdentifier("customize_id");
            string key = databaseType.QuoteIdentifier("define_key");
            string content = databaseType.QuoteIdentifier("content");
            string upd = databaseType.QuoteIdentifier("sys_update_time");
            dbAccess.ExecuteNonQuery(
                $"INSERT INTO {tbl} ({type}, {cust}, {key}, {content}, {upd}) VALUES ({{0}}, {{1}}, {{2}}, {{3}}, {now})",
                defineType, customizeId, defineKey, contentXml);
        }

        // The customize reader returns the per-customizeId override row; a different customizeId or the
        // base layer are isolated (distinct PK rows in the same table).
        private void RunCustomizeOverlay(DatabaseType databaseType)
        {
            var storage = NewStorage(databaseType);
            string customizeId = "cust_" + Guid.NewGuid().ToString("N");

            // FormLayout override: missing → null, then resolves after seeding.
            string layoutId = "RTL_" + Guid.NewGuid().ToString("N");
            Assert.Null(storage.GetCustomizeFormLayout(customizeId, layoutId));
            SeedCustomizeRow(databaseType, "FormLayout", customizeId, layoutId,
                XmlCodec.Serialize(new FormLayout { LayoutId = layoutId }));
            Assert.Equal(layoutId, storage.GetCustomizeFormLayout(customizeId, layoutId)!.LayoutId);
            // A different tenant has no override.
            Assert.Null(storage.GetCustomizeFormLayout("other_" + Guid.NewGuid().ToString("N"), layoutId));

            // Base layer is isolated from the customize layer (same define_key, different customize_id).
            storage.SaveFormLayout(new FormLayout { LayoutId = layoutId });
            Assert.NotNull(storage.GetFormLayout(layoutId));
            Assert.NotNull(storage.GetCustomizeFormLayout(customizeId, layoutId));

            // Language override (composite "lang.ns" key).
            string lang = $"rt-{Guid.NewGuid().ToString("N")[..8]}";
            const string ns = "common";
            SeedCustomizeRow(databaseType, "LanguageResource", customizeId, $"{lang}.{ns}",
                XmlCodec.Serialize(new LanguageResource { Lang = lang, Namespace = ns }));
            Assert.Equal(lang, storage.GetCustomizeLanguage(customizeId, lang, ns)!.Lang);

            // ProgramSettings override (singleton key "*").
            Assert.Null(storage.GetCustomizeProgramSettings(customizeId));
            SeedCustomizeRow(databaseType, "ProgramSettings", customizeId, "*",
                XmlCodec.Serialize(new ProgramSettings()));
            Assert.NotNull(storage.GetCustomizeProgramSettings(customizeId));

            // PluginSettings override (singleton key "*"). The base row written above carries a
            // different chain, so this also proves the two layers stay separate rows rather than
            // one overwriting the other.
            Assert.Null(storage.GetCustomizePluginSettings(customizeId));

            var basePlugins = new PluginSettings();
            basePlugins.Items!.Add("Order").Plugins!.Add("Pkg.Audit, Pkg", PluginStage.AfterSave);
            storage.SavePluginSettings(basePlugins);

            var tenantPlugins = new PluginSettings();
            tenantPlugins.Items!.Add("Order").Plugins!.Add("Cust.CreditLimit, Cust", PluginStage.BeforeSave);
            SeedCustomizeRow(databaseType, "PluginSettings", customizeId, "*",
                XmlCodec.Serialize(tenantPlugins));

            Assert.Equal(new PluginBinding("Cust.CreditLimit, Cust", PluginStage.BeforeSave),
                Assert.Single(storage.GetCustomizePluginSettings(customizeId)!.GetPluginBindings("Order")));
            Assert.Equal(new PluginBinding("Pkg.Audit, Pkg", PluginStage.AfterSave),
                Assert.Single(storage.GetPluginSettings()!.GetPluginBindings("Order")));
            // A different tenant has no override.
            Assert.Null(storage.GetCustomizePluginSettings("other_" + Guid.NewGuid().ToString("N")));
        }

        // The customize-write API: a tenant's plugin chain lands in its own row (the base row and other tenants are
        // untouched), a second save overwrites it, and each save bumps the cache-notify version.
        private void RunSaveCustomizePluginSettings(DatabaseType databaseType)
        {
            var storage = NewStorage(databaseType);
            string customizeId = "cust_" + Guid.NewGuid().ToString("N");
            string otherId = "other_" + Guid.NewGuid().ToString("N");

            var basePlugins = new PluginSettings();
            basePlugins.Items!.Add("Order").Plugins!.Add("Pkg.Audit, Pkg", PluginStage.AfterSave);
            storage.SavePluginSettings(basePlugins);
            long before = CacheVersion(databaseType, "PluginSettings:*");

            var first = new PluginSettings();
            first.Items!.Add("Order").Plugins!.Add("Cust.First, Cust", PluginStage.BeforeSave);
            storage.SaveCustomizePluginSettings(customizeId, first);

            Assert.Equal(new PluginBinding("Cust.First, Cust", PluginStage.BeforeSave),
                Assert.Single(storage.GetCustomizePluginSettings(customizeId)!.GetPluginBindings("Order")));
            Assert.Equal(new PluginBinding("Pkg.Audit, Pkg", PluginStage.AfterSave),
                Assert.Single(storage.GetPluginSettings()!.GetPluginBindings("Order")));
            Assert.Null(storage.GetCustomizePluginSettings(otherId));
            long afterFirst = CacheVersion(databaseType, "PluginSettings:*");
            Assert.True(afterFirst > before, $"expected the notify version to advance past {before}, got {afterFirst}");

            var second = new PluginSettings();
            second.Items!.Add("Order").Plugins!.Add("Cust.Second, Cust", PluginStage.AfterSave);
            storage.SaveCustomizePluginSettings(customizeId, second);

            Assert.Equal(new PluginBinding("Cust.Second, Cust", PluginStage.AfterSave),
                Assert.Single(storage.GetCustomizePluginSettings(customizeId)!.GetPluginBindings("Order")));
            Assert.True(CacheVersion(databaseType, "PluginSettings:*") > afterFirst);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server SaveCustomizePluginSettings upserts the tenant row only and bumps the notify version")]
        public void SaveCustomizePluginSettings_SqlServer() => RunSaveCustomizePluginSettings(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL SaveCustomizePluginSettings upserts the tenant row only and bumps the notify version")]
        public void SaveCustomizePluginSettings_PostgreSQL() => RunSaveCustomizePluginSettings(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL SaveCustomizePluginSettings upserts the tenant row only and bumps the notify version")]
        public void SaveCustomizePluginSettings_MySQL() => RunSaveCustomizePluginSettings(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle SaveCustomizePluginSettings upserts the tenant row only and bumps the notify version")]
        public void SaveCustomizePluginSettings_Oracle() => RunSaveCustomizePluginSettings(DatabaseType.Oracle);

        [Fact]
        [DisplayName("SaveCustomizePluginSettings rejects a blank customizeId before touching the database")]
        public void SaveCustomizePluginSettings_BlankCustomizeId_Throws()
        {
            var storage = new DbDefineStorage(new ThrowingServiceProvider());

            Assert.Throws<ArgumentException>(() => storage.SaveCustomizePluginSettings(" ", new PluginSettings()));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server reads customization overlays and isolates base from tenants")]
        public void CustomizeOverlay_SqlServer() => RunCustomizeOverlay(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL reads customization overlays and isolates base from tenants")]
        public void CustomizeOverlay_PostgreSQL() => RunCustomizeOverlay(DatabaseType.PostgreSQL);
    }
}
