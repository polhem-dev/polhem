using System.ComponentModel;
using System.Text.Json;
using Polhem.Definition.Database;

namespace Polhem.Templates.UnitTests;

public class TemplateOptionsTests
{
    public static TheoryData<string> Templates => [.. TemplatePaths.Templates];

    private static JsonElement Symbol(string template, string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(TemplatePaths.TemplateJson(template)));
        return document.RootElement.GetProperty("symbols").GetProperty(name).Clone();
    }

    [Theory]
    [MemberData(nameof(Templates))]
    [DisplayName("The database choices of each template are exactly the DatabaseType members")]
    public void DatabaseChoices_MatchDatabaseType(string template)
    {
        var choices = Symbol(template, "database").GetProperty("choices").EnumerateArray()
            .Select(c => c.GetProperty("choice").GetString()!)
            .Order(StringComparer.OrdinalIgnoreCase);
        var members = Enum.GetNames<DatabaseType>().Order(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(members, choices, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(Templates))]
    [DisplayName("No option of a template takes the name of an option dotnet new defines itself")]
    public void OptionNames_DoNotCollideWithDotnetNewOptions(string template)
    {
        // `dotnet new` keeps these for itself, so a template symbol of the same name is only reachable as
        // `--param:<name>`. A symbol called `language` once fell into exactly that.
        string[] reserved = ["name", "output", "dry-run", "force", "no-update-check", "project", "language", "type"];
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(Path.GetDirectoryName(TemplatePaths.TemplateJson(template))!, "dotnetcli.host.json")));
        var symbolInfo = document.RootElement.GetProperty("symbolInfo");
        using var templateJson = JsonDocument.Parse(File.ReadAllText(TemplatePaths.TemplateJson(template)));

        var optionNames = templateJson.RootElement.GetProperty("symbols").EnumerateObject()
            .Where(s => s.Value.GetProperty("type").GetString() == "parameter")
            .Select(s => symbolInfo.TryGetProperty(s.Name, out var info) && info.TryGetProperty("longName", out var longName)
                ? longName.GetString()!
                : s.Name)
            .ToList();

        Assert.NotEmpty(optionNames);
        Assert.Empty(optionNames.Intersect(reserved, StringComparer.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(Templates))]
    [DisplayName("The default of polhemVersion in each template is the placeholder the package build replaces")]
    public void PolhemVersionDefault_IsThePackPlaceholder(string template)
    {
        // Polhem.Templates.csproj (PackTemplateFiles) replaces this exact value when it packs each template.
        Assert.Equal("0.0.0-unpacked", Symbol(template, "polhemVersion").GetProperty("defaultValue").GetString());
    }
}
