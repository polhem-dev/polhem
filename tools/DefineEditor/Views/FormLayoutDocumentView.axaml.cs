using System.ComponentModel;
using Avalonia.Controls;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

public partial class FormLayoutDocumentView : UserControl
{
    // The preview row's height while it is expanded; a collapsed preview keeps only its header row.
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
        ApplyPreviewRow();
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FormLayoutDocumentViewModel.IsPreviewVisible))
            ApplyPreviewRow();
    }

    private void ApplyPreviewRow()
    {
        var previewRow = RightPane.RowDefinitions[2];
        if (_document?.IsPreviewVisible ?? true)
        {
            previewRow.Height = _expandedPreviewHeight;
        }
        else
        {
            // Keep the height the user dragged the splitter to, so expanding again restores it.
            if (previewRow.Height.IsStar)
                _expandedPreviewHeight = previewRow.Height;
            previewRow.Height = GridLength.Auto;
        }
    }
}
