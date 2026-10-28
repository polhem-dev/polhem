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

    /// <summary>The paths of the shared files, relative to the shared content folder.</summary>
    public static IReadOnlyList<string> SharedFiles() =>
        Directory.GetFiles(TemplatePaths.SharedContent, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(TemplatePaths.SharedContent, f))
            .ToList();
}
