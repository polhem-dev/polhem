using Avalonia;
using Avalonia.Controls;

namespace Polhem.UI.Avalonia.Controls.Editors
{
    /// <summary>
    /// Asks the user for a name, and optionally a yes-or-no choice beside it, in a modal dialog: a native window on
    /// the desktop, the overlay host where the platform has no windows (see <see cref="DialogHosting"/>).
    /// </summary>
    internal static class NamePromptDialog
    {
        /// <summary>
        /// Shows the prompt and returns the name and the choice, or <c>null</c> when the user backed out.
        /// </summary>
        /// <param name="host">A visual inside the owning top level.</param>
        /// <param name="title">The dialog's title.</param>
        /// <param name="label">The caption above the name box.</param>
        /// <param name="name">The name the box starts with.</param>
        /// <param name="maxLength">The longest name the box takes.</param>
        /// <param name="optionText">The caption of the check box, or <c>null</c> for none.</param>
        /// <param name="optionChecked">Whether the check box starts checked.</param>
        public static async Task<(string Name, bool Option)?> ShowAsync(Visual host, string title, string label, string name,
            int maxLength, string? optionText, bool optionChecked)
        {
            ArgumentNullException.ThrowIfNull(host);
            var panel = new NamePromptPanel(label, name, maxLength, optionText, optionChecked);
            var accepted = false;

            if (DialogHosting.GetWindowOwner(TopLevel.GetTopLevel(host)) is not { } owner)
            {
                var completed = new TaskCompletionSource();
                panel.Accepted += (_, _) => { accepted = true; completed.TrySetResult(); };
                panel.Cancelled += (_, _) => completed.TrySetResult();
                await OverlayDialogHost.ShowAsync(host, panel, title, completed.Task, NamePromptPanel.PreferredMinWidth)
                    .ConfigureAwait(true);
            }
            else
            {
                var window = new Window
                {
                    Title = title,
                    Content = panel,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false,
                    ShowInTaskbar = false,
                    MinWidth = NamePromptPanel.PreferredMinWidth,
                };
                panel.Accepted += (_, _) => { accepted = true; window.Close(); };
                panel.Cancelled += (_, _) => window.Close();
                await window.ShowDialog(owner).ConfigureAwait(true);
            }
            return accepted ? (panel.EnteredName, panel.IsOptionChecked) : null;
        }
    }
}
