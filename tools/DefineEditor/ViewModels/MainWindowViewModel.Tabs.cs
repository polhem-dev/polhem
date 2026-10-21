using CommunityToolkit.Mvvm.Input;
using Polhem.DefineEditor.Views;

namespace Polhem.DefineEditor.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task CloseDocument(DocumentViewModelBase? doc)
    {
        if (doc is null) return;
        var idx = OpenDocuments.IndexOf(doc);
        if (idx < 0) return;
        if (!await PrepareCloseAsync(new List<DocumentViewModelBase> { doc })) return;

        OpenDocuments.Remove(doc);
        doc.Dispose();

        if (ActiveDocument == doc)
        {
            ActiveDocument = OpenDocuments.Count == 0
                ? null
                : OpenDocuments[Math.Min(idx, OpenDocuments.Count - 1)];
        }
    }

    [RelayCommand]
    private async Task CloseOtherDocuments(DocumentViewModelBase? doc)
    {
        if (doc is null) return;
        var targets = OpenDocuments.Where(d => d != doc).ToList();
        if (!await PrepareCloseAsync(targets)) return;
        CloseDocuments(targets, activate: doc);
    }

    [RelayCommand]
    private async Task CloseDocumentsToTheRight(DocumentViewModelBase? doc)
    {
        if (doc is null) return;
        var idx = OpenDocuments.IndexOf(doc);
        if (idx < 0) return;
        var targets = OpenDocuments.Skip(idx + 1).ToList();
        if (!await PrepareCloseAsync(targets)) return;
        CloseDocuments(targets, activate: doc);
    }

    // Only ever closes clean tabs, so no unsaved-changes prompt is needed.
    [RelayCommand]
    private void CloseSavedDocuments() =>
        CloseDocuments(OpenDocuments.Where(d => !d.IsDirty).ToList(), activate: ActiveDocument);

    [RelayCommand]
    private async Task CloseAllDocuments()
    {
        var targets = OpenDocuments.ToList();
        if (!await PrepareCloseAsync(targets)) return;
        CloseDocuments(targets, activate: null);
    }

    /// <summary>
    /// Gatekeeper run before any tab-close action. When <paramref name="docs"/>
    /// contains dirty documents, prompts Save / Don't Save / Cancel (VS Code
    /// style). "Save" runs each dirty editor's own Save flow — if any of them
    /// is backed out (validation-error prompt cancelled) the document stays
    /// dirty and the whole close is aborted so no edits are lost. Returns
    /// <c>true</c> when the caller may proceed with closing. Headless contexts
    /// (no main window) skip the prompt, consistent with the other dialogs.
    /// </summary>
    private static async Task<bool> PrepareCloseAsync(IReadOnlyList<DocumentViewModelBase> docs)
    {
        var dirty = docs.Where(d => d.IsDirty).ToList();
        if (dirty.Count == 0) return true;

        var owner = GetOwnerWindow();
        if (owner is null) return true;

        var message = dirty.Count == 1
            ? L("Confirm_CloseUnsavedMessage", dirty[0].Title)
            : L("Confirm_CloseUnsavedMessageMulti", dirty.Count,
                string.Join("\n", dirty.Select(d => "• " + d.Title)));

        var choice = await ConfirmationDialog.ShowUnsavedAsync(
            owner,
            L("Confirm_CloseUnsavedTitle"),
            message,
            saveLabel: L("Action_Save"),
            discardLabel: L("Action_DontSave"),
            cancelLabel: L("Action_Cancel"));

        switch (choice)
        {
            case ConfirmCloseResult.Discard:
                return true;
            case ConfirmCloseResult.Save:
                foreach (var doc in dirty)
                {
                    if (doc.FileSaveCommand is IAsyncRelayCommand asyncSave)
                        await asyncSave.ExecuteAsync(null);
                    else
                        doc.FileSaveCommand?.Execute(null);
                }
                return dirty.All(d => !d.IsDirty);
            default:
                return false;
        }
    }

    /// <summary>
    /// Removes and disposes <paramref name="docs"/>, then re-points
    /// <see cref="ActiveDocument"/>: prefer <paramref name="activate"/> when it
    /// survived the close, otherwise fall back to the last remaining tab (or
    /// null when none are left). Shared by the tab context-menu close actions.
    /// </summary>
    private void CloseDocuments(IReadOnlyList<DocumentViewModelBase> docs, DocumentViewModelBase? activate)
    {
        foreach (var doc in docs)
        {
            OpenDocuments.Remove(doc);
            doc.Dispose();
        }

        if (activate is not null && OpenDocuments.Contains(activate))
            ActiveDocument = activate;
        else if (ActiveDocument is null || !OpenDocuments.Contains(ActiveDocument))
            ActiveDocument = OpenDocuments.LastOrDefault();
    }

    /// <summary>
    /// Saves every dirty document that supports saving, reusing each editor's
    /// own Save flow (including the save-despite-validation-errors prompt — a
    /// cancelled prompt leaves that document dirty and the batch moves on).
    /// </summary>
    [RelayCommand]
    private async Task SaveAll()
    {
        var targets = OpenDocuments
            .Where(d => d.IsDirty && d.FileSaveCommand is not null)
            .ToList();
        if (targets.Count == 0) return;

        var saved = 0;
        foreach (var doc in targets)
        {
            if (doc.FileSaveCommand is IAsyncRelayCommand asyncSave)
                await asyncSave.ExecuteAsync(null);
            else
                doc.FileSaveCommand!.Execute(null);
            if (!doc.IsDirty) saved++;
        }
        StatusText = L("Status_SavedAll", saved, targets.Count);
    }

    /// <summary>
    /// Drops every open document tab and disposes the view-models so their
    /// <see cref="LocalizationService.CultureChanged"/> subscriptions release.
    /// Used by <see cref="OpenSolution"/> when switching solutions.
    /// </summary>
    private void DisposeAndClearOpenDocuments()
    {
        foreach (var doc in OpenDocuments) doc.Dispose();
        OpenDocuments.Clear();
    }
}
