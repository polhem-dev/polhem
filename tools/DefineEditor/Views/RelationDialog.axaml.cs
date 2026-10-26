using Avalonia.Controls;
using Avalonia.Interactivity;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

public partial class RelationDialog : Window
{
    public RelationDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the dialog modally over <paramref name="owner"/> and returns whether the user applied it; the result is
    /// then in <paramref name="viewModel"/>.
    /// </summary>
    public static async Task<bool> ShowAsync(Window owner, RelationDialogViewModel viewModel)
    {
        var dialog = new RelationDialog { DataContext = viewModel };
        viewModel.Applied += (_, _) => dialog.Close(true);
        return await dialog.ShowDialog<bool>(owner);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
