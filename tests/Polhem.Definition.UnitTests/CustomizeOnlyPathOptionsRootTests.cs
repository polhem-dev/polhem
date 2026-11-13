using System.ComponentModel;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Tests for how <see cref="CustomizeOnlyPathOptions"/> confines a customization code to a directory directly
    /// under the customization root.
    /// </summary>
    public class CustomizeOnlyPathOptionsRootTests
    {
        [Fact]
        [DisplayName("A customization code of a single dot resolves to the root itself and is rejected")]
        public void Constructor_DotCustomizeId_ThrowsArgumentException()
        {
            string root = Path.Combine(Path.GetTempPath(), "polhem-cust-root");

            Assert.Throws<ArgumentException>(() => new CustomizeOnlyPathOptions(root, "."));
        }

        [Fact]
        [DisplayName("A customization root that already ends with a separator still yields paths under the customization code")]
        public void Constructor_RootWithTrailingSeparator_ResolvesUnderCustomizeId()
        {
            string root = Path.Combine(Path.GetTempPath(), "polhem-cust-root") + Path.DirectorySeparatorChar;

            var paths = new CustomizeOnlyPathOptions(root, "acme");

            Assert.Equal(Path.Combine(Path.GetFullPath(root), "acme", "MenuSettings.xml"), paths.GetMenuSettingsFilePath());
            Assert.Equal(Path.Combine(Path.GetFullPath(root), "acme", "PluginSettings.xml"), paths.GetPluginSettingsFilePath());
        }
    }
}
