
namespace Polhem.DefineEditor.Services;

/// <summary>
/// A program and its arguments that DefineEditor runs as a child process, such as the database manager. The arguments
/// are passed one by one and never through a shell, so no quoting or escaping applies.
/// </summary>
public sealed class ExternalCommand
{
    /// <summary>The program to run: a name found on the <c>PATH</c> (<c>dotnet</c>) or a path.</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>The arguments, each passed to the program as it is.</summary>
    public List<string> Arguments { get; set; } = new();

    /// <summary>
    /// The folder the program runs in. A relative path is resolved against the base directory given to
    /// <see cref="ResolveWorkingDirectory"/>; empty means that directory itself.
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>Environment variables set for the program, on top of the ones it inherits.</summary>
    public Dictionary<string, string> Environment { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Resolves <see cref="WorkingDirectory"/> against <paramref name="baseDirectory"/>.</summary>
    public string ResolveWorkingDirectory(string baseDirectory)
        => Path.GetFullPath(string.IsNullOrWhiteSpace(WorkingDirectory)
            ? baseDirectory
            : Path.Combine(baseDirectory, WorkingDirectory));

    /// <summary>The command line as a person would type it, with arguments that contain spaces quoted.</summary>
    public string ToDisplayString() => string.Join(' ', new[] { Command }.Concat(Arguments).Select(Quote));

    private static string Quote(string value)
        => value.Length == 0 || value.Any(char.IsWhiteSpace) ? $"\"{value}\"" : value;
}
