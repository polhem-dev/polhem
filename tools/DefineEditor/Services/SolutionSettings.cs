using System.Text.Json;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// The settings DefineEditor keeps with a solution, in <see cref="FileName"/> inside the DefinePath. Unlike
/// <see cref="UserSettings"/> they travel with the project, so a project template can fill them in.
/// </summary>
/// <remarks>
/// The framework loads definition files by name and never reads this file; the explorer does not list it either.
/// </remarks>
public sealed class SolutionSettings
{
    /// <summary>The file name, in the DefinePath folder.</summary>
    public const string FileName = "DefineEditor.json";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>
    /// The command that builds or upgrades the solution's database tables, typically the host's
    /// <c>db upgrade</c> command (<c>dotnet run --project &lt;Server&gt; -- db upgrade</c>).
    /// </summary>
    public ExternalCommand DatabaseUpgrade { get; set; } = new();

    /// <summary>The path of the settings file of the solution at <paramref name="definePath"/>.</summary>
    public static string GetPath(string definePath) => Path.Combine(definePath, FileName);

    /// <summary>
    /// Loads the settings of the solution at <paramref name="definePath"/>, or defaults when the file does not exist.
    /// </summary>
    /// <exception cref="InvalidDataException">The file exists but is not valid settings JSON.</exception>
    public static SolutionSettings Load(string definePath)
    {
        var path = GetPath(definePath);
        if (!File.Exists(path)) return new SolutionSettings();
        try
        {
            var settings = JsonSerializer.Deserialize<SolutionSettings>(File.ReadAllText(path), s_jsonOptions)
                ?? new SolutionSettings();
            settings.DatabaseUpgrade ??= new ExternalCommand();
            settings.DatabaseUpgrade.Arguments ??= new List<string>();
            return settings;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{FileName}: {ex.Message}", ex);
        }
    }

    /// <summary>Writes the settings to the solution at <paramref name="definePath"/>.</summary>
    public void Save(string definePath)
        => File.WriteAllText(GetPath(definePath), JsonSerializer.Serialize(this, s_jsonOptions) + "\n");
}
