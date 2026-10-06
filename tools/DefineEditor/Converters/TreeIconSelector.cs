using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Polhem.Definition.ObjectTree;

namespace Polhem.DefineEditor.Converters;

/// <summary>
/// Turns an editor's node-to-resource-key mapping into the icon selector of an <c>ObjectTreeView</c>,
/// resolving the key against the application resources for the current theme.
/// </summary>
public static class TreeIconSelector
{
    /// <summary>
    /// Binds a view model's <see cref="ViewModels.ObjectTreeDocumentViewModelBase.IconKeys"/> to
    /// <c>ObjectTreeView.IconSelector</c>, so the view model stays free of Avalonia types.
    /// </summary>
    public static IValueConverter Converter { get; } =
        new FuncValueConverter<Func<ObjectTreeNode, string>?, Func<ObjectTreeNode, Geometry?>?>(
            iconKeyFor => iconKeyFor is null ? null : From(iconKeyFor));

    public static Func<ObjectTreeNode, Geometry?> From(Func<ObjectTreeNode, string> iconKeyFor) =>
        node => Resolve(iconKeyFor(node));

    public static Geometry? Resolve(string key) =>
        Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out var resource)
            ? resource as Geometry
            : null;
}
