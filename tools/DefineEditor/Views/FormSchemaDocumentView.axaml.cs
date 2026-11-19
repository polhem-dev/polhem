using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Polhem.DefineEditor.ViewModels;
using Polhem.Definition.Forms;

namespace Polhem.DefineEditor.Views;

public partial class FormSchemaDocumentView : UserControl
{
    public FormSchemaDocumentView()
    {
        InitializeComponent();
    }

    // A double-click on a rule's node opens the rule dialog. One on the expander arrow, or on any other node, keeps the
    // tree's own behavior.
    private async void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not FormSchemaDocumentViewModel { SelectedTreeNode.Value: FormRule } document) return;
        if (e.Source is not Avalonia.Visual source || source.FindAncestorOfType<TreeViewItem>(includeSelf: true) is null) return;
        if (source.FindAncestorOfType<Avalonia.Controls.Primitives.ToggleButton>(includeSelf: true) is not null) return;
        e.Handled = true;
        await document.EditSelectedRuleAsync();
    }
}
