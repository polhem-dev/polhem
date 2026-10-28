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
