using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Polhem.Core.Serialization;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.ViewModels;

// The XML view: the define object as XmlSerializer writes it, the same text a save would put in the file, unsaved edits
// included. It is read-only for now; the tree and the property grid stay the only way to change the object.
public abstract partial class ObjectTreeDocumentViewModelBase
{
    /// <summary>Whether the document shows its XML instead of the tree and the property grid.</summary>
    [ObservableProperty]
    private bool _isXmlView;

    /// <summary>The define object as XML, read when the XML view opens and after every change while it is open.</summary>
    [ObservableProperty]
    private string _xmlText = string.Empty;

    /// <summary>Shows the tree and the property grid.</summary>
    [RelayCommand]
    private void ShowDesign() => IsXmlView = false;

    /// <summary>Shows the XML.</summary>
    [RelayCommand]
    private void ShowXml() => IsXmlView = true;

    partial void OnIsXmlViewChanged(bool value)
    {
        if (value) RefreshXmlText();
    }

    /// <summary>
    /// Where the selected tree node's element starts in <see cref="XmlText"/>, or <c>null</c> when it cannot be found.
    /// </summary>
    public XmlTextPosition? LocateSelectedNodeInXml() =>
        SelectedTreeNode is { } node ? XmlElementLocator.Find(XmlText, RootObject, node.Value) : null;

    private void RefreshXmlText() => XmlText = XmlCodec.Serialize(RootObject);

    // Undo and Redo keep working while the XML is shown, so the text follows every change of the object.
    private void OnDocumentChangedForXml()
    {
        if (IsXmlView) RefreshXmlText();
    }
}
