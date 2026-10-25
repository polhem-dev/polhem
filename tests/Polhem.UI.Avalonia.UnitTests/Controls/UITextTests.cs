using System.ComponentModel;
using Polhem.Definition.Language;
using Polhem.Tests.Shared;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// Tests that the built-in controls' own text (<see cref="UIText"/>) follows the user's culture.
    /// </summary>
    public class UITextTests
    {
        [Fact]
        [DisplayName("The built-in controls' text follows the UI culture and falls back to English")]
        public void UIText_FollowsUICulture()
        {
            using (new CultureScope("zh-TW"))
            {
                Assert.Equal("儲存", UIText.Get(PolhemUIText.Save));
            }
            using (new CultureScope("en-US"))
            {
                Assert.Equal("Save", UIText.Get(PolhemUIText.Save));
            }
        }
    }
}
