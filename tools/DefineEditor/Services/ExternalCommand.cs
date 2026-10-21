using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// A program and its arguments that DefineEditor runs as a child process, such as the database upgrade command of a
/// solution. The arguments are passed one by one and never through a shell, so no quoting or escaping applies.
/// </summary>
public sealed class ExternalCommand
{
    /// <summary>The program to run: a name found on the <c>PATH</c> (<c>dotnet</c>) or a path.</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>The arguments, each passed to the program as it is.</summary>
    public List<string> Arguments { get; set; } = new();

    /// <summary>
    /// The folder the program runs in. A relative path is resolved against the folder of the settings file; empty
    /// means that folder itself.
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>True when there is a program to run.</summary>
    [JsonIgnore]
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Command);

    /// <summary>Returns a copy with <paramref name="argument"/> added after the existing arguments.</summary>
    public ExternalCommand WithArgument(string argument) => new()
    {
        Command = Command,
        Arguments = [.. Arguments, argument],
        WorkingDirectory = WorkingDirectory,
    };

    /// <summary>Resolves <see cref="WorkingDirectory"/> against <paramref name="baseDirectory"/>.</summary>
    public string ResolveWorkingDirectory(string baseDirectory)
        => Path.GetFullPath(string.IsNullOrWhiteSpace(WorkingDirectory)
            ? baseDirectory
            : Path.Combine(baseDirectory, WorkingDirectory));

    /// <summary>The command line as a person would type it, with arguments that contain spaces quoted.</summary>
    public string ToDisplayString() => string.Join(' ', new[] { Command }.Concat(Arguments).Select(Quote));

    /// <summary>
    /// Identifies what the command does in a given solution: the solution folder, the program, the arguments and the
    /// working directory. A change to any of them gives a different value, so an approval of the old command does not
    /// carry over to the new one.
    /// </summary>
    public string Fingerprint(string solutionPath)
    {
        var text = string.Join('\0', new[] { Path.GetFullPath(solutionPath), Command, WorkingDirectory }.Concat(Arguments));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>
    /// Splits a command line typed on one line into arguments: spaces separate them, and double quotes keep spaces
    /// inside one. The reverse of the arguments half of <see cref="ToDisplayString"/>.
    /// </summary>
    public static List<string> SplitArguments(string text)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var hasToken = false;
        foreach (var c in text)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                hasToken = true;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (hasToken) result.Add(current.ToString());
                current.Clear();
                hasToken = false;
            }
            else
            {
                current.Append(c);
                hasToken = true;
            }
        }
        if (hasToken) result.Add(current.ToString());
        return result;
    }

    /// <summary>Joins arguments into one line that <see cref="SplitArguments"/> reads back unchanged.</summary>
    public static string JoinArguments(IEnumerable<string> arguments) => string.Join(' ', arguments.Select(Quote));

    private static string Quote(string value)
        => value.Length == 0 || value.Any(char.IsWhiteSpace) ? $"\"{value}\"" : value;
}
