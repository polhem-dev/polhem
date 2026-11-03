using System.Globalization;
using System.Text.Json;

namespace Polhem.Templates.UnitTests;

/// <summary>Reads what a template generated, for comparing two generated applications.</summary>
internal static class GeneratedOutput
{
    /// <summary>
    /// The relative path and content of each generated file, with the site's port, which each run of a template picks
    /// anew, replaced by a placeholder.
    /// </summary>
    /// <param name="output">The folder the template generated.</param>
    /// <param name="siteProject">The folder of the project whose launch settings carry the port, relative to the output.</param>
    public static Dictionary<string, string> Files(string output, string siteProject)
    {
        using var settings = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(output, siteProject, "Properties", "launchSettings.json")));
        var url = settings.RootElement.GetProperty("profiles").EnumerateObject().First().Value
            .GetProperty("applicationUrl").GetString()!;
        var port = new Uri(url.Split(';')[0]).Port.ToString(CultureInfo.InvariantCulture);
        return Directory.GetFiles(output, "*", SearchOption.AllDirectories)
            .ToDictionary(
                f => Path.GetRelativePath(output, f),
                f => File.ReadAllText(f).Replace(port, "<port>", StringComparison.Ordinal),
                StringComparer.Ordinal);
    }
}
