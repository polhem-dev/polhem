namespace Polhem.DefineEditor.ViewModels;

/// <summary>What the right-hand side of the FormLayout editor shows.</summary>
public enum FormLayoutPaneLayout
{
    /// <summary>The property grid above the live preview.</summary>
    PropertiesAndPreview,

    /// <summary>Only the live preview; a node's properties are edited in a dialog.</summary>
    PreviewOnly,

    /// <summary>Only the property grid.</summary>
    PropertiesOnly,
}
