using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Polhem.Core.Serialization;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.Views;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.DefineEditor.ViewModels;

// The XML view: the define object as XML, which can be edited as text. The text is read back into the object after a
// pause in typing (DefineXmlParser); a text that cannot be read leaves the object as it was and lists what is wrong.
//
// The text belongs to the person typing it: reading it back never rewrites it, so their layout and comments stay. A
// document that was only edited as XML since the XML was shown is saved as typed (SaveText). An edit in the tree
// generates the XML again from the object, as every save from the tree does.
public abstract partial class ObjectTreeDocumentViewModelBase
{
    // Text typed in the XML view and not read back yet.
    private string? _pendingXmlText;

    // Whether XmlText is what a save writes: it was typed, or read from the file, and no edit in the tree came since.
    private bool _isXmlTextTheSource;

    // Set while a typed text is being applied, so the change it causes does not generate the XML again.
    private bool _isApplyingXml;

    /// <summary>Whether the document shows its XML instead of the tree and the property grid.</summary>
    [ObservableProperty]
    private bool _isXmlView;

    /// <summary>
    /// The XML the view shows: the file as it is on disk while the document has no unsaved edit, otherwise the define
    /// object as a save from the tree would write it, or the text typed since.
    /// </summary>
    [ObservableProperty]
    private string _xmlText = string.Empty;

    /// <summary>What is wrong with the XML typed last; empty when it was read into the document.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasXmlIssues))]
    private IReadOnlyList<XmlParseIssue> _xmlIssues = [];

    /// <summary>Whether the XML typed last could not be read into the document.</summary>
    public bool HasXmlIssues => XmlIssues.Count > 0;

    /// <summary>Describes <paramref name="issue"/> for the XML view's problem list, without its position.</summary>
    public static string DescribeXmlIssue(XmlParseIssue issue) => issue.Kind switch
    {
        XmlParseIssueKind.UnknownElement => L("XmlIssue_UnknownElement", issue.Detail),
        XmlParseIssueKind.UnknownAttribute => L("XmlIssue_UnknownAttribute", issue.Detail),
        XmlParseIssueKind.Empty => L("XmlIssue_Empty"),
        _ => issue.Detail,
    };

    /// <summary>The position of <paramref name="issue"/>, as the XML view's problem list shows it.</summary>
    public static string DescribeXmlIssuePosition(XmlParseIssue issue) =>
        L("XmlIssue_Position", issue.Line, issue.Column);

    /// <summary>Shows the tree and the property grid, after asking what to do with XML that cannot be read.</summary>
    [RelayCommand]
    private async Task ShowDesign()
    {
        if (!IsXmlView) return;
        if (!await ResolveXmlIssuesAsync()) return;
        IsXmlView = false;
    }

    /// <summary>Shows the XML.</summary>
    [RelayCommand]
    private void ShowXml() => IsXmlView = true;

    partial void OnIsXmlViewChanged(bool value)
    {
        if (value && !_isXmlTextTheSource) RefreshXmlText();
    }

    /// <summary>
    /// Where the selected tree node's element starts in <see cref="XmlText"/>, or <c>null</c> when it cannot be found.
    /// </summary>
    public XmlTextPosition? LocateSelectedNodeInXml() =>
        SelectedTreeNode is { } node ? XmlElementLocator.Find(XmlText, RootObject, node.Value) : null;

    /// <summary>The type of the define object, whose XML mapping the XML view's completion follows.</summary>
    public Type XmlRootType => RootObject.GetType();

    /// <summary>
    /// The values the property grid would offer for the attribute <paramref name="attribute"/> of the element at
    /// <paramref name="steps"/>, such as the solution's ProgIds or the fields of a table; <c>null</c> when it offers
    /// none, or when the element is not in the document yet.
    /// </summary>
    /// <remarks>
    /// The element is looked up in the define object, which follows the XML as it was last read, so an element typed
    /// since is not found until the text is read again.
    /// </remarks>
    public IReadOnlyList<string>? XmlValueSuggestions(IReadOnlyList<XmlElementStep> steps, string attribute)
    {
        if (ValueSuggestionProvider is not { } provider
            || DefineXmlObjectMap.ObjectAt(RootObject, steps) is not { } owner
            || XmlMemberRules.AttributeProperty(owner.GetType(), attribute) is not { } property
            || TypeDescriptor.GetProperties(owner)[property.Name] is not { } descriptor)
        {
            return null;
        }
        return provider(descriptor, owner) is { Count: > 0 } values ? values : null;
    }

    /// <summary>
    /// What the property grid says about the attribute <paramref name="attribute"/> of the element at
    /// <paramref name="steps"/>: its description, in the editor's language; <c>null</c> when it has none.
    /// </summary>
    /// <remarks>The element's type is read from the definition types, so an element not read back yet has help too.</remarks>
    public string? XmlAttributeHelp(IReadOnlyList<XmlElementStep> steps, string attribute)
    {
        if (DefineXmlObjectMap.TypeAt(XmlRootType, steps) is not { } type
            || XmlMemberRules.AttributeProperty(type, attribute) is not { } property
            || property.GetCustomAttribute<DescriptionAttribute>()?.Description is not { Length: > 0 } description)
        {
            return null;
        }
        return PropertyLabels.Translate(new PropertyGridText(
            PropertyGridTextKind.Description, type, property.Name, description)) ?? description;
    }

    /// <summary>Notes text typed in the XML view; <see cref="FlushXmlEdit"/> reads it into the document.</summary>
    public void OnXmlTextEdited(string text)
    {
        _pendingXmlText = text;
        XmlText = text;
        IsDirty = true;
    }

    /// <summary>
    /// Reads the text typed in the XML view since the last call into the document. A text that cannot be read leaves
    /// the document as it was and fills <see cref="XmlIssues"/>.
    /// </summary>
    public void FlushXmlEdit()
    {
        if (_pendingXmlText is not { } text) return;
        _pendingXmlText = null;
        ApplyXmlText(text);
    }

    private void ApplyXmlText(string text)
    {
        var result = DefineXmlParser.Parse(text, RootObject.GetType());
        XmlIssues = result.Issues;
        if (!result.Succeeded)
        {
            StatusText = L("XmlView_StatusInvalid", result.Issues.Count);
            return;
        }

        _isXmlTextTheSource = true;
        if (XmlCodec.Serialize(result.Value!) != XmlCodec.Serialize(RootObject))
        {
            _isApplyingXml = true;
            try
            {
                ReplaceDocumentRoot(result.Value!);
            }
            finally
            {
                _isApplyingXml = false;
            }
        }

        // A change of layout or comments alone leaves the object as it was, but the file a save writes changes.
        IsDirty = !IsAtSavedState || !MatchesFile(text);
        StatusText = L("XmlView_StatusApplied");
    }

    // Asks what to do with XML that cannot be read: back to it to fix it (false), or throw it away (true).
    private async Task<bool> ResolveXmlIssuesAsync()
    {
        FlushXmlEdit();
        if (!HasXmlIssues) return true;

        var owner = GetOwnerWindow();
        if (owner is not null)
        {
            var discard = await ConfirmationDialog.ShowAsync(
                owner,
                L("XmlView_DiscardTitle"),
                L("XmlView_DiscardMessage", XmlIssues.Count),
                confirmLabel: L("XmlView_Discard"),
                cancelLabel: L("XmlView_KeepEditing"));
            if (!discard) return false;
        }

        // The typed text marked the document unsaved; without it, only the object's own edits count.
        IsDirty = !IsAtSavedState;
        RefreshXmlText();
        StatusText = L("XmlView_StatusDiscarded");
        return true;
    }

    // The XML again from the document: the file as it is when nothing is unsaved, which keeps its comments, otherwise
    // the object as a save from the tree writes it.
    private void RefreshXmlText()
    {
        _pendingXmlText = null;
        XmlIssues = [];
        if (!IsDirty && ReadFile() is { } file)
        {
            _isXmlTextTheSource = true;
            XmlText = file;
            return;
        }

        _isXmlTextTheSource = false;
        XmlText = XmlCodec.Serialize(RootObject);
    }

    // An edit in the tree, an undo or a redo: the XML now comes from the object.
    private void OnDocumentChangedForXml()
    {
        if (_isApplyingXml) return;
        _isXmlTextTheSource = false;
        if (IsXmlView) RefreshXmlText();
    }

    /// <summary>
    /// The text a save writes when the document was only edited as XML since the XML was shown; <c>null</c> when a
    /// save writes the object.
    /// </summary>
    private string? SaveText => _isXmlTextTheSource && !HasXmlIssues ? XmlText : null;

    private bool MatchesFile(string text) => ReadFile() is { } file && file == text;

    private string? ReadFile()
    {
        try
        {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
