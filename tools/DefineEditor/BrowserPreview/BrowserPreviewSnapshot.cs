using Polhem.Definition.Layouts;

namespace Polhem.DefineEditor.BrowserPreview;

/// <summary>
/// One state of a FormLayout document as the browser preview draws it: the layout as XML and the preview options
/// chosen in the editor.
/// </summary>
/// <param name="Title">The browser tab's title.</param>
/// <param name="LayoutXml">The layout serialized as it would be saved, or empty when <paramref name="Error"/> is set.</param>
/// <param name="FormMode">The form mode the editors render in.</param>
/// <param name="IsCompact">Whether to render for a phone-sized screen.</param>
/// <param name="ShowFieldNames">Whether empty editors show their field name.</param>
/// <param name="Error">Why the layout could not be serialized, or <c>null</c>.</param>
public sealed record BrowserPreviewSnapshot(
    string Title,
    string LayoutXml,
    SingleFormMode FormMode,
    bool IsCompact,
    bool ShowFieldNames,
    string? Error = null);
