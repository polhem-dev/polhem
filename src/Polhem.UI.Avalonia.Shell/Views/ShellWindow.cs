using Avalonia.Controls;
using Polhem.UI.Avalonia.Shell.ViewModels;

namespace Polhem.UI.Avalonia.Shell.Views
{
    /// <summary>The desktop window: a thin frame around the platform-neutral <see cref="ShellView"/>.</summary>
    internal sealed class ShellWindow : Window
    {
        /// <summary>Initializes the window for <paramref name="viewModel"/>.</summary>
        public ShellWindow(ShellViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            Title = viewModel.AppTitle;
            Width = 1100;
            Height = 720;
            MinWidth = 640;
            MinHeight = 480;
            DataContext = viewModel;
            Content = new ShellView();
        }
    }
}
