namespace Polhem.DefineEditor.Services;

/// <summary>
/// The file that receives the lines of <see cref="CallLogChatClient"/>: <c>ai-calls.log</c> beside the user settings
/// file. Once it passes <see cref="MaxBytes"/> it is renamed to <c>ai-calls.old.log</c>, which replaces the previous one.
/// </summary>
public static class CallLogFile
{
    /// <summary>The size at which the log starts over.</summary>
    public const long MaxBytes = 1024 * 1024;

    private static readonly Lock s_gate = new();

    /// <summary>The path of the log.</summary>
    public static string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetDirectoryName(UserSettings.GetConfigPath())!, "logs", "ai-calls.log");

    /// <summary>Appends <paramref name="line"/> to the log.</summary>
    /// <param name="line">The line, without its line break.</param>
    public static void Append(string line) => Append(Path, line);

    /// <summary>Appends <paramref name="line"/> to the log at <paramref name="path"/>.</summary>
    /// <param name="path">The log's path.</param>
    /// <param name="line">The line, without its line break.</param>
    public static void Append(string path, string line)
    {
        lock (s_gate)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                if (File.Exists(path) && new FileInfo(path).Length >= MaxBytes)
                    File.Move(path, System.IO.Path.ChangeExtension(path, ".old.log"), overwrite: true);
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A diagnostic log that cannot be written must not fail the model call it describes.
                System.Diagnostics.Debug.WriteLine($"Cannot write the AI call log: {ex.Message}");
            }
        }
    }
}
