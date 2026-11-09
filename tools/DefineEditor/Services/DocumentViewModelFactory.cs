using Polhem.Definition;
using Polhem.DefineEditor.Models;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Builds the appropriate <see cref="DocumentViewModelBase"/> for a selected
/// <see cref="DefineNode"/>. Returns <c>null</c> when nothing actionable is
/// selected (no node, or a grouping node).
/// </summary>
public static class DocumentViewModelFactory
{
    public static DocumentViewModelBase? Create(DefineNode? node, SolutionContext solution)
    {
        if (node is null || node.Kind != DefineNodeKind.DefineFile || node.FilePath is null)
            return null;

        var document = node.DefineType switch
        {
            DefineType.FormSchema => LoadOrFallback(node, () => FormSchemaDocumentViewModel.Load(node.FilePath!, solution)),
            DefineType.PermissionModels => LoadOrFallback(node, () => PermissionModelsDocumentViewModel.Load(node.FilePath!)),
            DefineType.DbCategorySettings => LoadOrFallback(node, () => DbCategorySettingsDocumentViewModel.Load(node.FilePath!)),
            DefineType.ProgramSettings => LoadOrFallback(node, () => ProgramSettingsDocumentViewModel.Load(node.FilePath!)),
            DefineType.MenuSettings => LoadOrFallback(node, () => MenuSettingsDocumentViewModel.Load(node.FilePath!, solution)),
            DefineType.SystemSettings => LoadOrFallback(node, () => SystemSettingsDocumentViewModel.Load(node.FilePath!)),
            DefineType.DatabaseSettings => LoadOrFallback(node, () => DatabaseSettingsDocumentViewModel.Load(node.FilePath!)),
            DefineType.TableSchema => LoadOrFallback(node, () => TableSchemaDocumentViewModel.Load(node.FilePath!)),
            DefineType.FormLayout => LoadOrFallback(node, () => FormLayoutDocumentViewModel.Load(node.FilePath!, solution, LastPreviewEngine())),
            DefineType.Language => LoadOrFallback(node, () => LanguageDocumentViewModel.Load(node.FilePath!)),
            _ => Unsupported(node, "An editor for this define type is not implemented yet."),
        };
        if (document is ObjectTreeDocumentViewModelBase tree)
            ViewModeMemory.Restore(tree, UserSettings.Load().DocumentViewModes);
        return document;
    }

    // The engine a FormLayout's preview was drawn by last, so each new layout opens the way the previous one was left.
    private static FormLayoutPreviewEngine LastPreviewEngine() => ParsePreviewEngine(UserSettings.Load().FormLayoutPreviewEngine);

    /// <summary>
    /// Reads a stored <see cref="FormLayoutPreviewEngine"/> name, falling back to
    /// <see cref="FormLayoutPreviewEngine.Avalonia"/> for anything unknown.
    /// </summary>
    /// <remarks>
    /// <c>Web</c>, written before the web preview offered a second package, reads as
    /// <see cref="FormLayoutPreviewEngine.Mud"/>, the package it drew with then.
    /// </remarks>
    /// <param name="name">The name stored in the user's settings.</param>
    public static FormLayoutPreviewEngine ParsePreviewEngine(string? name)
    {
        if (string.Equals(name, LegacyWebEngineName, StringComparison.Ordinal))
            return FormLayoutPreviewEngine.Mud;
        return Enum.TryParse<FormLayoutPreviewEngine>(name, out var engine) && Enum.IsDefined(engine)
            ? engine
            : FormLayoutPreviewEngine.Avalonia;
    }

    private const string LegacyWebEngineName = "Web";

    private static DocumentViewModelBase LoadOrFallback(DefineNode node, Func<DocumentViewModelBase> loader)
    {
        try { return loader(); }
        catch (Exception ex) when (ex is IOException
                                 or InvalidOperationException
                                 or UnauthorizedAccessException
                                 or FileNotFoundException)
        {
            return Unsupported(node, $"Load failed: {ex.Message}");
        }
    }

    private static UnsupportedDocumentViewModel Unsupported(DefineNode node, string note)
    {
        var summary = string.Join(Environment.NewLine,
            $"Type: {node.DefineType}",
            $"Key: {node.KeyText ?? "(singleton)"}",
            $"Path: {node.FilePath}",
            string.Empty,
            note);
        return new UnsupportedDocumentViewModel(node.Name, node.FilePath ?? node.Name, summary);
    }
}
