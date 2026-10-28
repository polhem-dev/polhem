using System.ComponentModel;

namespace Polhem.Templates.UnitTests;

public class TemplateContentTests
{
    [Fact]
    [DisplayName("The shared content folder is not a template")]
    public void SharedContent_IsNotATemplate()
    {
        Assert.True(Directory.Exists(TemplatePaths.SharedContent));
        Assert.False(Directory.Exists(Path.Combine(TemplatePaths.SharedContent, ".template.config")));
        Assert.DoesNotContain(Path.GetFileName(TemplatePaths.SharedContent), TemplatePaths.Templates);
        Assert.NotEmpty(TemplatePaths.Templates);
    }

    // The package build packs every shared file into each template's folder, so a template's own copy at the same
    // path would put two files at one package path, and the copies would drift apart.
    [Fact]
    [DisplayName("No template keeps its own copy of a shared file")]
    public void Templates_DoNotRepeatSharedFiles()
    {
        var shared = SharedFiles();

        Assert.NotEmpty(shared);
        Assert.All(TemplatePaths.Templates, template => Assert.All(shared,
            file => Assert.False(File.Exists(Path.Combine(TemplatePaths.TemplateDirectory(template), file)),
                $"content/{template}/{file} repeats content/_shared/{file}.")));
    }

    // Each template's host project registers the chosen database's driver in its own Program.cs and csproj, where a
    // reader of the generated application expects it, so those blocks cannot live in content/_shared. This keeps the
    // copies the same.
    [Fact]
    [DisplayName("The database blocks of every template's host project are the same")]
    public void HostProjects_HaveTheSameDatabaseBlocks()
    {
        var hosts = TemplatePaths.Templates.Select(HostProject).ToList();

        Assert.True(hosts.Count > 1);
        foreach (var file in new[] { "Program.cs", "*.csproj" })
        {
            var blocks = hosts.Select(h => DatabaseBlocks(Directory.GetFiles(h, file).Single())).ToList();
            Assert.All(blocks, b => Assert.NotEmpty(b));
            Assert.All(blocks.Skip(1), b => Assert.Equal(blocks[0], b));
        }
    }

    // The project that registers the database driver: the one whose Program.cs calls DbProviderRegistry.Register.
    private static string HostProject(string template) =>
        Directory.GetDirectories(Path.Combine(TemplatePaths.TemplateDirectory(template), "src"))
            .Single(p => File.Exists(Path.Combine(p, "Program.cs"))
                && File.ReadAllText(Path.Combine(p, "Program.cs")).Contains("DbProviderRegistry.Register", StringComparison.Ordinal));

    // Each run of lines from a `#if (database == ...)` condition, in C# or in an XML comment, to its `#endif`.
    private static List<string> DatabaseBlocks(string path)
    {
        var blocks = new List<string>();
        List<string>? current = null;
        foreach (var line in File.ReadLines(path))
        {
            if (current is null && line.Contains("#if (database ==", StringComparison.Ordinal)) current = [];
            if (current is null) continue;
            current.Add(line);
            if (line.Contains("#endif", StringComparison.Ordinal))
            {
                blocks.Add(string.Join('\n', current));
                current = null;
            }
        }
        return blocks;
    }

    /// <summary>The paths of the shared files, relative to the shared content folder.</summary>
    public static IReadOnlyList<string> SharedFiles() =>
        Directory.GetFiles(TemplatePaths.SharedContent, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(TemplatePaths.SharedContent, f))
            .ToList();
}
