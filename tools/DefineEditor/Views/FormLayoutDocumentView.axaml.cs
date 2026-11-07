using System.ComponentModel;
using Avalonia.Controls;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

public partial class FormLayoutDocumentView : UserControl
{
    // The preview gets most of the width, since the form it draws is wider than a property grid. Each view keeps the
    // widths the splitter was dragged to, so going back to it restores them.
    private (GridLength Tree, GridLength Side) _designWidths = (new(1, GridUnitType.Star), new(1.2, GridUnitType.Star));
    private (GridLength Tree, GridLength Side) _previewWidths = (new(1, GridUnitType.Star), new(2.5, GridUnitType.Star));
    private bool _showsPreview;
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
        ApplyColumns();
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FormLayoutDocumentViewModel.ViewMode))
        {
            ApplyColumns();
        }
        // The next layout opened starts with the engine chosen here.
        else if (e.PropertyName == nameof(FormLayoutDocumentViewModel.PreviewEngine) && _document is not null)
        {
            var settings = UserSettings.Load();
            settings.FormLayoutPreviewEngine = _document.PreviewEngine.ToString();
            settings.Save();
        }
    }

    private void ApplyColumns()
    {
        var tree = DesignGrid.ColumnDefinitions[0];
        var side = DesignGrid.ColumnDefinitions[2];
        if (_showsPreview)
            _previewWidths = (tree.Width, side.Width);
        else
            _designWidths = (tree.Width, side.Width);

        _showsPreview = _document?.IsPreviewView == true;
        (tree.Width, side.Width) = _showsPreview ? _previewWidths : _designWidths;
    }
}
