using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace Polhem.UI.Avalonia.Controls.Editors
{
    /// <summary>
    /// The content of <see cref="NamePromptDialog"/>: a name box, an optional check box, and OK and Cancel.
    /// </summary>
    internal sealed class NamePromptPanel : UserControl
    {
        /// <summary>The width the dialog keeps on a screen wide enough for it.</summary>
        internal const double PreferredMinWidth = 320;

        private readonly TextBox _name;
        private readonly CheckBox? _option;
        private readonly Button _okButton;

        /// <summary>Initializes a new instance of <see cref="NamePromptPanel"/>.</summary>
        /// <param name="label">The caption above the name box.</param>
        /// <param name="name">The name the box starts with.</param>
        /// <param name="maxLength">The longest name the box takes.</param>
        /// <param name="optionText">The caption of the check box, or <c>null</c> for none.</param>
        /// <param name="optionChecked">Whether the check box starts checked.</param>
        public NamePromptPanel(string label, string name, int maxLength, string? optionText, bool optionChecked)
        {
            _name = new TextBox { Text = name, MaxLength = maxLength };
            _name.TextChanged += (_, _) => UpdateOk();
            _okButton = new Button { Content = UIText.Get(Polhem.Definition.Language.PolhemUIText.Ok) };
            _okButton.Classes.Add("Primary");
            _okButton.Click += (_, _) => Accept();
            var cancelButton = new Button { Content = UIText.Get(Polhem.Definition.Language.PolhemUIText.Cancel) };
            cancelButton.Click += (_, _) => Cancel();

            var fields = new StackPanel { Spacing = 6 };
            fields.Children.Add(new TextBlock { Text = label });
            fields.Children.Add(_name);
            if (optionText is not null)
            {
                _option = new CheckBox { Content = optionText, IsChecked = optionChecked, Margin = new Thickness(0, 4, 0, 0) };
                fields.Children.Add(_option);
            }

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Thickness(0, 16, 0, 0),
            };
            buttons.Children.Add(cancelButton);
            buttons.Children.Add(_okButton);

            var body = new DockPanel { Margin = new Thickness(16), LastChildFill = true };
            DockPanel.SetDock(buttons, Dock.Bottom);
            body.Children.Add(buttons);
            body.Children.Add(fields);
            Content = body;

            UpdateOk();
            Loaded += (_, _) =>
            {
                _name.Focus();
                _name.SelectAll();
            };
            KeyDown += OnKeyDown;
        }

        /// <summary>Raised when the user accepts a name that is not blank.</summary>
        public event EventHandler? Accepted;

        /// <summary>Raised when the user backs out.</summary>
        public event EventHandler? Cancelled;

        /// <summary>Gets the name entered, trimmed.</summary>
        public string EnteredName => (_name.Text ?? string.Empty).Trim();

        /// <summary>Gets whether the check box is checked; <see langword="false"/> when there is none.</summary>
        public bool IsOptionChecked => _option?.IsChecked == true;

        /// <summary>Accepts the name, as OK does, unless it is blank.</summary>
        public void Accept()
        {
            if (EnteredName.Length == 0) return;
            Accepted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Backs out, as Cancel does.</summary>
        public void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

        private void UpdateOk() => _okButton.IsEnabled = EnteredName.Length > 0;

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Cancel();
            }
            else if (e.Key == Key.Enter && e.Source is TextBox)
            {
                e.Handled = true;
                Accept();
            }
        }
    }
}
