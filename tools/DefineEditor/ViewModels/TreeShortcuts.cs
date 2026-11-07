namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// How the tree's keys are written in tooltips. The keys themselves are handled by
/// <see cref="Polhem.DefineEditor.Views.TreeKeyboard"/>; a key changed there is changed here too.
/// </summary>
public static class TreeShortcuts
{
    private static readonly bool s_isMacOS = OperatingSystem.IsMacOS();

    /// <summary>Delete the selected node.</summary>
    public static string Delete => s_isMacOS ? "⌫" : "Del";

    /// <summary>Duplicate the selected node.</summary>
    public static string Duplicate => s_isMacOS ? "⌘D" : "Ctrl+D";

    /// <summary>Move the selected node up among its siblings.</summary>
    public static string MoveUp => s_isMacOS ? "⌥↑" : "Alt+Up";

    /// <summary>Move the selected node down among its siblings.</summary>
    public static string MoveDown => s_isMacOS ? "⌥↓" : "Alt+Down";
}
