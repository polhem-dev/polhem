using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Polhem.Definition.Language;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// The message strip a view shows above its toolbar when an action is refused or fails: a tinted
    /// danger-coloured box with an icon, the message wrapped to the view's width, and a button that
    /// dismisses it.
    /// </summary>
    internal sealed class MessageBar : Border
    {
        // Error glyph (24 grid, even-odd fill): a filled circle with the exclamation mark cut out.
        private const string ErrorIconGeometry =
            "F0 M12 2a10 10 0 1 1 0 20a10 10 0 0 1 0-20Zm0 5a1.1 1.1 0 0 0-1.1 1.1v4.8a1.1 1.1 0 0 0 2.2 0V8.1A1.1 1.1 0 0 0 12 7Z" +
            "m0 8.6a1.3 1.3 0 1 0 0 2.6a1.3 1.3 0 0 0 0-2.6Z";
        private const string DismissIconGeometry =
            "M6.7 5.3a1 1 0 0 0-1.4 1.4L10.6 12l-5.3 5.3a1 1 0 1 0 1.4 1.4l5.3-5.3 5.3 5.3a1 1 0 0 0 1.4-1.4L13.4 12" +
            "l5.3-5.3a1 1 0 0 0-1.4-1.4L12 10.6 6.7 5.3Z";

        private readonly TextBlock _text;

        /// <summary>
        /// Initializes a new, hidden instance of <see cref="MessageBar"/>.
        /// </summary>
        public MessageBar()
        {
            IsVisible = false;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(4);
            Padding = new Thickness(12, 8, 4, 8);
            BindBrush(this, BackgroundProperty, "SemiColorDangerLight", new SolidColorBrush(Color.FromArgb(0x26, 0xF9, 0x39, 0x20)));
            BindBrush(this, BorderBrushProperty, "SemiColorDanger", Brushes.IndianRed);

            var icon = new PathIcon
            {
                Width = 18,
                Height = 18,
                Margin = new Thickness(0, 1, 10, 0),
                VerticalAlignment = VerticalAlignment.Top,
            };
            BindBrush(icon, TemplatedControl.ForegroundProperty, "SemiColorDanger", Brushes.IndianRed);

            _text = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var dismissIcon = new PathIcon { Width = 12, Height = 12 };
            var dismiss = new Button
            {
                Content = dismissIcon,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6),
                Margin = new Thickness(8, 0, 0, 0),
                MinWidth = 0,
                MinHeight = 0,
                VerticalAlignment = VerticalAlignment.Top,
            };
            var dismissText = UIText.Get(PolhemUIText.Dismiss);
            ToolTip.SetTip(dismiss, dismissText);
            AutomationProperties.SetName(dismiss, dismissText);
            dismiss.Click += (_, _) => Clear();

            // `Geometry.Parse` needs platform services that are absent when unit tests build the
            // view without an Avalonia platform, so the glyphs are parsed on first visual attach.
            AttachedToVisualTree += (_, _) =>
            {
                icon.Data ??= Geometry.Parse(ErrorIconGeometry);
                dismissIcon.Data ??= Geometry.Parse(DismissIconGeometry);
            };

            var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            Grid.SetColumn(icon, 0);
            Grid.SetColumn(_text, 1);
            Grid.SetColumn(dismiss, 2);
            layout.Children.Add(icon);
            layout.Children.Add(_text);
            layout.Children.Add(dismiss);
            Child = layout;
        }

        /// <summary>Gets the message shown, or an empty string when the bar is hidden.</summary>
        public string Text => _text.Text ?? string.Empty;

        /// <summary>
        /// Shows <paramref name="message"/>.
        /// </summary>
        /// <param name="message">The message to show.</param>
        public void Show(string message)
        {
            _text.Text = message;
            IsVisible = true;
        }

        /// <summary>
        /// Hides the bar and clears its message.
        /// </summary>
        public void Clear()
        {
            _text.Text = string.Empty;
            IsVisible = false;
        }

        private static void BindBrush(Control control, AvaloniaProperty property, string key, IBrush fallback)
            => control.Bind(property, control.GetResourceObservable(key, value => value as IBrush ?? fallback));
    }
}
