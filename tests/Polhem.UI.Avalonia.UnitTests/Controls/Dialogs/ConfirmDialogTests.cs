using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Polhem.UI.Avalonia.Controls.Editors;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Dialogs
{
    /// <summary>
    /// <see cref="ConfirmDialog"/> on a host without native windows: the question is shown on the overlay layer, and the
    /// answer is whichever button the user chose.
    /// </summary>
    public class ConfirmDialogTests
    {
        private static Button FindButton(ConfirmPanel panel, string caption)
        {
            var body = Assert.IsType<DockPanel>(panel.Content);
            var buttons = Assert.IsType<StackPanel>(body.Children[0]);
            return buttons.Children.OfType<Button>().Single(b => Equals(b.Content, caption));
        }

        [Fact]
        [DisplayName("ShowAsync shows the question as an overlay card titled with the given title")]
        public void ShowAsync_NoWindow_ShowsOverlayCardWithTitle()
        {
            using var scope = new InlineContinuationScope();
            var host = new OverlayTestHost();

            var task = ConfirmDialog.ShowAsync(host.Host, "Delete record", "Delete it?", "Delete", "Keep");

            Assert.False(task.IsCompleted);
            Assert.Equal("Delete record", host.Title);
            host.Panel<ConfirmPanel>().Cancel();
        }

        [Fact]
        [DisplayName("Choosing the confirm button returns true and removes the overlay card")]
        public void ShowAsync_Confirmed_ReturnsTrue()
        {
            using var scope = new InlineContinuationScope();
            var host = new OverlayTestHost();
            var task = ConfirmDialog.ShowAsync(host.Host, "Delete record", "Delete it?", "Delete", "Keep");

            FindButton(host.Panel<ConfirmPanel>(), "Delete").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.True(InlineContinuationScope.Completed(task));
            Assert.Empty(host.Overlay.Children);
        }

        [Fact]
        [DisplayName("Choosing the cancel button returns false and removes the overlay card")]
        public void ShowAsync_Cancelled_ReturnsFalse()
        {
            using var scope = new InlineContinuationScope();
            var host = new OverlayTestHost();
            var task = ConfirmDialog.ShowAsync(host.Host, "Delete record", "Delete it?", "Delete", "Keep");

            FindButton(host.Panel<ConfirmPanel>(), "Keep").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.False(InlineContinuationScope.Completed(task));
            Assert.Empty(host.Overlay.Children);
        }

        [Fact]
        [DisplayName("A token cancelled before the call throws without showing anything")]
        public void ShowAsync_AlreadyCancelled_Throws()
        {
            using var scope = new InlineContinuationScope();
            var host = new OverlayTestHost();

            var task = ConfirmDialog.ShowAsync(host.Host, "T", "M", "Yes", "No", new CancellationToken(canceled: true));

            Assert.True(task.IsCanceled);
            Assert.Empty(host.Overlay.Children);
        }

        [Fact]
        [DisplayName("A null host is rejected")]
        public void ShowAsync_NullHost_Throws()
        {
            var task = ConfirmDialog.ShowAsync(null!, "T", "M", "Yes", "No");

            Assert.IsType<ArgumentNullException>(task.Exception?.InnerException);
        }
    }
}
