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
        [DisplayName("LocalizationService 找得到內嵌的字串資源")]
        public void Localization_ResolvesEmbeddedStrings()
        {
            // The indexer falls back to the key when a lookup finds nothing.
            Assert.NotEqual("Menu_File", LocalizationService.Current["Menu_File"]);
        }

        [Fact]
        [DisplayName("使用者設定檔位於 Polhem.DefineEditor 資料夾")]
        public void UserSettings_LiveInPolhemDefineEditorFolder()
        {
            var folder = Path.GetFileName(Path.GetDirectoryName(UserSettings.GetConfigPath()));

            Assert.Equal("Polhem.DefineEditor", folder);
        }
    }
}
