namespace Polhem.UI.Core
{
    /// <summary>
    /// View services provided by the host UI framework (WinForms / WPF / Avalonia, etc.).
    /// </summary>
    public interface IUIViewService
    {
        /// <summary>
        /// Shows the API connection settings dialog.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that cancels the setup. An implementation that honours it closes its dialog and
        /// throws <see cref="OperationCanceledException"/>.
        /// </param>
        /// <returns>True when the user has completed the connection setup; otherwise, false.</returns>
        Task<bool> ShowApiConnectAsync(CancellationToken cancellationToken = default);
    }
}
