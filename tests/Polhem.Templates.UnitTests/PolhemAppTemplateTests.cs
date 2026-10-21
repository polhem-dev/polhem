using System.ComponentModel;
using System.Text.Json;
using Polhem.Core.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Settings;

namespace Polhem.Templates.UnitTests;

public class PolhemAppTemplateTests : IClassFixture<TemplatePackageFixture>
{
    private readonly TemplatePackageFixture _fx;

    public PolhemAppTemplateTests(TemplatePackageFixture fx) => _fx = fx;

    private string Generate(string database, params string[] options)
    {
        var output = Path.Combine(_fx.Root, $"{database}-{Guid.NewGuid():N}");
        _fx.Run(_fx.Root, ["new", "polhem-app", "--name", "Acme.Erp", "--output", output, "--database", database,
            "--no-restore", "--debug:custom-hive", _fx.Hive, .. options]);
        return output;
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSql")]
    [InlineData("MySql")]
    [InlineData("Oracle")]
    [InlineData("Sqlite")]
    [DisplayName("polhem-app generates an application that builds for each database")]
    public void Generate_EachDatabase_Builds(string database)
    {
        var output = Generate(database);

        // The generated server sets TreatWarningsAsErrors, so a warning, a NuGet audit warning included, fails the build.
        _fx.Run(output, "build", "Acme.Erp.slnx",
            $"-p:RestoreAdditionalProjectFallbackFolders={TemplatePackageFixture.GlobalPackages}");
    }

    [Theory]
    [InlineData("SqlServer", DatabaseType.SQLServer)]
    [InlineData("PostgreSql", DatabaseType.PostgreSQL)]
    [InlineData("MySql", DatabaseType.MySQL)]
    [InlineData("Oracle", DatabaseType.Oracle)]
    [InlineData("Sqlite", DatabaseType.SQLite)]
    [DisplayName("The generated DatabaseSettings has a common, company and log item on a server of the chosen engine")]
    public void Generate_DatabaseSettings_HasTheThreeCategories(string database, DatabaseType expected)
    {
        var output = Generate(database);

        var settings = XmlCodec.DeserializeFromFile<DatabaseSettings>(Path.Combine(output, "Define", "DatabaseSettings.xml"))!;

        var server = Assert.Single(settings.Servers!);
        Assert.Equal(expected, server.DatabaseType);
        Assert.Equal(["common", "company", "log"], settings.Items!.Select(i => i.CategoryId).Order(StringComparer.Ordinal));
        Assert.All(settings.Items!, item => Assert.Equal(server.Id, item.ServerId));
        // `db create` accepts only ASCII letters, digits and underscore in a database name.
        Assert.All(settings.Items!, item => Assert.Matches("^[a-z0-9_]+$", database == "Oracle" ? item.UserId : item.DbName));
    }

    [Fact]
    [DisplayName("The generated application pins the tools and packages to the template's own version")]
    public void Generate_ToolsAndPackages_UseThePackageVersion()
    {
        var output = Generate("SqlServer");

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, ".config", "dotnet-tools.json")));
        foreach (var tool in manifest.RootElement.GetProperty("tools").EnumerateObject())
        {
            Assert.Equal(_fx.PackageVersion, tool.Value.GetProperty("version").GetString());
        }
        var project = File.ReadAllText(Path.Combine(output, "src", "Acme.Erp.Server", "Acme.Erp.Server.csproj"));
        Assert.Contains($"Include=\"Polhem.Hosting\" Version=\"{_fx.PackageVersion}\"", project, StringComparison.Ordinal);
    }

    [Fact]
    [DisplayName("The company and database password options reach the VS Code task and DatabaseSettings")]
    public void Generate_CompanyAndPassword_AreWrittenWhereTheyAreUsed()
    {
        var output = Generate("SqlServer", "--company", "ACME", "--db-password", "Secret_1");

        var tasks = File.ReadAllText(Path.Combine(output, ".vscode", "tasks.json"));
        Assert.Contains("\"ACME\"", tasks, StringComparison.Ordinal);
        var settings = XmlCodec.DeserializeFromFile<DatabaseSettings>(Path.Combine(output, "Define", "DatabaseSettings.xml"))!;
        Assert.Equal("Secret_1", Assert.Single(settings.Servers!).Password);
    }
}
