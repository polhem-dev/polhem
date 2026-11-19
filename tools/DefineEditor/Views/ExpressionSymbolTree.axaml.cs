using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

/// <summary>The names an expression can use, under the headings of a tree; choosing one asks for it to be inserted.</summary>
public partial class ExpressionSymbolTree : UserControl
{
    /// <summary>Defines the <see cref="Groups"/> property.</summary>
    public static readonly StyledProperty<IReadOnlyList<ExpressionSymbolGroup>?> GroupsProperty =
        AvaloniaProperty.Register<ExpressionSymbolTree, IReadOnlyList<ExpressionSymbolGroup>?>(nameof(Groups));

    public ExpressionSymbolTree() => InitializeComponent();

    /// <summary>The headings and the names under them.</summary>
    public IReadOnlyList<ExpressionSymbolGroup>? Groups
    {
        get => GetValue(GroupsProperty);
        set => SetValue(GroupsProperty, value);
    }

    /// <summary>Raised with the text to insert when a name is double-clicked or chosen with Enter.</summary>
    public event EventHandler<string>? SymbolChosen;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GroupsProperty) Tree.ItemsSource = Groups;
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e) => Choose();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        Choose();
        e.Handled = true;
    }

    private void Choose()
    {
        if (Tree.SelectedItem is ExpressionSymbol symbol) SymbolChosen?.Invoke(this, symbol.InsertText);
    }
}
