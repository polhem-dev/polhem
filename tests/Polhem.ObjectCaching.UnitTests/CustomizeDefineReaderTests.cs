using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Behavior tests of <see cref="CustomizeDefineReader"/>: an existing customized file is returned; a missing
    /// one returns null; an unset CustomizePath or an empty customizeId returns null for everything (the second
    /// line of defense); tenants are isolated from each other.
    /// </summary>
    public sealed class CustomizeDefineReaderTests : IDisposable
    {
        private readonly string _root;
        // Unique per-test customization codes keep the prefixed cache entries isolated across
        // the shared process-wide cache provider, so no test sees another's entries.
        private readonly string _customizeId = "cust" + Guid.NewGuid().ToString("N");

        public CustomizeDefineReaderTests()
        {
            _root = Path.Combine(Path.GetTempPath(), $"polhem-custreader-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
        }

        private CustomizeDefineReader CreateReader(string? customizePath = null)
        {
            var paths = new PathOptions { DefinePath = "/tmp/base", CustomizePath = customizePath ?? _root };
            return new CustomizeDefineReader(new CacheContainerProvider(paths), paths);
        }

        private void WriteCustomizeFormLayout(string customizeId, string layoutId)
        {
            var paths = new CustomizeOnlyPathOptions(_root, customizeId);
            XmlCodec.SerializeToFile(new FormLayout { LayoutId = layoutId }, paths.GetFormLayoutFilePath(layoutId));
        }

        private void WriteCustomizeLanguage(string customizeId, string lang, string ns)
        {
            var paths = new CustomizeOnlyPathOptions(_root, customizeId);
            XmlCodec.SerializeToFile(new LanguageResource { Lang = lang, Namespace = ns }, paths.GetLanguageFilePath(lang, ns));
        }

        private void WriteCustomizeProgramSettings(string customizeId)
        {
            var paths = new CustomizeOnlyPathOptions(_root, customizeId);
            XmlCodec.SerializeToFile(new ProgramSettings(), paths.GetProgramSettingsFilePath());
        }

        private void WriteCustomizePluginSettings(string customizeId, string progId, string pluginType)
        {
            var paths = new CustomizeOnlyPathOptions(_root, customizeId);
            var settings = new PluginSettings();
            settings.Items!.Add(progId).Plugins!.Add(pluginType, PluginStage.BeforeSave);
            XmlCodec.SerializeToFile(settings, paths.GetPluginSettingsFilePath());
        }

        [Fact]
        [DisplayName("GetCustomizeFormLayout returns the customized object when the customized file exists")]
        public void GetCustomizeFormLayout_FileExists_ReturnsCustomize()
        {
            WriteCustomizeFormLayout(_customizeId, "EmployeeDefault");
            var reader = CreateReader();

            var result = reader.GetCustomizeFormLayout(_customizeId, "EmployeeDefault");

            Assert.NotNull(result);
            Assert.Equal("EmployeeDefault", result!.LayoutId);
        }

        [Fact]
        [DisplayName("GetCustomizeFormLayout returns null when the customized file does not exist")]
        public void GetCustomizeFormLayout_FileMissing_ReturnsNull()
        {
            Assert.Null(CreateReader().GetCustomizeFormLayout(_customizeId, "NonExistent"));
        }

        [Fact]
        [DisplayName("GetCustomizeLanguage returns the customized object when the customized file exists")]
        public void GetCustomizeLanguage_FileExists_ReturnsCustomize()
        {
            WriteCustomizeLanguage(_customizeId, "zh-TW", "Customer");
            var reader = CreateReader();

            var result = reader.GetCustomizeLanguage(_customizeId, "zh-TW", "Customer");

            Assert.NotNull(result);
            Assert.Equal("Customer", result!.Namespace);
        }

        [Fact]
        [DisplayName("GetCustomizeLanguage returns null when the customized file does not exist")]
        public void GetCustomizeLanguage_FileMissing_ReturnsNull()
        {
            Assert.Null(CreateReader().GetCustomizeLanguage(_customizeId, "zh-TW", "NonExistent"));
        }

        [Fact]
        [DisplayName("GetCustomizeProgramSettings returns the customized object when the customized file exists")]
        public void GetCustomizeProgramSettings_FileExists_ReturnsCustomize()
        {
            WriteCustomizeProgramSettings(_customizeId);
            var reader = CreateReader();

            Assert.NotNull(reader.GetCustomizeProgramSettings(_customizeId));
        }

        [Fact]
        [DisplayName("GetCustomizeProgramSettings returns null without throwing when the customized file does not exist")]
        public void GetCustomizeProgramSettings_FileMissing_ReturnsNull()
        {
            Assert.Null(CreateReader().GetCustomizeProgramSettings(_customizeId));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [DisplayName("An empty customizeId returns null for FormLayout, Language and ProgramSettings (second line of defense)")]
        public void EmptyCustomizeId_AllReturnNull(string? customizeId)
        {
            WriteCustomizeFormLayout(_customizeId, "EmployeeDefault");
            var reader = CreateReader();

            Assert.Null(reader.GetCustomizeFormLayout(customizeId!, "EmployeeDefault"));
            Assert.Null(reader.GetCustomizeLanguage(customizeId!, "zh-TW", "Customer"));
            Assert.Null(reader.GetCustomizeProgramSettings(customizeId!));
        }

        [Fact]
        [DisplayName("An unset CustomizePath returns null for FormLayout, Language and ProgramSettings (customization off, backward compatible)")]
        public void EmptyCustomizePath_AllReturnNull()
        {
            WriteCustomizeFormLayout(_customizeId, "EmployeeDefault");
            var reader = CreateReader(customizePath: "");

            Assert.Null(reader.GetCustomizeFormLayout(_customizeId, "EmployeeDefault"));
            Assert.Null(reader.GetCustomizeLanguage(_customizeId, "zh-TW", "Customer"));
            Assert.Null(reader.GetCustomizeProgramSettings(_customizeId));
        }

        [Fact]
        [DisplayName("Tenants are isolated: A's customization does not affect B's lookup")]
        public void CrossTenant_Isolated()
        {
            string custA = "a" + Guid.NewGuid().ToString("N");
            string custB = "b" + Guid.NewGuid().ToString("N");
            WriteCustomizeFormLayout(custA, "EmployeeDefault");
            var reader = CreateReader();

            Assert.NotNull(reader.GetCustomizeFormLayout(custA, "EmployeeDefault"));
            // B has no customized files and must get null. A's customization must not leak into B.
            Assert.Null(reader.GetCustomizeFormLayout(custB, "EmployeeDefault"));
        }

        [Fact]
        [DisplayName("GetCustomizePluginSettings reads the file under the customization path and returns that tenant's chain")]
        public void GetCustomizePluginSettings_FileExists_ReturnsCustomizeChain()
        {
            WriteCustomizePluginSettings(_customizeId, "Order", "Cust.CreditLimit, Cust");
            var reader = CreateReader();

            var settings = reader.GetCustomizePluginSettings(_customizeId);

            Assert.NotNull(settings);
            Assert.Equal(new PluginBinding("Cust.CreditLimit, Cust", PluginStage.BeforeSave),
                Assert.Single(settings!.GetPluginBindings("Order")));
        }

        [Fact]
        [DisplayName("GetCustomizePluginSettings returns null when the customized file does not exist instead of an empty instance")]
        public void GetCustomizePluginSettings_FileMissing_ReturnsNull()
        {
            // `PluginSettingsCache` returns an empty instance when the file is missing (the base layer wants that),
            // so the reader must check that the file exists before going to the cache. Otherwise "the tenant has no
            // customization" and "the tenant customized an empty chain" cannot be told apart.
            Assert.Null(CreateReader().GetCustomizePluginSettings(_customizeId));
        }

        [Fact]
        [DisplayName("A plugin override saved through CustomizeDefineWriter is visible to the next read even right after a miss")]
        public void GetCustomizePluginSettings_SavedByWriterAfterMiss_IsReadAtOnce()
        {
            var paths = new PathOptions { DefinePath = "/tmp/base", CustomizePath = _root };
            var provider = new CacheContainerProvider(paths);
            var reader = new CustomizeDefineReader(provider, paths);
            var writer = new CustomizeDefineWriter(provider, paths);
            Assert.Null(reader.GetCustomizePluginSettings(_customizeId));

            var settings = new PluginSettings();
            settings.Items!.Add("Order").Plugins!.Add("Cust.CreditLimit, Cust", PluginStage.BeforeSave);
            writer.SaveCustomizePluginSettings(_customizeId, settings);

            var read = reader.GetCustomizePluginSettings(_customizeId);
            Assert.NotNull(read);
            Assert.Single(read!.GetPluginBindings("Order"));
        }

        [Fact]
        [DisplayName("Tenants are isolated: A's plugin customization does not leak into B")]
        public void CrossTenant_PluginSettings_Isolated()
        {
            string custA = "a" + Guid.NewGuid().ToString("N");
            string custB = "b" + Guid.NewGuid().ToString("N");
            WriteCustomizePluginSettings(custA, "Order", "A.OnlyForA, A");
            WriteCustomizePluginSettings(custB, "Order", "B.OnlyForB, B");
            var reader = CreateReader();

            Assert.Equal("A.OnlyForA, A",
                Assert.Single(reader.GetCustomizePluginSettings(custA)!.GetPluginBindings("Order")).Type);
            Assert.Equal("B.OnlyForB, B",
                Assert.Single(reader.GetCustomizePluginSettings(custB)!.GetPluginBindings("Order")).Type);
        }

        [Fact]
        [DisplayName("The customized file lives at {CustomizePath}/{customizeId}/PluginSettings.xml, separate from the base path")]
        public void CustomizePluginSettings_LivesUnderTheCustomizeRoot()
        {
            WriteCustomizePluginSettings(_customizeId, "Order", "Cust.CreditLimit, Cust");

            string expected = Path.Combine(_root, _customizeId, "PluginSettings.xml");
            Assert.True(File.Exists(expected));

            // Customizing writes nothing to the base path.
            Assert.False(File.Exists(new PathOptions { DefinePath = _root }.GetPluginSettingsFilePath()));
        }

        [Fact]
        [DisplayName("Repeated lookups return the same cached instance reference (it is not rebuilt each time)")]
        public void RepeatedLookup_ReturnsStableCachedInstance()
        {
            WriteCustomizeFormLayout(_customizeId, "EmployeeDefault");
            var reader = CreateReader();

            var first = reader.GetCustomizeFormLayout(_customizeId, "EmployeeDefault");
            var second = reader.GetCustomizeFormLayout(_customizeId, "EmployeeDefault");

            Assert.Same(first, second);
        }

        [Fact]
        [DisplayName("Constructor throws ArgumentNullException for a null provider")]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new CustomizeDefineReader(null!, new PathOptions()));
        }
    }
}
