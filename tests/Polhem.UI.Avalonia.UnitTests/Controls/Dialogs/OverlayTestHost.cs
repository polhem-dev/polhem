using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Dialogs
{
    /// <summary>
    /// A host for the overlay dialogs without an Avalonia platform: a <see cref="VisualLayerManager"/> with its overlay
    /// layer turned on, the way a single-view top level provides one.
    /// </summary>
    /// <remarks>
    /// No top level exists, so <c>DialogHosting.GetWindowOwner</c> finds no window and every dialog takes the overlay
    /// path, as it does on the browser, iOS and Android.
    /// </remarks>
    internal sealed class OverlayTestHost
    {
        public OverlayTestHost(Control? host = null)
        {
            Host = host ?? new Border();
            LayerManager = new VisualLayerManager { EnableOverlayLayer = true, Child = Host };
            Overlay = OverlayLayer.GetOverlayLayer(Host)
                ?? throw new InvalidOperationException("The layer manager did not provide an overlay layer.");
        }

        /// <summary>Gets the visual a dialog is opened from.</summary>
        public Control Host { get; }

        /// <summary>Gets the layer manager that owns the overlay layer.</summary>
        public VisualLayerManager LayerManager { get; }

        /// <summary>Gets the overlay layer the dialogs are added to.</summary>
        public OverlayLayer Overlay { get; }

        /// <summary>Gets the dimmed backdrop of the open dialog.</summary>
        public Border Backdrop => Assert.IsType<Border>(Assert.Single(Overlay.Children));

        /// <summary>Gets the card that holds the open dialog's title and panel.</summary>
        public Border Card => Assert.IsType<Border>(Backdrop.Child);

        /// <summary>Gets the title shown above the panel, or <c>null</c> when the card has none.</summary>
        public string? Title => CardBody.Children.OfType<TextBlock>().FirstOrDefault()?.Text;

        /// <summary>Gets the panel hosted by the open dialog.</summary>
        public T Panel<T>() where T : Control => Assert.IsType<T>(CardBody.Children[^1]);

        private DockPanel CardBody => Assert.IsType<DockPanel>(Card.Child);
    }
}
