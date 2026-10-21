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
    [DisplayName("The database choices of polhem-app are exactly the DatabaseType members")]
    public void DatabaseChoices_MatchDatabaseType()
    {
        var choices = Symbol("database").GetProperty("choices").EnumerateArray()
            .Select(c => c.GetProperty("choice").GetString()!)
            .Order(StringComparer.OrdinalIgnoreCase);
        var members = Enum.GetNames<DatabaseType>().Order(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(members, choices, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    [DisplayName("The default of polhemVersion is the placeholder the package build replaces")]
    public void PolhemVersionDefault_IsThePackPlaceholder()
    {
        // Polhem.Templates.csproj (StampTemplateVersion) replaces this exact value when it packs the template.
        Assert.Equal("0.0.0-unpacked", Symbol("polhemVersion").GetProperty("defaultValue").GetString());
    }
}
