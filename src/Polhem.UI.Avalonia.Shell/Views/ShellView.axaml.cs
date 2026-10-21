using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Polhem.UI.Avalonia.Shell.Views
{
    /// <summary>
    /// The shell's root content. Keeps clear of the system areas on phones and routes the platform back request into
    /// the forms screen.
    /// </summary>
    internal sealed partial class ShellView : UserControl
    {
        private IInsetsManager? _insets;
        private TopLevel? _topLevel;

        /// <summary>Loads the XAML.</summary>
        public ShellView()
        {
            InitializeComponent();
        }

        /// <inheritdoc/>
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            // Single-view hosts (iOS, Android) report a safe area around the notch, the status bar and the home
            // indicator; the content is inset by it in any orientation. Desktop and browser report none.
            _topLevel = TopLevel.GetTopLevel(this);
            _insets = _topLevel?.InsetsManager;
            if (_insets is not null)
            {
                _insets.SafeAreaChanged += OnSafeAreaChanged;
                Padding = _insets.SafeAreaPadding;
            }

            // The platform back request (the Android back button or gesture, the browser's back button) unwinds the
            // forms screen record → tab before the platform takes over, which on Android exits the app.
            if (_topLevel is not null)
                _topLevel.BackRequested += OnBackRequested;
        }

        /// <inheritdoc/>
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (_insets is not null)
            {
                _insets.SafeAreaChanged -= OnSafeAreaChanged;
                _insets = null;
            }

            if (_topLevel is not null)
            {
                _topLevel.BackRequested -= OnBackRequested;
                _topLevel = null;
            }

            base.OnDetachedFromVisualTree(e);
        }

        private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e) => Padding = e.SafeAreaPadding;

        private void OnBackRequested(object? sender, RoutedEventArgs e)
        {
            // The connection and sign-in steps have nothing to unwind, so the request falls through to the platform.
            var forms = this.GetVisualDescendants().OfType<FormsView>().FirstOrDefault();
            if (forms is not null && forms.TryHandleBack())
                e.Handled = true;
        }
    }
}
