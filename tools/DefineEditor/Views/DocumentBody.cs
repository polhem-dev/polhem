using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

/// <summary>
/// The body of a define document: its design (the tree and the property grid, the first child) and its
/// <see cref="XmlDocumentPane"/>, shown one at a time or one above the other with a splitter between them, as the document's
/// <see cref="ObjectTreeDocumentViewModelBase.ViewMode"/> says.
/// </summary>
public sealed class DocumentBody : Grid
{
    private readonly GridSplitter _splitter = new()
    {
        ResizeDirection = GridResizeDirection.Rows,
        VerticalAlignment = VerticalAlignment.Stretch,
    };

    private ObjectTreeDocumentViewModelBase? _document;

    // One above the other, as a XAML designer splits by default: both keep the full width, which the tree's toolbar
    // and the XML's long lines need; side by side, the toolbar ran into its find box.
    public DocumentBody()
    {
        RowDefinitions = new RowDefinitions("*,4,*");
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        SetRow(_splitter, 1);
        Children.Add(_splitter);
        Layout();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_document is not null) _document.PropertyChanged -= OnDocumentPropertyChanged;
        _document = DataContext as ObjectTreeDocumentViewModelBase;
        if (_document is not null) _document.PropertyChanged += OnDocumentPropertyChanged;
        Layout();
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The next document of this kind opens in the view chosen here.
        if (e.PropertyName == nameof(ObjectTreeDocumentViewModelBase.ViewMode) && _document is not null)
        {
            var settings = UserSettings.Load();
            ViewModeMemory.Remember(_document, settings.DocumentViewModes);
            settings.Save();
        }

        if (e.PropertyName is nameof(ObjectTreeDocumentViewModelBase.ViewMode)
            or nameof(ObjectTreeDocumentViewModelBase.IsDesignEnabled))
        {
            Layout();
        }
    }

    private void Layout()
    {
        var design = Children.FirstOrDefault(c => c is not XmlDocumentPane && c != _splitter);
        var xml = Children.OfType<XmlDocumentPane>().FirstOrDefault();
        if (design is null || xml is null) return;

        var split = _document?.IsSplitView == true;
        design.IsVisible = _document?.IsDesignVisible ?? true;
        design.IsEnabled = _document?.IsDesignEnabled ?? true;
        ToolTip.SetShowOnDisabled(design, true);
        ToolTip.SetTip(design, design.IsEnabled ? null : LocalizationService.Current["XmlView_DesignLocked"]);
        xml.IsVisible = _document?.IsXmlView == true;
        _splitter.IsVisible = split;

        SetRow(design, 0);
        SetRowSpan(design, split ? 1 : 3);
        SetRow(xml, split ? 2 : 0);
        SetRowSpan(xml, split ? 1 : 3);
    }
}
