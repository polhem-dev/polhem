using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Polhem.UI.Avalonia.Controls.Editors
{
    /// <summary>
    /// Presents a panel as a modal overlay on the current top level's
    /// <see cref="OverlayLayer"/>. This is the single-view fallback for hosts that cannot
    /// open a native <see cref="Window"/>: the browser (WASM), iOS and Android heads, where
    /// creating a window throws. <see cref="LookupDialog"/> and <see cref="RowEditDialog"/> call
    /// this whenever <see cref="DialogHosting.GetWindowOwner"/> finds no owning window; the desktop
    /// classic-window lifetime keeps the window path.
    /// </summary>
    /// <remarks>
    /// The card stays inside the part of the screen the user can see: a gutter from every edge, the
    /// platform's safe area (notch, home indicator), and on a phone the area above the on-screen
    /// keyboard while it is open. The card's size limits are that area (<see cref="ComputeLayout"/>,
    /// pinned by <c>OverlayDialogHostTests</c>), so a hosted panel that scrolls its body and docks its
    /// buttons outside the scrolled part keeps the buttons on screen.
    /// </remarks>
    internal static class OverlayDialogHost
    {
        /// <summary>The widest the card grows on a large screen.</summary>
        internal const double MaxCardWidth = 560;

        /// <summary>The space kept between the card and each edge of the visible area.</summary>
        internal const double Gutter = 12;

        /// <summary>
        /// Adds <paramref name="content"/> as a centered modal card over a dimmed backdrop,
        /// then awaits <paramref name="completion"/> before removing it.
        /// </summary>
        /// <param name="host">A visual inside the target top level (resolves the overlay layer).</param>
        /// <param name="content">The panel to host (e.g. <see cref="LookupPanel"/> / <see cref="RowEditPanel"/>).</param>
        /// <param name="title">Optional header text shown above the panel.</param>
        /// <param name="completion">A task that completes when the panel commits or cancels.</param>
        /// <param name="minCardWidth">
        /// The width the card keeps even when its content is narrower, on screens wide enough for it; a
        /// narrower screen gives the card its whole visible width instead.
        /// </param>
        public static async Task ShowAsync(Visual host, Control content, string? title, Task completion,
            double minCardWidth = 0)
        {
            ArgumentNullException.ThrowIfNull(host);
            ArgumentNullException.ThrowIfNull(content);
            ArgumentNullException.ThrowIfNull(completion);

            var overlay = OverlayLayer.GetOverlayLayer(host)
                ?? throw new InvalidOperationException("No OverlayLayer is available for the current top level.");
            var topLevel = TopLevel.GetTopLevel(host);
            var inputPane = topLevel?.InputPane;
            var insets = topLevel?.InsetsManager;

            // Visual derives from StyledElement, so ActualThemeVariant is always available.
            var variant = host.ActualThemeVariant;
            var card = BuildCard(content, title, variant);
            card.HorizontalAlignment = HorizontalAlignment.Center;
            card.VerticalAlignment = VerticalAlignment.Center;

            // The backdrop's non-null brush both dims the page and captures pointer input so
            // the dimmed content stays inert (a null background would not hit-test). It does not
            // dismiss on click — only the panel's OK / Cancel close the dialog, so an in-progress
            // row edit cannot be lost by a stray click outside the card.
            var backdrop = new Border
            {
                Background = new SolidColorBrush(Colors.Black, 0.45),
                Child = card,
            };

            var safeArea = insets?.SafeAreaPadding ?? default;
            var keyboard = inputPane?.State == InputPaneState.Open ? inputPane.OccludedRect : default;

            // OverlayLayer derives from Canvas, so children are sized to their content rather
            // than stretched; size the backdrop to the layer explicitly and track resizes.
            void Relayout()
            {
                var size = overlay.Bounds.Size;
                var layout = ComputeLayout(size, safeArea, ComputeKeyboardInset(size.Height, keyboard), minCardWidth);
                backdrop.Width = size.Width;
                backdrop.Height = size.Height;
                backdrop.Padding = layout.Padding;
                card.MaxWidth = layout.MaxWidth;
                card.MinWidth = layout.MinWidth;
                card.MaxHeight = layout.MaxHeight;
            }
            void OnOverlayChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
            {
                if (e.Property == Visual.BoundsProperty) Relayout();
            }
            void OnInputPaneChanged(object? sender, InputPaneStateEventArgs e)
            {
                keyboard = e.NewState == InputPaneState.Open ? e.EndRect : default;
                Relayout();
                // The card just shrank to the space above the keyboard; scroll the focused editor
                // back into view once that layout has run, so the user sees what they type.
                Dispatcher.UIThread.Post(() => BringFocusedIntoView(topLevel, card), DispatcherPriority.Background);
            }
            void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e)
            {
                safeArea = e.SafeAreaPadding;
                Relayout();
            }

            Relayout();
            overlay.PropertyChanged += OnOverlayChanged;
            if (inputPane is not null) inputPane.StateChanged += OnInputPaneChanged;
            if (insets is not null) insets.SafeAreaChanged += OnSafeAreaChanged;
            overlay.Children.Add(backdrop);
            try
            {
                await completion.ConfigureAwait(true);
            }
            finally
            {
                overlay.PropertyChanged -= OnOverlayChanged;
                if (inputPane is not null) inputPane.StateChanged -= OnInputPaneChanged;
                if (insets is not null) insets.SafeAreaChanged -= OnSafeAreaChanged;
                overlay.Children.Remove(backdrop);
            }
        }

        /// <summary>
        /// Computes where the card may sit: the backdrop padding that keeps it inside the visible area,
        /// and the size limits that make it fit there.
        /// </summary>
        /// <param name="overlaySize">The size of the overlay layer (the whole top level).</param>
        /// <param name="safeArea">The platform's safe-area padding.</param>
        /// <param name="keyboardInset">How far the on-screen keyboard reaches up from the bottom edge.</param>
        /// <param name="minCardWidth">The width the card keeps when the screen is wide enough.</param>
        internal static OverlayCardLayout ComputeLayout(Size overlaySize, Thickness safeArea, double keyboardInset,
            double minCardWidth)
        {
            var padding = new Thickness(
                safeArea.Left + Gutter,
                safeArea.Top + Gutter,
                safeArea.Right + Gutter,
                Math.Max(safeArea.Bottom, keyboardInset) + Gutter);
            var availableWidth = Math.Max(0, overlaySize.Width - padding.Left - padding.Right);
            var availableHeight = Math.Max(0, overlaySize.Height - padding.Top - padding.Bottom);
            return new OverlayCardLayout(
                padding,
                Math.Min(MaxCardWidth, availableWidth),
                Math.Min(Math.Max(0, minCardWidth), availableWidth),
                availableHeight);
        }

        /// <summary>
        /// Returns how far the keyboard covers the overlay up from its bottom edge.
        /// </summary>
        /// <param name="overlayHeight">The height of the overlay layer.</param>
        /// <param name="occludedRect">The input pane's bounds in top-level coordinates; empty when closed.</param>
        /// <remarks>
        /// The distance from the pane's top edge to the bottom of the overlay is used when the pane lies
        /// inside the overlay, which also covers the bar some keyboards add above the keys. A pane
        /// reported without a usable position falls back to its height.
        /// </remarks>
        internal static double ComputeKeyboardInset(double overlayHeight, Rect occludedRect)
        {
            if (occludedRect.Height <= 0 || overlayHeight <= 0) return 0;
            var inset = occludedRect.Y > 0 && occludedRect.Y < overlayHeight
                ? overlayHeight - occludedRect.Y
                : occludedRect.Height;
            return Math.Clamp(inset, 0, overlayHeight);
        }

        private static void BringFocusedIntoView(TopLevel? topLevel, Visual card)
        {
            if (topLevel?.FocusManager?.GetFocusedElement() is Control focused && card.IsVisualAncestorOf(focused))
                focused.BringIntoView();
        }

        private static Border BuildCard(Control content, string? title, ThemeVariant variant)
        {
            var dark = variant == ThemeVariant.Dark;
            var surface = dark ? Color.FromRgb(0x2A, 0x2B, 0x33) : Color.FromRgb(0xFF, 0xFF, 0xFF);
            var stroke = dark ? Color.FromRgb(0x3C, 0x3D, 0x47) : Color.FromRgb(0xD0, 0xD0, 0xD8);

            var inner = new DockPanel { LastChildFill = true };
            if (!string.IsNullOrEmpty(title))
            {
                var header = new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    Margin = new Thickness(16, 12, 16, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                DockPanel.SetDock(header, Dock.Top);
                inner.Children.Add(header);
            }
            inner.Children.Add(content);

            return new Border
            {
                Background = new SolidColorBrush(surface),
                BorderBrush = new SolidColorBrush(stroke),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                ClipToBounds = true,
                Child = inner,
            };
        }

        /// <summary>
        /// Where the overlay card may sit, as computed by <see cref="ComputeLayout"/>.
        /// </summary>
        /// <param name="Padding">The backdrop padding: gutter, safe area and keyboard.</param>
        /// <param name="MaxWidth">The widest the card may be.</param>
        /// <param name="MinWidth">The narrowest the card may be.</param>
        /// <param name="MaxHeight">The tallest the card may be.</param>
        internal readonly record struct OverlayCardLayout(Thickness Padding, double MaxWidth, double MinWidth, double MaxHeight);
    }
}
