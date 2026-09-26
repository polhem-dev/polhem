using System.ComponentModel;
using Avalonia.Media;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// Behaviour checks for <see cref="FieldCaptionStyle"/>: required captions are blue, read-only
    /// captions stay the theme default (their cue is the parenthesised caption / editor underline),
    /// and read-only suppresses the required colour.
    /// </summary>
    public class FieldCaptionStyleTests
    {
        [Fact]
        [DisplayName("An ordinary field caption (neither read-only nor required) is not colored")]
        public void GetCaptionForeground_Normal_ReturnsNull()
        {
            Assert.Null(FieldCaptionStyle.GetCaptionForeground(readOnly: false, required: false));
        }

        [Fact]
        [DisplayName("A read-only field caption is not colored (parentheses mark it instead)")]
        public void GetCaptionForeground_ReadOnly_ReturnsNull()
        {
            Assert.Null(FieldCaptionStyle.GetCaptionForeground(readOnly: true, required: false));
        }

        [Fact]
        [DisplayName("A required field caption is blue")]
        public void GetCaptionForeground_Required_ReturnsBlue()
        {
            var brush = Assert.IsType<ISolidColorBrush>(
                FieldCaptionStyle.GetCaptionForeground(readOnly: false, required: true), exactMatch: false);
            Assert.Equal(Color.FromRgb(0x25, 0x63, 0xEB), brush.Color);
        }

        [Fact]
        [DisplayName("Read-only takes precedence over required (the required blue is not applied)")]
        public void GetCaptionForeground_ReadOnlyAndRequired_ReturnsNull()
        {
            Assert.Null(FieldCaptionStyle.GetCaptionForeground(readOnly: true, required: true));
        }

        [Fact]
        [DisplayName("A read-only field caption is wrapped in parentheses, for example Amount becomes (Amount)")]
        public void FormatCaption_ReadOnly_WrapsInParentheses()
        {
            Assert.Equal("(Amount)", FieldCaptionStyle.FormatCaption("Amount", readOnly: true));
        }

        [Fact]
        [DisplayName("An editable field caption stays as is")]
        public void FormatCaption_Editable_ReturnsPlain()
        {
            Assert.Equal("Amount", FieldCaptionStyle.FormatCaption("Amount", readOnly: false));
        }

        [Fact]
        [DisplayName("A read-only field with an empty caption gets no parentheses")]
        public void FormatCaption_ReadOnlyEmptyCaption_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, FieldCaptionStyle.FormatCaption(string.Empty, readOnly: true));
        }
    }
}
