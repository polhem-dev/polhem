namespace Polhem.DefineEditor.ViewModels;

/// <summary>What draws the FormLayout editor's live preview.</summary>
public enum FormLayoutPreviewEngine
{
    /// <summary>The framework's Avalonia controls, drawn by the editor itself.</summary>
    Avalonia,

    /// <summary>
    /// The browser preview page, shown in a web view inside the editor: the framework's Blazor components, drawn by
    /// the system's web engine.
    /// </summary>
    Web,
}
