using Avalonia.Controls;
using Avalonia.Interactivity;
using Polhem.DefineEditor.ViewModels;
using Polhem.Definition.Forms;

namespace Polhem.DefineEditor.Views;

/// <summary>Adds or edits a form rule as a whole, with the names it can use and the AI assistant beside it.</summary>
public partial class RuleDialog : Window
{
    private ExpressionBox _activeBox;

    public RuleDialog()
    {
        InitializeComponent();
        _activeBox = ConditionBox;
        WhenBox.EditorFocused += (_, _) => _activeBox = WhenBox;
        ConditionBox.EditorFocused += (_, _) => _activeBox = ConditionBox;
        Symbols.SymbolChosen += (_, text) => _activeBox.Insert(text);
        Opened += (_, _) => _activeBox.FocusEnd();
    }

    /// <summary>
    /// Shows the dialog modally over <paramref name="owner"/> with the caret in <paramref name="focus"/>'s expression;
    /// returns <c>true</c> when the user confirmed.
    /// </summary>
    /// <param name="owner">The window the dialog is shown over.</param>
    /// <param name="viewModel">The rule being added or edited.</param>
    /// <param name="focus"><see cref="FormRule.When"/> or <see cref="FormRule.Condition"/>.</param>
    public static Task<bool> ShowAsync(Window owner, RuleDialogViewModel viewModel, string focus = nameof(FormRule.Condition))
    {
        var dialog = new RuleDialog { DataContext = viewModel };
        if (focus == nameof(FormRule.When)) dialog._activeBox = dialog.WhenBox;
        return dialog.ShowDialog<bool>(owner);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (DataContext is RuleDialogViewModel { CanConfirm: true }) Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
