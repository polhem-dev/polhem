using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Polhem.UI.Avalonia.Controls.Editors
{
    /// <summary>
    /// The content of <see cref="ConfirmDialog"/>: a message, a confirm button and a cancel button.
    /// </summary>
    internal sealed class ConfirmPanel : UserControl
    {
        /// <summary>The width the dialog keeps on a screen wide enough for it.</summary>
        internal const double PreferredMinWidth = 320;

        private readonly Button _confirmButton;
        private readonly Button _cancelButton;

        /// <summary>Initializes a new instance of <see cref="ConfirmPanel"/>.</summary>
        /// <param name="message">The question to ask.</param>
        /// <param name="confirmText">The caption of the button that goes ahead.</param>
        /// <param name="cancelText">The caption of the button that backs out.</param>
        public ConfirmPanel(string message, string confirmText, string cancelText)
        {
            _confirmButton = new Button { Content = confirmText };
            _confirmButton.Classes.Add("Danger");
            _confirmButton.Click += (_, _) => Confirmed?.Invoke(this, EventArgs.Empty);

            _cancelButton = new Button { Content = cancelText };
            _cancelButton.Click += (_, _) => Cancel();

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Thickness(0, 16, 0, 0),
            };
            buttons.Children.Add(_cancelButton);
            buttons.Children.Add(_confirmButton);

            var body = new DockPanel { Margin = new Thickness(16), LastChildFill = true };
            DockPanel.SetDock(buttons, Dock.Bottom);
            body.Children.Add(buttons);
            body.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
            Content = body;

            // The safe answer holds the focus, so a stray Enter or Space does not go ahead.
            Loaded += (_, _) => _cancelButton.Focus();
            KeyDown += OnKeyDown;
        }

        /// <summary>Raised when the user goes ahead.</summary>
        public event EventHandler? Confirmed;

        /// <summary>Raised when the user backs out.</summary>
        public event EventHandler? Cancelled;

        /// <summary>Backs out, as the cancel button does.</summary>
        public void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            Cancel();
        }
    }
}
