using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.ObjectCaching;
using Microsoft.Extensions.DependencyInjection;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// Checks how AddPolhemFramework wires the tenant customization overlay: the provider and the reader resolve;
    /// ILanguageService, IBoTypeResolver and IDefineAccess still resolve with the reader injected (no circular
    /// dependency); an unset CustomizePath means base only; a set CustomizePath makes the overlay work end to end
    /// through DI.
    /// </summary>
    public sealed class CustomizationWiringTests : IDisposable
    {
        private readonly string _defineDir;
        private readonly string _customizeDir;

        public CustomizationWiringTests()
        {
            _defineDir = Path.Combine(Path.GetTempPath(), $"polhem-wire-def-{Guid.NewGuid():N}");
            _customizeDir = Path.Combine(Path.GetTempPath(), $"polhem-wire-cust-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_defineDir);
            Directory.CreateDirectory(_customizeDir);
        }

        public void Dispose()
        {
            foreach (var dir in new[] { _defineDir, _customizeDir })
            {
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        private ServiceProvider BuildProvider(string customizePath)
        {
            var services = new ServiceCollection();
            services.AddPolhemFramework(
                new BackendConfiguration(),
                new PathOptions { DefinePath = _defineDir, CustomizePath = customizePath },
                autoCreateMasterKey: true);
            return services.BuildServiceProvider();
        }

        [Fact]
        [DisplayName("When the IDefineStorage itself implements ICustomizeDefineReader, it is preferred over the file-based reader")]
        public void AddPolhemFramework_StorageImplementsReader_PrefersStorage()
        {
            var services = new ServiceCollection();
            var configuration = new BackendConfiguration();
            configuration.Components.DefineStorage =
                $"{typeof(CustomizeAwareStorage).FullName}, {typeof(CustomizeAwareStorage).Assembly.GetName().Name}";
            services.AddPolhemFramework(
                configuration,
                new PathOptions { DefinePath = _defineDir, CustomizePath = _customizeDir },
                autoCreateMasterKey: true);
            using var sp = services.BuildServiceProvider();

            var reader = sp.GetRequiredService<ICustomizeDefineReader>();

            // The storage's own implementation is used, not the files under CustomizePath.
            Assert.IsType<CustomizeAwareStorage>(reader);
            Assert.Same(sp.GetRequiredService<IDefineStorage>(), reader);
        }

        [Fact]
        [DisplayName("When the IDefineStorage does not implement ICustomizeDefineReader, the file-based reader is used")]
        public void AddPolhemFramework_StorageWithoutReader_FallsBackToFileReader()
        {
            using var sp = BuildProvider(_customizeDir);

            Assert.IsType<CustomizeDefineReader>(sp.GetRequiredService<ICustomizeDefineReader>());
        }

        [Fact]
        [DisplayName("AddPolhemFramework registers and resolves ICacheContainerProvider and ICustomizeDefineReader")]
        public void AddPolhemFramework_ResolvesProviderAndReader()
        {
            using var sp = BuildProvider(_customizeDir);

            Assert.NotNull(sp.GetRequiredService<ICacheContainerProvider>());
            Assert.NotNull(sp.GetRequiredService<ICustomizeDefineReader>());
        }

        [Fact]
        [DisplayName("ILanguageService, IBoTypeResolver and IDefineAccess still resolve with the reader injected (no circular dependency)")]
        public void AddPolhemFramework_ConsumersWithReader_Resolve()
        {
            using var sp = BuildProvider(_customizeDir);

            Assert.NotNull(sp.GetRequiredService<Polhem.Definition.Language.ILanguageService>());
            Assert.NotNull(sp.GetRequiredService<Polhem.Business.IBoTypeResolver>());
            Assert.NotNull(sp.GetRequiredService<IDefineAccess>());
        }

        [Fact]
        [DisplayName("With CustomizePath unset the reader returns null for FormLayout, Language and ProgramSettings (base only)")]
        public void AddPolhemFramework_EmptyCustomizePath_ReaderReturnsNull()
        {
            using var sp = BuildProvider(customizePath: string.Empty);
            var reader = sp.GetRequiredService<ICustomizeDefineReader>();

            Assert.Null(reader.GetCustomizeFormLayout("acme", "EmployeeDefault"));
            Assert.Null(reader.GetCustomizeLanguage("acme", "zh-TW", "Common"));
            Assert.Null(reader.GetCustomizeProgramSettings("acme"));
        }

        [Fact]
        [DisplayName("With CustomizePath set, IDefineAccess.GetFormLayout returns the customized layout end to end through the DI-injected reader")]
        public void AddPolhemFramework_CustomizePathSet_FormLayoutOverlayWorksEndToEnd()
        {
            const string customizeId = "acme";
            const string layoutId = "EmployeeDefault";
            var custPaths = new CustomizeOnlyPathOptions(_customizeDir, customizeId);
            XmlCodec.SerializeToFile(new FormLayout { LayoutId = layoutId }, custPaths.GetFormLayoutFilePath(layoutId));

            using var sp = BuildProvider(_customizeDir);
            var access = sp.GetRequiredService<IDefineAccess>();

            // Whole-file override: with a non-empty customizeId and an existing customized file, the customized
            // layout is returned without touching the base.
            var result = access.GetFormLayout(customizeId, layoutId);

            Assert.NotNull(result);
            Assert.Equal(layoutId, result.LayoutId);
        }

        /// <summary>
        /// Simulates a database-style storage where the base and the customizations live in one place and differ only
        /// by an identifying column, so the storage itself is the <see cref="ICustomizeDefineReader"/> (like
        /// <c>DbDefineStorage</c>). The test only cares which one DI picks, so the methods need no real behavior.
        /// </summary>
        public sealed class CustomizeAwareStorage : IDefineStorage, ICustomizeDefineReader
        {
            public CustomizeAwareStorage(PathOptions paths)
            {
                ArgumentNullException.ThrowIfNull(paths);
            }

            public LanguageResource? GetCustomizeLanguage(string customizeId, string lang, string ns) => null;
            public ProgramSettings? GetCustomizeProgramSettings(string customizeId) => null;
            public FormLayout? GetCustomizeFormLayout(string customizeId, string layoutId) => null;
            public MenuSettings? GetCustomizeMenuSettings(string customizeId) => null;
            public PluginSettings? GetCustomizePluginSettings(string customizeId) => null;

            public DbCategorySettings? GetDbCategorySettings() => null;
            public void SaveDbCategorySettings(DbCategorySettings settings) { }
            public CurrencySettings? GetCurrencySettings() => null;
            public void SaveCurrencySettings(CurrencySettings settings) { }
            public UnitSettings? GetUnitSettings() => null;
            public void SaveUnitSettings(UnitSettings settings) { }
            public TableSchema? GetTableSchema(string categoryId, string tableName) => null;
            public void SaveTableSchema(string categoryId, TableSchema tableSchema) { }
            public FormSchema? GetFormSchema(string progId) => null;
            public void SaveFormSchema(FormSchema formSchema) { }
            public ProgramSettings? GetProgramSettings() => null;
            public void SaveProgramSettings(ProgramSettings settings) { }
            public MenuSettings? GetMenuSettings() => null;
            public void SaveMenuSettings(MenuSettings settings) { }
            public PluginSettings? GetPluginSettings() => null;
            public void SavePluginSettings(PluginSettings settings) { }
            public FormLayout? GetFormLayout(string layoutId) => null;
            public void SaveFormLayout(FormLayout formLayout) { }
            public LanguageResource? GetLanguage(string lang, string ns) => null;
            public void SaveLanguage(LanguageResource resource) { }
        }
    }
}
