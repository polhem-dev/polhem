using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Polhem.DefineEditor.BrowserPreview;

/// <summary>
/// Hands the latest snapshot of each FormLayout document from the editor to the browser pages that show it.
/// </summary>
/// <remarks>
/// The editor publishes on its UI thread and the pages read on their circuits, so the hub only ever passes immutable
/// snapshots; a page never sees the layout instance the editor changes in place.
/// </remarks>
public sealed class BrowserPreviewHub
{
    private readonly ConcurrentDictionary<string, string> _documentIds = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, BrowserPreviewSnapshot> _snapshots = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PreviewTarget?> _selections = new(StringComparer.Ordinal);

    /// <summary>
    /// Raised with the document id after <see cref="Publish"/> stores a new snapshot.
    /// </summary>
    public event Action<string>? SnapshotPublished;

    /// <summary>
    /// Returns the id that names a document in preview URLs, the same id for the same document key for the life of
    /// the hub.
    /// </summary>
    /// <remarks>
    /// The id is random rather than derived from the key, so a URL does not carry the file path of the definition.
    /// </remarks>
    /// <param name="documentKey">The document's key, its file path.</param>
    public string GetDocumentId(string documentKey)
        => _documentIds.GetOrAdd(documentKey, static _ => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8)));

    /// <summary>
    /// Stores <paramref name="snapshot"/> as the latest state of the document and notifies the pages showing it.
    /// </summary>
    public void Publish(string documentId, BrowserPreviewSnapshot snapshot)
    {
        _snapshots[documentId] = snapshot;
        SnapshotPublished?.Invoke(documentId);
    }

    /// <summary>
    /// Returns the latest snapshot of the document, or <c>null</c> when none was published.
    /// </summary>
    public BrowserPreviewSnapshot? GetSnapshot(string documentId)
        => _snapshots.TryGetValue(documentId, out var snapshot) ? snapshot : null;

    /// <summary>
    /// Raised with the document id and the part when a page asks the editor to select what was clicked in it.
    /// </summary>
    /// <remarks>It is raised on the page's circuit, not on the editor's UI thread.</remarks>
    public event Action<string, PreviewTarget>? SelectionRequested;

    /// <summary>Raised with the document id after <see cref="PublishSelection"/> stores the editor's selection.</summary>
    public event Action<string>? SelectionPublished;

    /// <summary>Asks the editor showing the document to select <paramref name="target"/>.</summary>
    public void RequestSelection(string documentId, PreviewTarget target)
        => SelectionRequested?.Invoke(documentId, target);

    /// <summary>
    /// Stores the part of the form the editor has selected, or <c>null</c> for none, and notifies the pages showing
    /// the document, which outline it.
    /// </summary>
    public void PublishSelection(string documentId, PreviewTarget? target)
    {
        _selections[documentId] = target;
        SelectionPublished?.Invoke(documentId);
    }

    /// <summary>Returns the part of the form the editor has selected, or <c>null</c>.</summary>
    public PreviewTarget? GetSelection(string documentId)
        => _selections.TryGetValue(documentId, out var target) ? target : null;
}
