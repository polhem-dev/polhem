using System.ComponentModel;
using Polhem.Definition.Language;
using Polhem.UI.Avalonia.Controls;
using Polhem.Tests.Shared;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// Tests that display text follows the user's culture: <see cref="CellValueFormatter"/> for
    /// numbers and dates, and <see cref="UIText"/> for the built-in controls' own text.
    /// </summary>
    public class CellValueFormatterTests
    {
        [Fact]
        [DisplayName("A number format renders with the user's culture separators")]
        public void Format_NumberFormat_UsesCurrentCulture()
        {
            using (new CultureScope("de-DE"))
            {
                Assert.Equal("1.234,50", CellValueFormatter.Format(1234.5m, string.Empty, "N2"));
            }
            using (new CultureScope("en-US"))
            {
                Assert.Equal("1,234.50", CellValueFormatter.Format(1234.5m, string.Empty, "N2"));
            }
        }

        [Fact]
        [DisplayName("A date renders with the user's culture short date pattern")]
        public void Format_Date_UsesCultureShortDate()
        {
            var date = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Unspecified);
            using (new CultureScope("de-DE"))
            {
                Assert.Equal("15.01.2026", CellValueFormatter.Format(date, string.Empty, string.Empty));
            }
            using (new CultureScope("en-US"))
            {
                Assert.Equal("1/15/2026", CellValueFormatter.Format(date, string.Empty, string.Empty));
            }
        }

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
