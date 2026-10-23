using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

/// <summary>
/// Edits the selected tree node's properties in a dialog, for an editor whose side does not show the property grid.
/// </summary>
/// <remarks>
/// The dialog's <see cref="EditorPane"/> takes the document as its data context, the way the side's pane does, so a
/// value written here marks the document dirty and refreshes the tree labels the same way.
/// </remarks>
public partial class NodePropertiesDialog : Window
{
    public NodePropertiesDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the properties of <paramref name="document"/>'s selected node modally over <paramref name="owner"/>.
    /// </summary>
    public static async Task ShowAsync(Window owner, ObjectTreeDocumentViewModelBase document, string title)
    {
        var dialog = new NodePropertiesDialog { DataContext = document, Title = title };
        // The window's menu, which carries Save, is not active while the dialog is, so the dialog takes the shortcut.
        var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        dialog.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.S, modifier), Command = document.FileSaveCommand });
        await dialog.ShowDialog(owner);
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        // The pane listens to the document; clearing the data context lets the closed dialog go.
        DataContext = null;
        base.OnClosed(e);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
