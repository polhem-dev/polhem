using Avalonia.Controls;
using Avalonia.Interactivity;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

/// <summary>Edits a field's expression, with the fields and functions it can use beside it.</summary>
public partial class ExpressionEditorDialog : Window
{
    public ExpressionEditorDialog()
    {
        InitializeComponent();
        Symbols.SymbolChosen += (_, text) => Editor.Insert(text);
        Opened += (_, _) => Editor.FocusEnd();
    }

    /// <summary>
    /// Shows the dialog modally over <paramref name="owner"/>; returns <c>true</c> when the user confirmed, after which
    /// <see cref="ExpressionFieldViewModel.Expression"/> holds the new value.
    /// </summary>
    public static Task<bool> ShowAsync(Window owner, ExpressionEditorViewModel viewModel) =>
        new ExpressionEditorDialog { DataContext = viewModel }.ShowDialog<bool>(owner);

    private void OnOk(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
