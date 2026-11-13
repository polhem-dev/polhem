using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Polhem.Templates.UnitTests;

/// <summary>
/// A generated application references the same <c>Polhem.JsonRpc</c> version as the framework it uses. The framework
/// pins it in <c>PolhemJsonRpc.props</c>; the templates write it literally, because a generated application has no
/// copy of that file.
/// </summary>
public class TemplateJsonRpcVersionTests
{
    private static readonly Regex s_propsVersion = new(
        "<PolhemJsonRpcVersion>(?<version>[^<]+)</PolhemJsonRpcVersion>", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly Regex s_reference = new(
        "<PackageReference\\s+Include=\"Polhem\\.JsonRpc[^\"]*\"\\s+Version=\"(?<version>[^\"]+)\"",
        RegexOptions.None, TimeSpan.FromSeconds(1));

    [Fact]
    [DisplayName("Every Polhem.JsonRpc reference in the templates uses the version PolhemJsonRpc.props pins")]
    public void TemplateReferences_MatchTheFrameworkPin()
    {
        var props = File.ReadAllText(Path.Combine(TemplatePaths.RepositoryRoot, "PolhemJsonRpc.props"));
        var pinned = s_propsVersion.Match(props).Groups["version"].Value;
        var references = Directory.EnumerateFiles(TemplatePaths.ContentRoot, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(file => s_reference.Matches(File.ReadAllText(file))
                .Select(match => (File: Path.GetRelativePath(TemplatePaths.ContentRoot, file), Version: match.Groups["version"].Value)))
            .ToList();

        Assert.False(string.IsNullOrEmpty(pinned), "PolhemJsonRpc.props no longer declares PolhemJsonRpcVersion.");
        Assert.NotEmpty(references);
        Assert.All(references, reference => Assert.True(reference.Version == pinned,
            $"content/{reference.File} references Polhem.JsonRpc {reference.Version}, but PolhemJsonRpc.props pins {pinned}."));
    }
}
