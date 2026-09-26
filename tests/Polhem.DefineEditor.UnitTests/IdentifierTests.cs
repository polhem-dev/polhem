using System.ComponentModel;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// Pins the names the editor uses to find its own resources and its per-user settings.
    /// </summary>
    /// <remarks>
    /// Both are strings the compiler does not check. A wrong resource base name leaves every label
    /// showing its key; a changed settings folder makes the editor start from defaults and ignore the
    /// settings users already have.
    /// </remarks>
    public class IdentifierTests
    {
        [Fact]
        [DisplayName("LocalizationService finds the embedded string resources")]
        public void Localization_ResolvesEmbeddedStrings()
        {
            // The indexer falls back to the key when a lookup finds nothing.
            Assert.NotEqual("Menu_File", LocalizationService.Current["Menu_File"]);
        }

        [Fact]
        [DisplayName("The user settings file lives in the Polhem.DefineEditor folder")]
        public void UserSettings_LiveInPolhemDefineEditorFolder()
        {
            var folder = Path.GetFileName(Path.GetDirectoryName(UserSettings.GetConfigPath()));

            Assert.Equal("Polhem.DefineEditor", folder);
        }
    }
}
