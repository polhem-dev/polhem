using Avalonia.Controls;
using Avalonia.Interactivity;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

public partial class SolutionSettingsDialog : Window
{
    public SolutionSettingsDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the dialog modally over <paramref name="owner"/> and returns <c>true</c> when the user saved, after the
    /// dialog copied its fields into the settings it was opened with.
    /// </summary>
    public static async Task<bool> ShowAsync(Window owner, SolutionSettingsDialogViewModel viewModel)
    {
        var dialog = new SolutionSettingsDialog { DataContext = viewModel };
        viewModel.Applied += (_, _) => dialog.Close(true);
        dialog.Opened += (_, _) => dialog.CommandBox.Focus();
        return await dialog.ShowDialog<bool>(owner);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
