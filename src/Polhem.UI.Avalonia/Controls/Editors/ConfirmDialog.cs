using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Polhem.UI.Avalonia.Controls.Editors
{
    /// <summary>
    /// Asks the user a yes-or-no question in a modal dialog: a native window on the desktop, the overlay
    /// host where the platform has no windows (see <see cref="DialogHosting"/>).
    /// </summary>
    internal static class ConfirmDialog
    {
        /// <summary>
        /// Shows the question and returns whether the user went ahead. Closing the window counts as backing out.
        /// </summary>
        /// <param name="host">A visual inside the owning top level.</param>
        /// <param name="title">The dialog's title.</param>
        /// <param name="message">The question to ask.</param>
        /// <param name="confirmText">The caption of the button that goes ahead.</param>
        /// <param name="cancelText">The caption of the button that backs out.</param>
        /// <param name="cancellationToken">
        /// A token that closes an open dialog as if the user had backed out. A cancelled call throws
        /// <see cref="OperationCanceledException"/>.
        /// </param>
        public static async Task<bool> ShowAsync(Visual host, string title, string message, string confirmText,
            string cancelText, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(host);
            cancellationToken.ThrowIfCancellationRequested();

            var panel = new ConfirmPanel(message, confirmText, cancelText);
            var confirmed = false;
            // The callback can run on any thread, and the panel is a control, so it is posted to the UI thread.
            using var registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(panel.Cancel));

            if (DialogHosting.GetWindowOwner(TopLevel.GetTopLevel(host)) is not { } owner)
            {
                var completed = new TaskCompletionSource();
                panel.Confirmed += (_, _) => { confirmed = true; completed.TrySetResult(); };
                panel.Cancelled += (_, _) => completed.TrySetResult();
                await OverlayDialogHost.ShowAsync(host, panel, title, completed.Task, ConfirmPanel.PreferredMinWidth)
                    .ConfigureAwait(true);
                cancellationToken.ThrowIfCancellationRequested();
                return confirmed;
            }

            var window = new Window
            {
                Title = title,
                Content = panel,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false,
                MinWidth = ConfirmPanel.PreferredMinWidth,
            };
            panel.Confirmed += (_, _) => { confirmed = true; window.Close(); };
            panel.Cancelled += (_, _) => window.Close();

            await window.ShowDialog(owner).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            return confirmed;
        }
    }
}
