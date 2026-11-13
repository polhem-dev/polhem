using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.Definition.UnitTests.Storage
{
    /// <summary>
    /// Tests for the settings members of <see cref="CustomizeOnlyStorage"/>: the customizable settings files are read
    /// when present and reported as null when absent, and every other settings member refuses.
    /// </summary>
    public sealed class CustomizeOnlyStorageSettingsTests : IDisposable
    {
        private const string CustomizeId = "acme";
        private readonly string _root;
        private readonly CustomizeOnlyPathOptions _paths;

        public CustomizeOnlyStorageSettingsTests()
        {
            _root = Path.Combine(Path.GetTempPath(), $"polhem-custsettings-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
            _paths = new CustomizeOnlyPathOptions(_root, CustomizeId);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        private CustomizeOnlyStorage CreateStorage() => new(_paths);

        [Fact]
        [DisplayName("GetProgramSettings returns null when the tenant provides no override")]
        public void GetProgramSettings_FileMissing_ReturnsNull()
        {
            Assert.Null(CreateStorage().GetProgramSettings());
        }

        [Fact]
        [DisplayName("GetProgramSettings reads the tenant's override file when it exists")]
        public void GetProgramSettings_FileExists_ReturnsSettings()
        {
            XmlCodec.SerializeToFile(new ProgramSettings(), _paths.GetProgramSettingsFilePath());

            Assert.NotNull(CreateStorage().GetProgramSettings());
        }

        [Fact]
        [DisplayName("GetMenuSettings returns null when the tenant provides no override")]
        public void GetMenuSettings_FileMissing_ReturnsNull()
        {
            Assert.Null(CreateStorage().GetMenuSettings());
        }

        [Fact]
        [DisplayName("GetMenuSettings reads and validates the tenant's override file when it exists")]
        public void GetMenuSettings_FileExists_ReturnsSettings()
        {
            XmlCodec.SerializeToFile(new MenuSettings(), _paths.GetMenuSettingsFilePath());

            Assert.NotNull(CreateStorage().GetMenuSettings());
        }

        [Fact]
        [DisplayName("GetPluginSettings returns null when the tenant provides no override")]
        public void GetPluginSettings_FileMissing_ReturnsNull()
        {
            Assert.Null(CreateStorage().GetPluginSettings());
        }

        [Fact]
        [DisplayName("GetPluginSettings reads the tenant's override file when it exists")]
        public void GetPluginSettings_FileExists_ReturnsSettings()
        {
            var settings = new PluginSettings();
            settings.Items!.Add(new ProgramPluginItem("Order"));
            XmlCodec.SerializeToFile(settings, _paths.GetPluginSettingsFilePath());

            var restored = CreateStorage().GetPluginSettings();

            Assert.NotNull(restored);
            Assert.NotNull(restored!.Items!.GetOrDefault("Order"));
        }

        [Fact]
        [DisplayName("The currency and unit masters are not served by the override layer")]
        public void GetCurrencyAndUnitSettings_ThrowNotSupported()
        {
            var storage = CreateStorage();

            Assert.Throws<NotSupportedException>(() => storage.GetCurrencySettings());
            Assert.Throws<NotSupportedException>(() => storage.GetUnitSettings());
        }

        [Fact]
        [DisplayName("Every settings save throws NotSupportedException because the override layer is read-only")]
        public void SaveSettings_ThrowNotSupported()
        {
            var storage = CreateStorage();

            Assert.Throws<NotSupportedException>(() => storage.SaveCurrencySettings([]));
            Assert.Throws<NotSupportedException>(() => storage.SaveUnitSettings([]));
            Assert.Throws<NotSupportedException>(() => storage.SaveProgramSettings(new ProgramSettings()));
            Assert.Throws<NotSupportedException>(() => storage.SaveMenuSettings(new MenuSettings()));
            Assert.Throws<NotSupportedException>(() => storage.SavePluginSettings(new PluginSettings()));
        }

        [Fact]
        [DisplayName("GetChangeSource reports the menu and plugin override files the getters read")]
        public void GetChangeSource_MenuAndPluginSettings_ReportGetterPaths()
        {
            var storage = CreateStorage();

            Assert.Equal(new[] { _paths.GetMenuSettingsFilePath() },
                storage.GetChangeSource(DefineType.MenuSettings).FilePaths);
            Assert.Equal(new[] { _paths.GetPluginSettingsFilePath() },
                storage.GetChangeSource(DefineType.PluginSettings).FilePaths);
        }
    }
}
