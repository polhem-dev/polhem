using System.ComponentModel;
using System.Text.Json;
using Polhem.Definition.Database;

namespace Polhem.Templates.UnitTests;

public class TemplateOptionsTests
{
    private static JsonElement Symbol(string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(TemplatePaths.TemplateJson));
        return document.RootElement.GetProperty("symbols").GetProperty(name).Clone();
    }

    [Fact]
    [DisplayName("The database choices of polhem-avalonia are exactly the DatabaseType members")]
    public void DatabaseChoices_MatchDatabaseType()
    {
        var choices = Symbol("database").GetProperty("choices").EnumerateArray()
            .Select(c => c.GetProperty("choice").GetString()!)
            .Order(StringComparer.OrdinalIgnoreCase);
        var members = Enum.GetNames<DatabaseType>().Order(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(members, choices, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    [DisplayName("No option of polhem-avalonia takes the name of an option dotnet new defines itself")]
    public void OptionNames_DoNotCollideWithDotnetNewOptions()
    {
        // `dotnet new` keeps these for itself, so a template symbol of the same name is only reachable as
        // `--param:<name>`. A symbol called `language` once fell into exactly that.
        string[] reserved = ["name", "output", "dry-run", "force", "no-update-check", "project", "language", "type"];
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(Path.GetDirectoryName(TemplatePaths.TemplateJson)!, "dotnetcli.host.json")));
        var symbolInfo = document.RootElement.GetProperty("symbolInfo");
        using var template = JsonDocument.Parse(File.ReadAllText(TemplatePaths.TemplateJson));

        var optionNames = template.RootElement.GetProperty("symbols").EnumerateObject()
            .Where(s => s.Value.GetProperty("type").GetString() == "parameter")
            .Select(s => symbolInfo.TryGetProperty(s.Name, out var info) && info.TryGetProperty("longName", out var longName)
                ? longName.GetString()!
                : s.Name)
            .ToList();

        Assert.NotEmpty(optionNames);
        Assert.Empty(optionNames.Intersect(reserved, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    [DisplayName("The default of polhemVersion is the placeholder the package build replaces")]
    public void PolhemVersionDefault_IsThePackPlaceholder()
    {
        // Polhem.Templates.csproj (StampTemplateVersion) replaces this exact value when it packs the template.
        Assert.Equal("0.0.0-unpacked", Symbol("polhemVersion").GetProperty("defaultValue").GetString());
    }
}
