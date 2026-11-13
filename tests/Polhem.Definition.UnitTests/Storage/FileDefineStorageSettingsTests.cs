using System.ComponentModel;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.Definition.UnitTests.Storage
{
    /// <summary>
    /// Tests for the optional settings files of <see cref="FileDefineStorage"/> (currency, unit and plugin settings) and
    /// for the change signal it reports for every define type. Each test works in its own temp directory.
    /// </summary>
    public sealed class FileDefineStorageSettingsTests : IDisposable
    {
        private readonly string _root;
        private readonly PathOptions _paths;

        public FileDefineStorageSettingsTests()
        {
            _root = Path.Combine(Path.GetTempPath(), $"polhem-define-settings-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
            _paths = new PathOptions { DefinePath = _root };
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        [Fact]
        [DisplayName("The constructor rejects null path options")]
        public void Constructor_NullPaths_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new FileDefineStorage(null!));
        }

        [Fact]
        [DisplayName("GetCurrencySettings returns null when the optional currency file is missing")]
        public void GetCurrencySettings_FileMissing_ReturnsNull()
        {
            Assert.Null(new FileDefineStorage(_paths).GetCurrencySettings());
        }

        [Fact]
        [DisplayName("SaveCurrencySettings writes a file that GetCurrencySettings reads back")]
        public void SaveCurrencySettings_ThenGet_RoundTrips()
        {
            var storage = new FileDefineStorage(_paths);
            var settings = new CurrencySettings { new CurrencyItem("TWD", 1m) };

            storage.SaveCurrencySettings(settings);
            var restored = storage.GetCurrencySettings();

            Assert.NotNull(restored);
            Assert.Equal(1m, restored!.GetRounding("TWD"));
        }

        [Fact]
        [DisplayName("GetUnitSettings returns null when the optional unit file is missing")]
        public void GetUnitSettings_FileMissing_ReturnsNull()
        {
            Assert.Null(new FileDefineStorage(_paths).GetUnitSettings());
        }

        [Fact]
        [DisplayName("SaveUnitSettings writes a file that GetUnitSettings reads back")]
        public void SaveUnitSettings_ThenGet_RoundTrips()
        {
            var storage = new FileDefineStorage(_paths);
            var settings = new UnitSettings { new UnitItem("KG", 3) };

            storage.SaveUnitSettings(settings);
            var restored = storage.GetUnitSettings();

            Assert.NotNull(restored);
            Assert.Equal(3, restored!.GetDecimals("KG"));
        }

        [Fact]
        [DisplayName("GetPluginSettings returns null when the optional plugin file is missing")]
        public void GetPluginSettings_FileMissing_ReturnsNull()
        {
            Assert.Null(new FileDefineStorage(_paths).GetPluginSettings());
        }

        [Fact]
        [DisplayName("SavePluginSettings writes a file that GetPluginSettings reads back")]
        public void SavePluginSettings_ThenGet_RoundTrips()
        {
            var storage = new FileDefineStorage(_paths);
            var settings = new PluginSettings();
            settings.Items!.Add(new ProgramPluginItem("Order"));

            storage.SavePluginSettings(settings);
            var restored = storage.GetPluginSettings();

            Assert.NotNull(restored);
            Assert.NotNull(restored!.Items!.GetOrDefault("Order"));
        }

        [Theory]
        [InlineData(DefineType.DbCategorySettings)]
        [InlineData(DefineType.CurrencySettings)]
        [InlineData(DefineType.UnitSettings)]
        [InlineData(DefineType.ProgramSettings)]
        [InlineData(DefineType.MenuSettings)]
        [InlineData(DefineType.PluginSettings)]
        [DisplayName("GetChangeSource reports one settings file for each keyless settings type")]
        public void GetChangeSource_SettingsTypes_ReportsOneFile(DefineType defineType)
        {
            var source = new FileDefineStorage(_paths).GetChangeSource(defineType);

            Assert.NotNull(source.FilePaths);
            Assert.Single(source.FilePaths!);
        }

        [Fact]
        [DisplayName("GetChangeSource reports the same files the getters read for the keyed types")]
        public void GetChangeSource_KeyedTypes_ReportsGetterPaths()
        {
            var storage = new FileDefineStorage(_paths);

            Assert.Equal(new[] { _paths.GetFormSchemaFilePath("Order") },
                storage.GetChangeSource(DefineType.FormSchema, "Order").FilePaths);
            Assert.Equal(new[] { _paths.GetFormLayoutFilePath("Order") },
                storage.GetChangeSource(DefineType.FormLayout, "Order").FilePaths);
            Assert.Equal(new[] { _paths.GetTableSchemaFilePath("company", "ft_order") },
                storage.GetChangeSource(DefineType.TableSchema, "company", "ft_order").FilePaths);
            Assert.Equal(new[] { _paths.GetLanguageFilePath("zh-TW", "Order") },
                storage.GetChangeSource(DefineType.Language, "zh-TW", "Order").FilePaths);
        }

        [Theory]
        [InlineData(DefineType.FormSchema)]
        [InlineData(DefineType.FormLayout)]
        [InlineData(DefineType.TableSchema)]
        [InlineData(DefineType.Language)]
        [DisplayName("GetChangeSource reports no signal for a keyed type whose keys are missing")]
        public void GetChangeSource_KeyedTypeWithoutKeys_ReturnsNone(DefineType defineType)
        {
            Assert.Equal(DefineChangeSource.None, new FileDefineStorage(_paths).GetChangeSource(defineType));
        }

        [Fact]
        [DisplayName("GetChangeSource reports no signal for a type the file storage does not own")]
        public void GetChangeSource_UnownedType_ReturnsNone()
        {
            Assert.Equal(DefineChangeSource.None,
                new FileDefineStorage(_paths).GetChangeSource(DefineType.SystemSettings));
        }

        [Fact]
        [DisplayName("GetChangeSource with only a category key reports no signal for a two-key type")]
        public void GetChangeSource_TableSchemaOneKey_ReturnsNone()
        {
            Assert.Equal(DefineChangeSource.None,
                new FileDefineStorage(_paths).GetChangeSource(DefineType.TableSchema, "company"));
        }
    }
}
