using Avalonia.Controls;

namespace Polhem.UI.Avalonia.Controls.Editors
{
    /// <summary>
    /// Decides how a modal editor dialog is presented: as a native <see cref="Window"/> or on the top
    /// level's overlay layer through <see cref="OverlayDialogHost"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="LookupDialog"/> and <see cref="RowEditDialog"/> both ask here rather than each testing
    /// for itself. When they tested separately they drifted: the lookup dialog checked only for the
    /// browser and opened a <see cref="Window"/> on iOS and Android, whose windowing backends throw
    /// <see cref="NotSupportedException"/> when asked to create a window.
    /// </remarks>
    internal static class DialogHosting
    {
        /// <summary>
        /// Returns the window that parents a native modal dialog opened from a visual under
        /// <paramref name="topLevel"/>, or <c>null</c> when the dialog must use the overlay host.
        /// </summary>
        /// <param name="topLevel">The top level that owns the visual opening the dialog.</param>
        public static Window? GetWindowOwner(TopLevel? topLevel)
            => UsesOverlay(topLevel is Window, IsSingleViewPlatform()) ? null : topLevel as Window;

        /// <summary>
        /// The decision itself, separated from the platform probes so it can be tested on the desktop.
        /// </summary>
        /// <param name="ownerIsWindow">Whether the owning top level is a native <see cref="Window"/>.</param>
        /// <param name="singleViewPlatform">Whether the process runs on a platform with no native windows.</param>
        /// <returns>
        /// <c>true</c> unless a native window can parent the dialog: only the desktop classic-window
        /// lifetime has one. A single-view top level (browser, iOS, Android, or a desktop app that
        /// chose the single-view lifetime) has no window to parent a modal dialog.
        /// </returns>
        public static bool UsesOverlay(bool ownerIsWindow, bool singleViewPlatform)
            => singleViewPlatform || !ownerIsWindow;

        private static bool IsSingleViewPlatform()
            => OperatingSystem.IsBrowser() || OperatingSystem.IsIOS() || OperatingSystem.IsAndroid();
    }
}
