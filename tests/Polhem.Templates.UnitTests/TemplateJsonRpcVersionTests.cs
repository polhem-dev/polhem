using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Polhem.Templates.UnitTests;

/// <summary>
/// A generated application, and one set up by following the documents, references the same <c>Polhem.JsonRpc</c>
/// version as the framework it uses. The framework pins it in <c>PolhemJsonRpc.props</c>; the templates and the
/// documents write it literally, because an application has no copy of that file.
/// </summary>
public class TemplateJsonRpcVersionTests
{
    private static readonly Regex s_addPackage = new(
        @"dotnet add package (?<package>Polhem\.JsonRpc\S*)(?<rest>[^\r\n]*)", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly string[] s_documents =
    [
        "README.md",
        "README.zh-TW.md",
        Path.Combine("docs", "en", "getting-started", "getting-started.md"),
        Path.Combine("docs", "zh-TW", "getting-started", "getting-started.md"),
    ];

    [Fact]
    [DisplayName("Every Polhem.JsonRpc reference in the templates uses the version PolhemJsonRpc.props pins")]
    public void TemplateReferences_MatchTheFrameworkPin()
    {
        var pinned = PinnedVersion();
        var references = Directory.EnumerateFiles(TemplatePaths.ContentRoot, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(file => XDocument.Load(file).Descendants("PackageReference")
                .Where(reference => ((string?)reference.Attribute("Include"))?.StartsWith("Polhem.JsonRpc", StringComparison.Ordinal) == true)
                .Select(reference => (
                    File: Path.GetRelativePath(TemplatePaths.ContentRoot, file),
                    Version: (string?)reference.Attribute("Version") ?? (string?)reference.Element("Version"))))
            .ToList();

        Assert.NotEmpty(references);
        Assert.All(references, reference => Assert.True(reference.Version == pinned,
            $"content/{reference.File} references Polhem.JsonRpc {reference.Version ?? "without a version"}, but PolhemJsonRpc.props pins {pinned}."));
    }

    [Fact]
    [DisplayName("Every dotnet add package of a Polhem.JsonRpc package in the getting-started documents names the pinned version")]
    public void DocumentInstallCommands_NameThePinnedVersion()
    {
        var expected = $"--version {PinnedVersion().Trim('[', ']')}";
        var commands = s_documents
            .SelectMany(document => s_addPackage.Matches(File.ReadAllText(Path.Combine(TemplatePaths.RepositoryRoot, document)))
                .Select(match => (Document: document, Command: match.Value, Options: match.Groups["rest"].Value.Trim())))
            .ToList();

        Assert.Equal(s_documents.Length, commands.Select(command => command.Document).Distinct().Count());
        Assert.All(commands, command => Assert.True(command.Options == expected,
            $"{command.Document}: '{command.Command}' should end with '{expected}', the version PolhemJsonRpc.props pins."));
    }

    private static string PinnedVersion()
    {
        var props = XDocument.Load(Path.Combine(TemplatePaths.RepositoryRoot, "PolhemJsonRpc.props"));
        var pinned = props.Descendants("PolhemJsonRpcVersion").Select(element => element.Value).SingleOrDefault();
        Assert.False(string.IsNullOrEmpty(pinned), "PolhemJsonRpc.props no longer declares PolhemJsonRpcVersion.");
        Assert.Matches(@"^\[[^,\]]+\]$", pinned);
        return pinned!;
    }
}
