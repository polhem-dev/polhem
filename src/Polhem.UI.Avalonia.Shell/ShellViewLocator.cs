using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Polhem.UI.Avalonia.Shell.ViewModels;
using Polhem.UI.Avalonia.Shell.Views;

namespace Polhem.UI.Avalonia.Shell
{
    /// <summary>
    /// Builds the view of each shell view model. An explicit mapping rather than a type-name convention: a name
    /// looked up by reflection is what trimming removes, and the shell's views live in its own assembly.
    /// </summary>
    internal sealed class ShellViewLocator : IDataTemplate
    {
        /// <inheritdoc/>
        public Control? Build(object? param) => param switch
        {
            ConnectionViewModel => new ConnectionView(),
            LoginViewModel => new LoginView(),
            FormsViewModel => new FormsView(),
            _ => null,
        };

        /// <inheritdoc/>
        public bool Match(object? data) => data is ConnectionViewModel or LoginViewModel or FormsViewModel;
    }
}
