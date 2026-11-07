using Polhem.Definition.Layouts;

namespace Polhem.DefineEditor.BrowserPreview;

/// <summary>
/// A part of a previewed form by its position, as the browser page finds it in what it drew: the page and the editor
/// share no object, only the layout's order.
/// </summary>
/// <remarks>
/// The positions follow the order the form presenters draw in: the sections and then the details in layout order,
/// and in each only the visible fields or columns.
/// </remarks>
/// <param name="Kind">What kind of part it is.</param>
/// <param name="Index">The position of the section or the detail grid.</param>
/// <param name="Item">The position of the field or the column in it; -1 for a section or a grid.</param>
public readonly record struct PreviewTarget(PreviewTargetKind Kind, int Index, int Item = -1)
{
    /// <summary>Returns the layout object this target names in <paramref name="layout"/>, or <c>null</c>.</summary>
    /// <param name="layout">The layout the page drew.</param>
    public object? Resolve(FormLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return Kind switch
        {
            PreviewTargetKind.Section => At(layout.Sections, Index),
            PreviewTargetKind.Field => At(At(layout.Sections, Index)?.Fields?.Where(f => f.Visible), Item),
            PreviewTargetKind.Grid => At(layout.Details, Index),
            PreviewTargetKind.Column => At(At(layout.Details, Index)?.Columns?.Where(c => c.Visible), Item),
            _ => null,
        };
    }

    /// <summary>
    /// Returns the target that names <paramref name="layoutObject"/> in <paramref name="layout"/>, or <c>null</c>
    /// when the page draws no part for it (the layout itself, a hidden field, a folder).
    /// </summary>
    /// <param name="layout">The layout the page draws.</param>
    /// <param name="layoutObject">A section, a field, a detail grid or a column of <paramref name="layout"/>.</param>
    public static PreviewTarget? Locate(FormLayout layout, object? layoutObject)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var sections = layout.Sections?.ToList() ?? [];
        var details = layout.Details?.ToList() ?? [];
        switch (layoutObject)
        {
            case LayoutSection section when IndexOf(sections, section) is var index and >= 0:
                return new PreviewTarget(PreviewTargetKind.Section, index);
            case LayoutGrid grid when IndexOf(details, grid) is var index and >= 0:
                return new PreviewTarget(PreviewTargetKind.Grid, index);
            case LayoutField field:
                for (var i = 0; i < sections.Count; i++)
                {
                    var item = IndexOf(sections[i].Fields?.Where(f => f.Visible).ToList() ?? [], field);
                    if (item >= 0) return new PreviewTarget(PreviewTargetKind.Field, i, item);
                }
                return null;
            case LayoutColumn column:
                for (var i = 0; i < details.Count; i++)
                {
                    var item = IndexOf(details[i].Columns?.Where(c => c.Visible).ToList() ?? [], column);
                    if (item >= 0) return new PreviewTarget(PreviewTargetKind.Column, i, item);
                }
                return null;
            default:
                return null;
        }
    }

    private static T? At<T>(IEnumerable<T>? items, int index) where T : class
        => items is null || index < 0 ? null : items.Skip(index).FirstOrDefault();

    private static int IndexOf<T>(List<T> items, T item) where T : class
        => items.FindIndex(i => ReferenceEquals(i, item));
}
