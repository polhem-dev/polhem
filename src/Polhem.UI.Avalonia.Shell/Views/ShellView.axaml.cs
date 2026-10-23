using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Polhem.UI.Avalonia.Shell.Views
{
    /// <summary>
    /// The shell's root content. Keeps clear of the system areas and the on-screen keyboard on phones, and routes the
    /// platform back request into the forms screen.
    /// </summary>
    internal sealed partial class ShellView : UserControl
    {
        private IInsetsManager? _insets;
        private IInputPane? _inputPane;
        private TopLevel? _topLevel;
        private Thickness _safeArea;
        private Rect _keyboard;

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
                _safeArea = _insets.SafeAreaPadding;
            }

            // The on-screen keyboard covers the bottom of the screen without resizing the view, so a sign-in button or
            // a form field near the bottom would sit under it. The content is lifted above the keyboard instead.
            _inputPane = _topLevel?.InputPane;
            if (_inputPane is not null)
            {
                _inputPane.StateChanged += OnInputPaneChanged;
                _keyboard = _inputPane.State == InputPaneState.Open ? _inputPane.OccludedRect : default;
            }
            UpdatePadding();

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

            if (_inputPane is not null)
            {
                _inputPane.StateChanged -= OnInputPaneChanged;
                _inputPane = null;
            }

            if (_topLevel is not null)
            {
                _topLevel.BackRequested -= OnBackRequested;
                _topLevel = null;
            }

            base.OnDetachedFromVisualTree(e);
        }

        private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e)
        {
            _safeArea = e.SafeAreaPadding;
            UpdatePadding();
        }

        private void OnInputPaneChanged(object? sender, InputPaneStateEventArgs e)
        {
            _keyboard = e.NewState == InputPaneState.Open ? e.EndRect : default;
            UpdatePadding();
            // The content just shrank to the space above the keyboard; scroll the focused editor back into view once
            // that layout has run, so the user sees what they type.
            Dispatcher.UIThread.Post(() =>
            {
                if (_topLevel?.FocusManager?.GetFocusedElement() is Control focused)
                    focused.BringIntoView();
            }, DispatcherPriority.Background);
        }

        private void UpdatePadding()
            => Padding = ComputePadding(_safeArea, _keyboard, _topLevel?.Bounds.Height ?? 0);

        /// <summary>
        /// Returns the root padding: the safe area, with the bottom raised to clear the keyboard when it covers more.
        /// </summary>
        /// <param name="safeArea">The safe-area padding the platform reports.</param>
        /// <param name="keyboard">The area the keyboard occludes, in top-level coordinates; empty when it is closed.</param>
        /// <param name="height">The height of the top level.</param>
        internal static Thickness ComputePadding(Thickness safeArea, Rect keyboard, double height)
        {
            var covered = keyboard.Height > 0 && height > 0 ? Math.Max(0, height - keyboard.Top) : 0;
            return new Thickness(safeArea.Left, safeArea.Top, safeArea.Right, Math.Max(safeArea.Bottom, covered));
        }

        private void OnBackRequested(object? sender, RoutedEventArgs e)
        {
            // The connection and sign-in steps have nothing to unwind, so the request falls through to the platform.
            var forms = this.GetVisualDescendants().OfType<FormsView>().FirstOrDefault();
            if (forms is not null && forms.TryHandleBack())
                e.Handled = true;
        }
    }
}
