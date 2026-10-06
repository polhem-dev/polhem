using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

public partial class FormLayoutDocumentView : UserControl
{
    public FormLayoutDocumentView()
    {
        InitializeComponent();
        Tree.IconSelector = node =>
            Application.Current is { } app
            && app.TryGetResource(FormLayoutDocumentViewModel.IconKeyFor(node), app.ActualThemeVariant, out var resource)
                ? resource as Geometry
                : null;
    }
}
