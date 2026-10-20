using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

/// <summary>
/// The CREATE TABLE statement of the TableSchema being edited, in the dialect picked in its header.
/// </summary>
public partial class CreateTablePreview : UserControl
{
    public CreateTablePreview() => InitializeComponent();

    // The clipboard belongs to the window, which the view model cannot reach, so copying stays in the view.
    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TableSchemaDocumentViewModel document) return;
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        await clipboard.SetTextAsync(document.CreateTableScript);
        document.StatusText = LocalizationService.Current["Status_CreateTableCopied"];
    }
}
