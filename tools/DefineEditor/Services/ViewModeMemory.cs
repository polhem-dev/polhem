using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Remembers, for each kind of define document, the view it was left in (<see cref="DocumentViewMode"/>), so the next
/// document of that kind opens the same way.
/// </summary>
/// <remarks>The views are kept in <see cref="UserSettings.DocumentViewModes"/>, by the document's type name.</remarks>
public static class ViewModeMemory
{
    /// <summary>Records the view <paramref name="document"/> shows now in <paramref name="saved"/>.</summary>
    /// <param name="document">The document whose view to record.</param>
    /// <param name="saved">The views by document kind, which the settings file keeps.</param>
    public static void Remember(ObjectTreeDocumentViewModelBase document, IDictionary<string, string> saved)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(saved);
        saved[KeyOf(document)] = document.ViewMode.ToString();
    }

    /// <summary>
    /// Shows <paramref name="document"/> in the view recorded for its kind. A view that is unknown, or that the
    /// document cannot show (a preview for a document without one), leaves the document in Design.
    /// </summary>
    /// <param name="document">The document just opened.</param>
    /// <param name="saved">The views by document kind, which the settings file keeps.</param>
    public static void Restore(ObjectTreeDocumentViewModelBase document, IReadOnlyDictionary<string, string> saved)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(saved);
        if (!saved.TryGetValue(KeyOf(document), out var name)
            || !Enum.TryParse<DocumentViewMode>(name, out var mode)
            || !Enum.IsDefined(mode)
            || (mode == DocumentViewMode.Preview && !document.SupportsPreview))
        {
            return;
        }
        document.ViewMode = mode;
    }

    private static string KeyOf(ObjectTreeDocumentViewModelBase document) => document.GetType().Name;
}
