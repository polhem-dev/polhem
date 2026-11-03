namespace Polhem.Templates.UnitTests;

/// <summary>Locations in the repository the template tests read.</summary>
internal static class TemplatePaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string TemplateProject => Path.Combine(RepositoryRoot, "tools", "Polhem.Templates", "Polhem.Templates.csproj");

    public static string ContentRoot => Path.Combine(RepositoryRoot, "tools", "Polhem.Templates", "content");

    /// <summary>The folder of files every template carries, packed into each template's folder.</summary>
    public static string SharedContent => Path.Combine(ContentRoot, "_shared");

    /// <summary>The names of the templates: the folders of content/ that have a .template.config/template.json.</summary>
    public static IReadOnlyList<string> Templates =>
        Directory.GetDirectories(ContentRoot)
            .Where(d => File.Exists(Path.Combine(d, ".template.config", "template.json")))
            .Select(d => Path.GetFileName(d))
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The templates whose folder holds their own content. The others are derived: their template.json takes the
    /// content of another template's folder (Polhem.Templates.csproj describes them).
    /// </summary>
    public static IReadOnlyList<string> ContentTemplates =>
        Templates.Where(t => DerivedFrom(t) is null).ToList();

    /// <summary>The template whose content a derived template takes, or <see langword="null"/> for a content template.</summary>
    public static string? DerivedFrom(string template)
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(TemplateJson(template)));
        if (!document.RootElement.TryGetProperty("sources", out var sources)) return null;
        // A content template may list sources too (for modifiers), without a `source` of its own.
        var source = sources.EnumerateArray()
            .Select(s => s.TryGetProperty("source", out var path) ? path.GetString() : null)
            .SingleOrDefault(path => path is not null && path.StartsWith("../", StringComparison.Ordinal));
        return source?.Trim('.', '/');
    }

    public static string TemplateDirectory(string template) => Path.Combine(ContentRoot, template);

    public static string TemplateJson(string template) =>
        Path.Combine(TemplateDirectory(template), ".template.config", "template.json");

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Polhem.slnx"))) return dir.FullName;
        }
        throw new InvalidOperationException($"No Polhem.slnx above '{AppContext.BaseDirectory}'.");
    }
}
