using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Settings;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// <see cref="CustomizeDefineReader"/> for the menu override, which must tell "no override" apart from "an empty
    /// menu", and the customization guard of the menu and plugin overrides.
    /// </summary>
    public sealed class CustomizeDefineReaderMenuTests : IDisposable
    {
        private readonly string _root;
        private readonly string _customizeId = "custm" + Guid.NewGuid().ToString("N");

        public CustomizeDefineReaderMenuTests()
        {
            _root = Path.Combine(Path.GetTempPath(), $"polhem-custmenu-{Guid.NewGuid():N}");
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

        [Fact]
        [DisplayName("GetCustomizeMenuSettings returns the tenant's menu when the override file exists")]
        public void GetCustomizeMenuSettings_FileExists_ReturnsOverride()
        {
            var paths = new CustomizeOnlyPathOptions(_root, _customizeId);
            XmlCodec.SerializeToFile(new MenuSettings(), paths.GetMenuSettingsFilePath());

            Assert.NotNull(CreateReader().GetCustomizeMenuSettings(_customizeId));
        }

        [Fact]
        [DisplayName("GetCustomizeMenuSettings returns null when the tenant has no menu override")]
        public void GetCustomizeMenuSettings_FileMissing_ReturnsNull()
        {
            Assert.Null(CreateReader().GetCustomizeMenuSettings(_customizeId));
        }

        [Fact]
        [DisplayName("GetCustomizeMenuSettings returns null for an empty customization code")]
        public void GetCustomizeMenuSettings_EmptyCustomizeId_ReturnsNull()
        {
            Assert.Null(CreateReader().GetCustomizeMenuSettings(string.Empty));
        }

        [Fact]
        [DisplayName("GetCustomizePluginSettings returns null when no customization root is configured")]
        public void GetCustomizePluginSettings_NoCustomizePath_ReturnsNull()
        {
            Assert.Null(CreateReader(customizePath: string.Empty).GetCustomizePluginSettings(_customizeId));
        }

        [Fact]
        [DisplayName("The constructor refuses null path options")]
        public void Constructor_NullPaths_Throws()
        {
            var paths = new PathOptions { DefinePath = "/tmp/base" };

            Assert.Throws<ArgumentNullException>(() => new CustomizeDefineReader(new CacheContainerProvider(paths), null!));
        }
    }
}
