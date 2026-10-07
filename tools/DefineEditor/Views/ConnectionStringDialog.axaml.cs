using Avalonia.Controls;
using Avalonia.Interactivity;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

public partial class ConnectionStringDialog : Window
{
    public ConnectionStringDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the dialog modally over <paramref name="owner"/> and returns the parse result the user applied, or
    /// <c>null</c> when they cancelled or closed the window.
    /// </summary>
    public static async Task<ConnectionStringParseResult?> ShowAsync(Window owner, ConnectionStringDialogViewModel viewModel)
    {
        var dialog = new ConnectionStringDialog { DataContext = viewModel };
        viewModel.Applied += (_, _) => dialog.Close(viewModel.ParseResult);
        return await dialog.ShowDialog<ConnectionStringParseResult?>(owner);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
