using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Polhem.UI.Avalonia.Controls.Editors;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Dialogs
{
    /// <summary>
    /// <see cref="OverlayDialogHost.ShowAsync"/>: the card it adds to the overlay layer, its theme colors and title, and
    /// its removal once the dialog completes. The placement arithmetic is covered by <c>OverlayDialogHostTests</c>.
    /// </summary>
    public class OverlayDialogHostShowTests
    {
        [Fact]
        [DisplayName("ShowAsync adds a dimmed backdrop holding the card and removes it when the dialog completes")]
        public void ShowAsync_UntilCompleted_KeepsBackdropOnOverlay()
        {
            using var scope = new InlineContinuationScope();
            var host = new OverlayTestHost();
            var content = new TextBlock { Text = "body" };
            var completion = new TaskCompletionSource();

            var task = OverlayDialogHost.ShowAsync(host.Host, content, "Title", completion.Task);

            Assert.False(task.IsCompleted);
            Assert.NotNull(host.Backdrop.Background);
            Assert.Equal("Title", host.Title);
            Assert.Same(content, host.Panel<TextBlock>());
            completion.SetResult();
            InlineContinuationScope.Completed(task);
            Assert.Empty(host.Overlay.Children);
        }

        [Fact]
        [DisplayName("A dialog without a title shows only its panel in the card")]
        public void ShowAsync_NoTitle_ShowsPanelOnly()
        {
            using var scope = new InlineContinuationScope();
            var host = new OverlayTestHost();
            var completion = new TaskCompletionSource();

            var task = OverlayDialogHost.ShowAsync(host.Host, new Border(), null, completion.Task);

            Assert.Null(host.Title);
            Assert.Single(Assert.IsType<DockPanel>(host.Card.Child).Children);
            completion.SetResult();
            InlineContinuationScope.Completed(task);
        }

        [Fact]
        [DisplayName("The card uses the light surface under a light theme")]
        public void ShowAsync_LightTheme_UsesLightSurface()
        {
            using var scope = new InlineContinuationScope();
            var host = new OverlayTestHost();
            var completion = new TaskCompletionSource();

            var task = OverlayDialogHost.ShowAsync(host.Host, new Border(), "T", completion.Task);

            Assert.Equal(Color.FromRgb(0xFF, 0xFF, 0xFF), Assert.IsType<SolidColorBrush>(host.Card.Background).Color);
            completion.SetResult();
            InlineContinuationScope.Completed(task);
        }

        [Fact]
        [DisplayName("The card uses the dark surface under a dark theme")]
        public void ShowAsync_DarkTheme_UsesDarkSurface()
        {
            using var scope = new InlineContinuationScope();
            var host = new OverlayTestHost(new ThemeVariantScope { RequestedThemeVariant = ThemeVariant.Dark });
            var completion = new TaskCompletionSource();

            var task = OverlayDialogHost.ShowAsync(host.Host, new Border(), "T", completion.Task);

            Assert.Equal(Color.FromRgb(0x2A, 0x2B, 0x33), Assert.IsType<SolidColorBrush>(host.Card.Background).Color);
            completion.SetResult();
            InlineContinuationScope.Completed(task);
        }

        [Fact]
        [DisplayName("A failed completion still removes the card and passes the failure on")]
        public void ShowAsync_FaultedCompletion_RemovesCardAndRethrows()
        {
            using var scope = new InlineContinuationScope();
            var host = new OverlayTestHost();
            var completion = new TaskCompletionSource();

            var task = OverlayDialogHost.ShowAsync(host.Host, new Border(), "T", completion.Task);
            completion.SetException(new InvalidOperationException("boom"));

            Assert.Throws<InvalidOperationException>(() => InlineContinuationScope.Completed(task));
            Assert.Empty(host.Overlay.Children);
        }

        [Fact]
        [DisplayName("A host with no overlay layer is rejected")]
        public void ShowAsync_NoOverlayLayer_Throws()
        {
            var task = OverlayDialogHost.ShowAsync(new Border(), new Border(), "T", Task.CompletedTask);

            Assert.IsType<InvalidOperationException>(task.Exception?.InnerException);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [DisplayName("Null arguments are rejected")]
        public void ShowAsync_NullArgument_Throws(int nullArgument)
        {
            var host = new OverlayTestHost();

            var task = OverlayDialogHost.ShowAsync(
                nullArgument == 0 ? null! : host.Host,
                nullArgument == 1 ? null! : new Border(),
                "T",
                nullArgument == 2 ? null! : Task.CompletedTask);

            Assert.IsType<ArgumentNullException>(task.Exception?.InnerException);
        }
    }
}
