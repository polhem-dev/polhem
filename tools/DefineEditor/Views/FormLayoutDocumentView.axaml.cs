using System.ComponentModel;
using Avalonia.Controls;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

public partial class FormLayoutDocumentView : UserControl
{
    // The rows' heights while both are shown; a collapsed preview keeps only its header row, and the layouts that show
    // one pane give it the whole side.
    private GridLength _editorHeight = new(1, GridUnitType.Star);
    private GridLength _expandedPreviewHeight = new(1, GridUnitType.Star);
    private FormLayoutDocumentViewModel? _document;

    public FormLayoutDocumentView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_document is not null)
            _document.PropertyChanged -= OnDocumentPropertyChanged;
        _document = DataContext as FormLayoutDocumentViewModel;
        if (_document is not null)
            _document.PropertyChanged += OnDocumentPropertyChanged;
        ApplyRows();
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FormLayoutDocumentViewModel.IsPreviewVisible)
            or nameof(FormLayoutDocumentViewModel.PaneLayout))
            ApplyRows();
    }

    private void ApplyRows()
    {
        var editorRow = RightPane.RowDefinitions[0];
        var previewRow = RightPane.RowDefinitions[2];
        // Keep the heights the user dragged the splitter to, so showing both again restores them. Only the layout
        // that shows both with the preview expanded leaves star heights that came from the splitter.
        if (editorRow.Height.IsStar && previewRow.Height.IsStar && editorRow.Height.Value > 0 && previewRow.Height.Value > 0)
        {
            _editorHeight = editorRow.Height;
            _expandedPreviewHeight = previewRow.Height;
        }

        var layout = _document?.PaneLayout ?? FormLayoutPaneLayout.PropertiesAndPreview;
        var previewExpanded = _document?.IsPreviewVisible ?? true;
        editorRow.Height = layout == FormLayoutPaneLayout.PreviewOnly ? new GridLength(0) : _editorHeight;
        previewRow.Height = layout switch
        {
            FormLayoutPaneLayout.PropertiesOnly => new GridLength(0),
            FormLayoutPaneLayout.PreviewOnly => new GridLength(1, GridUnitType.Star),
            _ => previewExpanded ? _expandedPreviewHeight : GridLength.Auto,
        };
    }
}
