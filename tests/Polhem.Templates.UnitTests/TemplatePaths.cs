namespace Polhem.Templates.UnitTests;

/// <summary>Locations in the repository the template tests read.</summary>
internal static class TemplatePaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string TemplateProject => Path.Combine(RepositoryRoot, "tools", "Polhem.Templates", "Polhem.Templates.csproj");

    public static string TemplateJson => Path.Combine(
        RepositoryRoot, "tools", "Polhem.Templates", "content", "polhem-app", ".template.config", "template.json");

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Polhem.slnx"))) return dir.FullName;
        }
        throw new InvalidOperationException($"No Polhem.slnx above '{AppContext.BaseDirectory}'.");
    }
}
