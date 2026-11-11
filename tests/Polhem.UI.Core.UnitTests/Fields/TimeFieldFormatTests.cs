using System.ComponentModel;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Core.UnitTests.Fields
{
    /// <summary>
    /// The value a time editor writes back, the rule both UI heads share.
    /// </summary>
    public class TimeFieldFormatTests
    {
        [Theory]
        [InlineData("", "09:30", "")]
        [InlineData("   ", "09:30", "")]
        [InlineData(null, "09:30", "")]
        [DisplayName("An emptied box writes back an empty string, the stored form of an unset time")]
        public void ResolveWriteBack_EmptyText_Unsets(string? text, string current, string expected)
        {
            Assert.Equal(expected, TimeFieldFormat.ResolveWriteBack(text, current));
        }

        [Fact]
        [DisplayName("Text that parses as a time writes back its normalised HH:mm form")]
        public void ResolveWriteBack_ValidText_IsNormalised()
        {
            Assert.Equal("07:05", TimeFieldFormat.ResolveWriteBack("7:05", "09:30"));
        }

        [Fact]
        [DisplayName("Text that does not parse keeps the current value")]
        public void ResolveWriteBack_InvalidText_KeepsCurrent()
        {
            Assert.Equal("09:30", TimeFieldFormat.ResolveWriteBack("not a time", "09:30"));
        }
    }
}
