using Polhem.DefineEditor.Models;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>A file in the Go to File list.</summary>
/// <param name="Node">The file's node in the Explorer view, which opening it selects.</param>
/// <param name="Detail">The file's path relative to the solution folder, shown under its name.</param>
public sealed record QuickOpenItem(DefineNode Node, string Detail)
{
    /// <summary>The file's name as the Explorer view shows it.</summary>
    public string Name => Node.Name;

    /// <summary>The key of the icon shown before the name.</summary>
    public string Icon => Node.Icon;
}
