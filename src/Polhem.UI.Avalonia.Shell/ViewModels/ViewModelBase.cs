using CommunityToolkit.Mvvm.ComponentModel;
using Polhem.UI.Avalonia.Shell.Localization;

namespace Polhem.UI.Avalonia.Shell.ViewModels
{
    /// <summary>
    /// Base of the shell's view models: the marker <see cref="ShellViewLocator"/> resolves views by, and a status
    /// line that follows the interface language.
    /// </summary>
    internal abstract partial class ViewModelBase : ObservableObject
    {
        private string _statusKey = string.Empty;
        private object?[] _statusArgs = [];

        /// <summary>Gets the status line text in the interface language.</summary>
        [ObservableProperty]
        public partial string Status { get; set; } = string.Empty;

        /// <summary>Gets whether <see cref="Status"/> reports an error; the views colour it.</summary>
        [ObservableProperty]
        public partial bool IsError { get; set; }

        /// <summary>
        /// Re-renders the text this view model holds after the interface language changed. The views' own text binds
        /// to <see cref="ShellLocalizer"/> and follows on its own; only text a view model stored needs this.
        /// </summary>
        public virtual void RefreshText()
        {
            if (_statusKey.Length > 0)
                Status = ShellLocalizer.Current.Format(_statusKey, _statusArgs);
        }

        /// <summary>
        /// Shows the text of <paramref name="key"/> as the status line, remembering the key so a language switch
        /// re-renders it.
        /// </summary>
        protected void SetStatus(string key, bool isError, params object?[] args)
        {
            _statusKey = key;
            _statusArgs = args;
            Status = ShellLocalizer.Current.Format(key, args);
            IsError = isError;
        }
    }
}
