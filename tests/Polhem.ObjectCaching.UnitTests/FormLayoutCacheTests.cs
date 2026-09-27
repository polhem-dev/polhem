using System.ComponentModel;
using System.Reflection;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.ObjectCaching.Define;

namespace Polhem.ObjectCaching.UnitTests
{
    public class FormLayoutCacheTests
    {
        private sealed class StubDefineStorage : IDefineStorage
        {
            private readonly FormLayout? _layout;

            public StubDefineStorage(FormLayout? layout = null) => _layout = layout;

            public FormLayout? GetFormLayout(string layoutId) => _layout;
            public DbCategorySettings? GetDbCategorySettings() => throw new NotImplementedException();
            public void SaveDbCategorySettings(DbCategorySettings settings) => throw new NotImplementedException();
            public CurrencySettings? GetCurrencySettings() => throw new NotImplementedException();
            public void SaveCurrencySettings(CurrencySettings settings) => throw new NotImplementedException();
            public UnitSettings? GetUnitSettings() => throw new NotImplementedException();
            public void SaveUnitSettings(UnitSettings settings) => throw new NotImplementedException();
            public ProgramSettings? GetProgramSettings() => throw new NotImplementedException();
            public void SaveProgramSettings(ProgramSettings settings) => throw new NotImplementedException();
            public MenuSettings? GetMenuSettings() => throw new NotImplementedException();
            public void SaveMenuSettings(MenuSettings settings) => throw new NotImplementedException();
            public PluginSettings? GetPluginSettings() => throw new NotImplementedException();
            public void SavePluginSettings(PluginSettings settings) => throw new NotImplementedException();
            public TableSchema? GetTableSchema(string categoryId, string tableName) => throw new NotImplementedException();
            public void SaveTableSchema(string categoryId, TableSchema tableSchema) => throw new NotImplementedException();
            public FormSchema? GetFormSchema(string progId) => throw new NotImplementedException();
            public void SaveFormSchema(FormSchema formSchema) => throw new NotImplementedException();
            public void SaveFormLayout(FormLayout formLayout) => throw new NotImplementedException();
            public LanguageResource? GetLanguage(string lang, string ns) => throw new NotImplementedException();
            public void SaveLanguage(LanguageResource resource) => throw new NotImplementedException();
        }

        /// <summary>
        /// Calls the cache's policy override. <see cref="FormLayoutCache"/> is sealed, so its
        /// <c>protected</c> override is reached through the base declaration; invoking the base method
        /// dispatches virtually to the override under test.
        /// </summary>
        private static CacheItemPolicy GetCachePolicy(FormLayoutCache cache, string key)
        {
            var method = typeof(KeyObjectCache<FormLayout>).GetMethod(
                "GetPolicy", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(string)]);
            Assert.NotNull(method);
            return Assert.IsType<CacheItemPolicy>(method.Invoke(cache, [key]));
        }

        [Fact]
        [DisplayName("Constructor throws ArgumentNullException for a null storage")]
        public void Constructor_NullStorage_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new FormLayoutCache(null!));
        }

        [Fact]
        [DisplayName("GetPolicy sets ChangeMonitorFilePaths when using FileDefineStorage")]
        public void GetPolicy_FileDefineStorage_SetsChangeMonitorFilePaths()
        {
            var storage = new FileDefineStorage(new PathOptions());
            var cache = new FormLayoutCache(storage);

            var policy = GetCachePolicy(cache, "Employee");

            Assert.NotNull(policy.ChangeMonitorFilePaths);
            Assert.Single(policy.ChangeMonitorFilePaths);
        }

        [Fact]
        [DisplayName("GetPolicy leaves ChangeMonitorFilePaths null when not using FileDefineStorage")]
        public void GetPolicy_NonFileDefineStorage_NoChangeMonitorFilePaths()
        {
            var stub = new StubDefineStorage();
            var cache = new FormLayoutCache(stub);

            var policy = GetCachePolicy(cache, "Employee");

            Assert.Null(policy.ChangeMonitorFilePaths);
        }

        [Fact]
        [DisplayName("Get calls storage.GetFormLayout and returns its result")]
        public void Get_StorageReturnsLayout_ReturnsSameLayout()
        {
            string prefix = Guid.NewGuid().ToString("N");
            var layout = new FormLayout();
            var stub = new StubDefineStorage(layout);
            var cache = new FormLayoutCache(stub, prefix);

            var result = cache.Get("TestLayout");

            Assert.Same(layout, result);
            cache.Remove("TestLayout");
        }

        [Fact]
        [DisplayName("Get returns null when storage.GetFormLayout returns null")]
        public void Get_StorageReturnsNull_ReturnsNull()
        {
            string prefix = Guid.NewGuid().ToString("N");
            var stub = new StubDefineStorage();
            var cache = new FormLayoutCache(stub, prefix);

            var result = cache.Get("NonExistent");

            Assert.Null(result);
        }
    }
}
