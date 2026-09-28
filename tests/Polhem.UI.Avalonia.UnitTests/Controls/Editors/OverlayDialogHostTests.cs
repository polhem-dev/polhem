using System.ComponentModel;
using Avalonia;
using Polhem.UI.Avalonia.Controls.Editors;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// The placement rules of <see cref="OverlayDialogHost"/>: the card stays inside the visible part of
    /// a phone screen, including the part left above the on-screen keyboard.
    /// </summary>
    public class OverlayDialogHostTests
    {
        // An iPhone 17 portrait screen in device-independent units, with its safe area.
        private static readonly Size s_phone = new(402, 874);
        private static readonly Thickness s_phoneSafeArea = new(0, 62, 0, 34);

        [Fact]
        [DisplayName("On a phone the card fits between the screen edges, even when its panel prefers a wider card")]
        public void ComputeLayout_PhoneWidth_CardFitsInsideScreen()
        {
            var layout = OverlayDialogHost.ComputeLayout(s_phone, s_phoneSafeArea, keyboardInset: 0,
                minCardWidth: LookupPanel.PreferredMinWidth);

            var available = s_phone.Width - 2 * OverlayDialogHost.Gutter;
            Assert.Equal(available, layout.MaxWidth);
            Assert.True(layout.MinWidth <= layout.MaxWidth);
            Assert.Equal(OverlayDialogHost.Gutter, layout.Padding.Left);
            Assert.Equal(OverlayDialogHost.Gutter, layout.Padding.Right);
        }

        [Fact]
        [DisplayName("On a wide screen the card keeps its panel's preferred width and its maximum")]
        public void ComputeLayout_WideScreen_KeepsPreferredAndMaximumWidth()
        {
            var layout = OverlayDialogHost.ComputeLayout(new Size(1280, 800), default, keyboardInset: 0,
                minCardWidth: LookupPanel.PreferredMinWidth);

            Assert.Equal(LookupPanel.PreferredMinWidth, layout.MinWidth);
            Assert.Equal(OverlayDialogHost.MaxCardWidth, layout.MaxWidth);
        }

        [Fact]
        [DisplayName("An open keyboard moves the card's bottom limit above the keyboard")]
        public void ComputeLayout_KeyboardOpen_CardEndsAboveKeyboard()
        {
            const double keyboard = 336;
            var closed = OverlayDialogHost.ComputeLayout(s_phone, s_phoneSafeArea, keyboardInset: 0, minCardWidth: 0);
            var open = OverlayDialogHost.ComputeLayout(s_phone, s_phoneSafeArea, keyboard, minCardWidth: 0);

            Assert.Equal(keyboard + OverlayDialogHost.Gutter, open.Padding.Bottom);
            Assert.Equal(s_phone.Height - open.Padding.Top - keyboard - OverlayDialogHost.Gutter, open.MaxHeight);
            Assert.True(open.MaxHeight < closed.MaxHeight);
        }

        [Fact]
        [DisplayName("The safe area is kept clear at the top and bottom of the screen")]
        public void ComputeLayout_SafeArea_IsKeptClear()
        {
            var layout = OverlayDialogHost.ComputeLayout(s_phone, s_phoneSafeArea, keyboardInset: 0, minCardWidth: 0);

            Assert.Equal(s_phoneSafeArea.Top + OverlayDialogHost.Gutter, layout.Padding.Top);
            Assert.Equal(s_phoneSafeArea.Bottom + OverlayDialogHost.Gutter, layout.Padding.Bottom);
        }

        [Theory]
        [InlineData(874, 538, 336, 336)]  // pane inside the overlay: measured from its top edge
        [InlineData(874, 0, 336, 336)]    // pane without a usable position: its height
        [InlineData(874, 0, 0, 0)]        // closed pane
        [InlineData(874, 0, 2000, 874)]   // never more than the overlay
        [DisplayName("ComputeKeyboardInset measures how far the keyboard reaches up from the bottom edge")]
        public void ComputeKeyboardInset_ReturnsCoveredHeight(double overlayHeight, double top, double height, double expected)
        {
            var inset = OverlayDialogHost.ComputeKeyboardInset(overlayHeight, new Rect(0, top, 402, height));

            Assert.Equal(expected, inset);
        }
    }
}
