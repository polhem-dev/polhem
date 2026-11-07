using Avalonia.Controls;
using Avalonia.Interactivity;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

/// <summary>Picks and orders the fields a comma-separated field list names.</summary>
public partial class FieldPickerDialog : Window
{
    public FieldPickerDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the dialog modally over <paramref name="owner"/>; returns <c>true</c> when the user confirmed, after which
    /// <see cref="FieldPickerViewModel.Result"/> holds the new value.
    /// </summary>
    public static Task<bool> ShowAsync(Window owner, FieldPickerViewModel viewModel) =>
        new FieldPickerDialog { DataContext = viewModel }.ShowDialog<bool>(owner);

    private void OnOk(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
