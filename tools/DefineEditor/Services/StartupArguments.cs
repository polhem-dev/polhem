namespace Polhem.DefineEditor.Services;

/// <summary>
/// Reads the command line DefineEditor was started with: <c>Polhem.DefineEditor [&lt;DefinePath&gt;]</c>. A project
/// task opens its own definitions this way, for example <c>dotnet polhem-define Define</c>.
/// </summary>
public static class StartupArguments
{
    /// <summary>
    /// Returns the DefinePath to open, as a full path, or <see langword="null"/> when the command line names none.
    /// </summary>
    /// <remarks>
    /// The first argument that does not start with <c>-</c> is the path; a relative path is resolved against
    /// <paramref name="currentDirectory"/>. Options are skipped, so the <c>-psn_…</c> argument that older macOS versions
    /// pass to an app started from Finder is not mistaken for a folder.
    /// </remarks>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="currentDirectory">The folder a relative path is resolved against.</param>
    public static string? GetSolutionPath(IEnumerable<string>? args, string currentDirectory)
    {
        var path = args?.FirstOrDefault(a => a.Length > 0 && !a.StartsWith('-'));
        if (path is null) return null;
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path, currentDirectory));
    }
}
