using System.ComponentModel;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Tests for path composition and path traversal protection in <see cref="CustomizeOnlyPathOptions"/>.
    /// </summary>
    public class CustomizeOnlyPathOptionsTests
    {
        private const string CustomizeRoot = "/tmp/polhem-customize-tests";
        private const string CustomizeId = "acme";

        [Fact]
        [DisplayName("GetProgramSettingsFilePath resolves to {CustomizePath}/{customizeId}/ProgramSettings.xml")]
        public void GetProgramSettingsFilePath_ReturnsCustomizeRootedPath()
        {
            var paths = new CustomizeOnlyPathOptions(CustomizeRoot, CustomizeId);
            var expected = Path.Combine(Path.GetFullPath(Path.Combine(CustomizeRoot, CustomizeId)), "ProgramSettings.xml");
            Assert.Equal(expected, paths.GetProgramSettingsFilePath());
        }

        [Fact]
        [DisplayName("GetFormLayoutFilePath resolves to {CustomizePath}/{customizeId}/FormLayout/<layoutId>.FormLayout.xml")]
        public void GetFormLayoutFilePath_ReturnsCustomizeRootedPath()
        {
            var paths = new CustomizeOnlyPathOptions(CustomizeRoot, CustomizeId);
            var root = Path.GetFullPath(Path.Combine(CustomizeRoot, CustomizeId));
            var expected = Path.Combine(root, "FormLayout", "EmployeeDefault.FormLayout.xml");
            Assert.Equal(expected, paths.GetFormLayoutFilePath("EmployeeDefault"));
        }

        [Fact]
        [DisplayName("GetLanguageFilePath resolves to {CustomizePath}/{customizeId}/Language/<lang>/<ns>.Language.xml")]
        public void GetLanguageFilePath_ReturnsCustomizeRootedPath()
        {
            var paths = new CustomizeOnlyPathOptions(CustomizeRoot, CustomizeId);
            var root = Path.GetFullPath(Path.Combine(CustomizeRoot, CustomizeId));
            var expected = Path.Combine(root, "Language", "zh-TW", "Customer.Language.xml");
            Assert.Equal(expected, paths.GetLanguageFilePath("zh-TW", "Customer"));
        }

        [Theory]
        [InlineData("..")]
        [InlineData("../escape")]
        [InlineData("foo/bar")]
        [InlineData("foo\\bar")]
        [DisplayName("A customizeId containing path traversal characters throws ArgumentException")]
        public void Constructor_CustomizeIdWithPathTraversal_ThrowsArgumentException(string customizeId)
        {
            Assert.Throws<ArgumentException>(() => new CustomizeOnlyPathOptions(CustomizeRoot, customizeId));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("An empty or whitespace customizeId throws ArgumentException")]
        public void Constructor_EmptyCustomizeId_ThrowsArgumentException(string customizeId)
        {
            Assert.Throws<ArgumentException>(() => new CustomizeOnlyPathOptions(CustomizeRoot, customizeId));
        }

        [Fact]
        [DisplayName("An empty customizePath throws ArgumentException")]
        public void Constructor_EmptyCustomizePath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new CustomizeOnlyPathOptions("", CustomizeId));
        }
    }
}
