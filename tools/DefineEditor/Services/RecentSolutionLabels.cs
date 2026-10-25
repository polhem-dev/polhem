using Polhem.DefineEditor.Models;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Turns the recorded DefinePath folders into the short names and locations the recent lists show.
/// </summary>
/// <remarks>
/// Most DefinePath folders are called <c>Define</c>, so the folder's own name says nothing; the folder above it
/// names the application. Names that still collide grow one parent folder at a time until they differ, the way
/// VS Code tells apart editor tabs of files with the same name.
/// </remarks>
public static class RecentSolutionLabels
{
    private const string DefineFolderName = "Define";

    /// <summary>Builds the entries for <paramref name="paths"/> against the current user's home directory.</summary>
    public static IReadOnlyList<RecentSolution> Build(IReadOnlyList<string> paths) =>
        Build(paths, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Directory.Exists);

    /// <summary>Builds the entries for <paramref name="paths"/>, keeping their order.</summary>
    /// <param name="paths">The recorded DefinePath folders.</param>
    /// <param name="homeDirectory">The folder shown as <c>~</c>; empty to show every location in full.</param>
    /// <param name="folderExists">Tells whether a recorded folder is still there.</param>
    public static IReadOnlyList<RecentSolution> Build(
        IReadOnlyList<string> paths, string homeDirectory, Func<string, bool> folderExists)
    {
        var anchors = paths.Select(GetAnchor).ToArray();
        var depths = new int[paths.Count];
        Array.Fill(depths, 1);

        bool grew;
        do
        {
            grew = false;
            var names = anchors.Select((anchor, i) => JoinTail(anchor, depths[i])).ToArray();
            foreach (var group in Enumerable.Range(0, names.Length)
                         .GroupBy(i => names[i], StringComparer.OrdinalIgnoreCase)
                         .Where(g => g.Count() > 1))
            {
                foreach (var i in group.Where(i => CanGrow(anchors[i], depths[i])))
                {
                    depths[i]++;
                    grew = true;
                }
            }
        }
        while (grew);

        return paths.Select((path, i) => new RecentSolution(
                path,
                JoinTail(anchors[i], depths[i]),
                ShortenHome(Ascend(anchors[i], depths[i]), homeDirectory),
                folderExists(path)))
            .ToArray();
    }

    /// <summary>The folder whose name stands for <paramref name="path"/>: its parent when it is a <c>Define</c> folder.</summary>
    private static string GetAnchor(string path)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(path);
        var parent = Path.GetDirectoryName(trimmed);
        return string.Equals(Path.GetFileName(trimmed), DefineFolderName, StringComparison.OrdinalIgnoreCase)
               && !string.IsNullOrEmpty(Path.GetFileName(parent))
            ? parent!
            : trimmed;
    }

    /// <summary>The last <paramref name="depth"/> folder names of <paramref name="anchor"/>.</summary>
    private static string JoinTail(string anchor, int depth)
    {
        var parts = new List<string>();
        var current = anchor;
        for (var i = 0; i < depth && !string.IsNullOrEmpty(Path.GetFileName(current)); i++)
        {
            parts.Insert(0, Path.GetFileName(current));
            current = Path.GetDirectoryName(current) ?? string.Empty;
        }
        return parts.Count == 0 ? anchor : string.Join(Path.DirectorySeparatorChar, parts);
    }

    /// <summary>The folder above the last <paramref name="depth"/> names of <paramref name="anchor"/>.</summary>
    private static string Ascend(string anchor, int depth)
    {
        var current = anchor;
        for (var i = 0; i < depth && !string.IsNullOrEmpty(Path.GetFileName(current)); i++)
            current = Path.GetDirectoryName(current) ?? string.Empty;
        return current;
    }

    private static bool CanGrow(string anchor, int depth) =>
        !string.IsNullOrEmpty(Path.GetFileName(Ascend(anchor, depth)));

    private static string ShortenHome(string location, string homeDirectory)
    {
        if (string.IsNullOrEmpty(homeDirectory)) return location;
        var home = Path.TrimEndingDirectorySeparator(homeDirectory);
        if (string.Equals(location, home, StringComparison.OrdinalIgnoreCase)) return "~";
        return location.StartsWith(home + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? "~" + location[home.Length..]
            : location;
    }
}
