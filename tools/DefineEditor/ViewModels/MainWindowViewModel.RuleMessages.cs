using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.ViewModels;

public partial class MainWindowViewModel
{
    private void OnRuleMessagesApplied(object? sender, RuleMessagesAppliedEventArgs e) => WriteRuleMessages(e);

    /// <summary>
    /// Writes the translations of a drafted rule's message into the form's Language files. A file open with unsaved
    /// changes is left alone, since saving it would overwrite the translation; a file open without changes is closed,
    /// so its tab does not show the old text.
    /// </summary>
    public void WriteRuleMessages(RuleMessagesAppliedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Translations.Count == 0) return;
        var written = new List<string>();
        var problems = new List<string>();
        var stale = new List<DocumentViewModelBase>();
        foreach (var translation in e.Translations)
        {
            var name = Path.GetFileName(translation.File.Path);
            var open = OpenDocuments.OfType<ObjectTreeDocumentViewModelBase>()
                .FirstOrDefault(d => SamePath(d.FilePath, translation.File.Path));
            if (open is { IsDirty: true })
            {
                problems.Add(L("RuleAssistant_FileDirty", name));
                continue;
            }
            try
            {
                RuleMessageFiles.Write(translation.File, e.RuleId, translation.Text.Trim());
                written.Add(name);
                if (open is not null) stale.Add(open);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                problems.Add(L("RuleAssistant_FileFailed", name, ex.Message));
            }
        }
        if (stale.Count > 0) CloseDocuments(stale, ActiveDocument);
        StatusText = problems.Count == 0
            ? L("Status_RuleMessagesWritten", e.RuleId, string.Join(", ", written))
            : L("Status_RuleMessagesProblems", e.RuleId, string.Join(" ", problems));
    }
}
