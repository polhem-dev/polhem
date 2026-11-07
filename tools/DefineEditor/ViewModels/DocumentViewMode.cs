namespace Polhem.DefineEditor.ViewModels;

/// <summary>What the body of a define document shows; the buttons at the left of its toolbar choose it.</summary>
public enum DocumentViewMode
{
    /// <summary>The tree beside the property grid.</summary>
    Design,

    /// <summary>
    /// The tree beside a preview of what the define object draws, for the documents that have one
    /// (<see cref="ObjectTreeDocumentViewModelBase.SupportsPreview"/>).
    /// </summary>
    Preview,

    /// <summary>Only the XML.</summary>
    Xml,

    /// <summary>The tree and the property grid above the XML.</summary>
    Split,
}
