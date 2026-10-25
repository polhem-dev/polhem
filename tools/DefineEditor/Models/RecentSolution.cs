namespace Polhem.DefineEditor.Models;

/// <summary>
/// One entry of the recent-solutions list as the Welcome page and File → Open Recent show it.
/// </summary>
/// <param name="Path">The DefinePath folder, as recorded in the user settings.</param>
/// <param name="Name">The short name that identifies the folder among the other entries.</param>
/// <param name="Location">The folder that contains <paramref name="Name"/>, with the home directory shown as <c>~</c>.</param>
public sealed record RecentSolution(string Path, string Name, string Location)
{
    /// <summary>The single-line label of a native menu item, which cannot style the two parts apart.</summary>
    public string MenuLabel => string.IsNullOrEmpty(Location) ? Name : $"{Name} — {Location}";
}
