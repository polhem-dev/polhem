using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.DefineEditor.Views;

// The Avalonia preview and the tree select together: a click on a section, a field, a detail grid or a grid's column
// header selects its node, and the selected node's part of the form is outlined.
public partial class FormLayoutPreview
{
    // What each rendered part draws, and the part each layout object was drawn as. Both are rebuilt on every render,
    // since a render builds every control again.
    private readonly Dictionary<Control, object> _layoutObjects = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, Control> _parts = new(ReferenceEqualityComparer.Instance);

    private readonly Border _selectionOutline = new()
    {
        BorderThickness = new Thickness(2),
        CornerRadius = new CornerRadius(4),
        Margin = new Thickness(-3),
        IsHitTestVisible = false,
    };

    private Visual? _outlinedPart;

    private void InitializeSelection()
    {
        // The outline sits just outside the part, and a grid clips its column headers to their own bounds.
        AdornerLayer.SetIsClipEnabled(_selectionOutline, false);
        // Tunnelling, and with handled events too, so a field editor still takes its own click afterwards.
        PreviewSheet.AddHandler(PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    // NOTE: this follows how FormLayoutPresenter builds the form: one part per section and then one per detail grid,
    // in layout order, and in each section one cell per visible field, in order, in the first Grid inside it.
    private void MapRenderedParts(FormLayout layout)
    {
        ClearRenderedParts();
        if (LayoutPresenter.Content is not Panel host) return;

        var sections = layout.Sections?.ToList() ?? [];
        var details = layout.Details?.ToList() ?? [];
        for (var i = 0; i < host.Children.Count; i++)
        {
            var part = host.Children[i];
            if (i < sections.Count)
                MapSection(part, sections[i]);
            else if (i - sections.Count < details.Count)
                Map(part, details[i - sections.Count]);
        }
    }

    private void MapSection(Control part, LayoutSection section)
    {
        Map(part, section);
        if (part.GetLogicalDescendants().OfType<Grid>().FirstOrDefault() is not { } cells) return;
        var fields = section.Fields?.Where(f => f.Visible).ToList() ?? [];
        for (var i = 0; i < Math.Min(cells.Children.Count, fields.Count); i++)
            Map(cells.Children[i], fields[i]);
    }

    private void Map(Control part, object layoutObject)
    {
        _layoutObjects[part] = layoutObject;
        _parts[layoutObject] = part;
    }

    private void ClearRenderedParts()
    {
        _layoutObjects.Clear();
        _parts.Clear();
        HideSelectionOutline();
    }

    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_document is { IsWebPreview: false } document && e.Source is Visual source && LayoutObjectAt(source) is { } target)
            document.SelectNodeFor(target);
    }

    // The innermost part around the clicked visual: a column header before its grid, a field before its section.
    private object? LayoutObjectAt(Visual source)
    {
        for (var visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is DataGridColumnHeader header && header.FindAncestorOfType<GridControl>() is { } grid
                && ColumnOf(grid, header) is { } column)
            {
                return column;
            }
            if (visual is Control control && _layoutObjects.TryGetValue(control, out var layoutObject))
                return layoutObject;
        }
        return null;
    }

    // NOTE: the grid builds one column per visible layout column, in layout order, as ShowColumnFieldNames relies on too.
    private static LayoutColumn? ColumnOf(GridControl grid, DataGridColumnHeader header)
    {
        var index = grid.InnerGrid.Columns.ToList().FindIndex(c => ReferenceEquals(c.Header, header.Content));
        var visible = grid.Layout?.Columns?.Where(c => c.Visible).ToList();
        return index >= 0 && visible is not null && index < visible.Count ? visible[index] : null;
    }

    private static DataGridColumnHeader? HeaderOf(LayoutColumn column, IEnumerable<GridControl> grids)
    {
        foreach (var grid in grids)
        {
            var index = grid.Layout?.Columns?.Where(c => c.Visible).ToList().IndexOf(column) ?? -1;
            if (index < 0 || index >= grid.InnerGrid.Columns.Count) continue;
            var columnHeader = grid.InnerGrid.Columns[index].Header;
            return grid.GetVisualDescendants().OfType<DataGridColumnHeader>()
                .FirstOrDefault(h => ReferenceEquals(h.Content, columnHeader));
        }
        return null;
    }

    // A render builds the controls, and a grid's column headers exist only once it has been laid out, so the outline
    // waits for that.
    private void ShowSelectionOutlineLater() => Dispatcher.UIThread.Post(ShowSelectionOutline, DispatcherPriority.Loaded);

    private void ShowSelectionOutline()
    {
        var part = _document is { IsPreviewView: true, IsWebPreview: false, SelectedTreeNode: { IsFolder: false, Value: { } value } }
            ? PartFor(value)
            : null;
        if (ReferenceEquals(part, _outlinedPart)) return;

        HideSelectionOutline();
        if (part is null) return;
        _selectionOutline.BorderBrush = this.TryFindResource("SemiColorPrimary", ActualThemeVariant, out var brush) && brush is IBrush b
            ? b
            : Brushes.DodgerBlue;
        AdornerLayer.SetAdorner(part, _selectionOutline);
        _outlinedPart = part;
    }

    private Visual? PartFor(object layoutObject) => layoutObject switch
    {
        LayoutColumn column => HeaderOf(column, LayoutPresenter.GetLogicalDescendants().OfType<GridControl>()),
        _ => _parts.GetValueOrDefault(layoutObject),
    };

    private void HideSelectionOutline()
    {
        if (_outlinedPart is null) return;
        AdornerLayer.SetAdorner(_outlinedPart, null);
        _outlinedPart = null;
    }
}
