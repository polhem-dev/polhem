using System.ComponentModel;
using System.Text.Json;

namespace Polhem.Templates.UnitTests;

[Collection(TemplatePackageCollection.Name)]
public class PolhemBlazorServerTemplateTests
{
    private readonly TemplatePackageFixture _fx;

    public PolhemBlazorServerTemplateTests(TemplatePackageFixture fx) => _fx = fx;

    private string Generate(string database, params string[] options)
    {
        var output = Path.Combine(_fx.Root, $"blazor-{database}-{Guid.NewGuid():N}");
        _fx.Run(_fx.Root, ["new", "polhem-blazor-server", "--name", "Acme.Erp", "--output", output, "--database", database,
            "--no-restore", "--debug:custom-hive", _fx.Hive, .. options]);
        return output;
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSql")]
    [InlineData("MySql")]
    [InlineData("Oracle")]
    [InlineData("Sqlite")]
    [DisplayName("polhem-blazor-server generates an application that builds for each database")]
    public void Generate_EachDatabase_Builds(string database)
    {
        var output = Generate(database);

        // The generated site sets TreatWarningsAsErrors, so a warning, a NuGet audit warning included, fails the build.
        _fx.Run(output, "build", "Acme.Erp.slnx",
            $"-p:RestoreAdditionalProjectFallbackFolders={TemplatePackageFixture.GlobalPackages}");
    }

    [Fact]
    [DisplayName("polhem-blazor-server with the Fluent UI library generates an application that builds")]
    public void Generate_Fluent_Builds()
    {
        var output = Generate("Sqlite", "--ui", "fluent");

        _fx.Run(output, "build", "Acme.Erp.slnx",
            $"-p:RestoreAdditionalProjectFallbackFolders={TemplatePackageFixture.GlobalPackages}");
    }

    // The UI library is chosen by conditional blocks in several files; a block left out of one of them would leave the
    // other library's package, namespace or stylesheet behind.
    [Theory]
    [InlineData("mud", "Mud", "Fluent")]
    [InlineData("fluent", "Fluent", "Mud")]
    [DisplayName("The generated site references only the chosen UI library")]
    public void Generate_EachUi_ReferencesOnlyItsLibrary(string ui, string library, string otherLibrary)
    {
        var output = Generate("SqlServer", "--ui", ui);

        var site = Path.Combine(output, "src", "Acme.Erp.Web");
        Assert.Contains($"Include=\"Polhem.Web.Blazor.{library}\"", File.ReadAllText(Path.Combine(site, "Acme.Erp.Web.csproj")),
            StringComparison.Ordinal);
        Assert.All(Directory.GetFiles(site, "*", SearchOption.AllDirectories),
            file => Assert.DoesNotContain(otherLibrary, File.ReadAllText(file), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("mud", "_content/Polhem.Web.Blazor.Mud/polhem-mud.css")]
    [InlineData("fluent", "_content/Polhem.Web.Blazor.Fluent/polhem-fluent.css")]
    [DisplayName("The generated site starts on a new SQLite database and serves its page and the UI library's stylesheet")]
    public async Task Generate_EachUi_StartsAndServesThePage(string ui, string stylesheet)
    {
        var output = Generate("Sqlite", "--ui", ui);
        var project = Path.Combine("src", "Acme.Erp.Web", "Acme.Erp.Web.csproj");
        _fx.Run(output, "build", project,
            $"-p:RestoreAdditionalProjectFallbackFolders={TemplatePackageFixture.GlobalPackages}");
        _fx.Run(output, "run", "--project", project, "--no-build", "--", "db", "create");
        _fx.Run(output, "run", "--project", project, "--no-build", "--", "db", "upgrade");

        using var site = await RunningSite.StartAsync(_fx, output, project);

        var page = await site.Client.GetStringAsync(new Uri("/", UriKind.Relative));
        Assert.Contains(stylesheet, page, StringComparison.Ordinal);
        using var response = await site.Client.GetAsync(new Uri("/" + stylesheet, UriKind.Relative));
        Assert.True(response.IsSuccessStatusCode, $"GET /{stylesheet} returned {(int)response.StatusCode}:\n{site.Output}");
    }

    [Fact]
    [DisplayName("polhem-blazor-server generates one site project, the definitions and the workbook folder")]
    public void Generate_HasOneSiteProject()
    {
        var output = Generate("SqlServer");

        Assert.Equal(["Acme.Erp.Web"],
            Directory.GetDirectories(Path.Combine(output, "src")).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.True(File.Exists(Path.Combine(output, "Define", "SystemSettings.xml")));
        Assert.True(File.Exists(Path.Combine(output, "Excel", "README.md")));
    }

    [Fact]
    [DisplayName("The generated site pins the tools and packages to the template's own version")]
    public void Generate_ToolsAndPackages_UseThePackageVersion()
    {
        var output = Generate("SqlServer");

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, ".config", "dotnet-tools.json")));
        foreach (var tool in manifest.RootElement.GetProperty("tools").EnumerateObject())
        {
            Assert.Equal(_fx.PackageVersion, tool.Value.GetProperty("version").GetString());
        }
        var project = File.ReadAllText(Path.Combine(output, "src", "Acme.Erp.Web", "Acme.Erp.Web.csproj"));
        Assert.Contains($"Include=\"Polhem.Hosting\" Version=\"{_fx.PackageVersion}\"", project, StringComparison.Ordinal);
        Assert.Contains($"Include=\"Polhem.Web.Blazor.Server\" Version=\"{_fx.PackageVersion}\"", project, StringComparison.Ordinal);
        Assert.Contains($"Include=\"Polhem.Web.Blazor.Mud\" Version=\"{_fx.PackageVersion}\"", project, StringComparison.Ordinal);
    }

    [Fact]
    [DisplayName("The company and default language options reach the sign-in screen and the VS Code task")]
    public void Generate_CompanyAndLanguage_AreWrittenWhereTheyAreUsed()
    {
        var output = Generate("SqlServer", "--company", "ACME", "--default-language", "zh-TW");

        var program = File.ReadAllText(Path.Combine(output, "src", "Acme.Erp.Web", "Program.cs"));
        Assert.Contains("shell.DefaultCompanyId = \"ACME\"", program, StringComparison.Ordinal);
        Assert.Contains("shell.DefaultCulture = \"zh-TW\"", program, StringComparison.Ordinal);
        Assert.Contains("using Acme.Erp.Web.Components;", program, StringComparison.Ordinal);
        var tasks = File.ReadAllText(Path.Combine(output, ".vscode", "tasks.json"));
        Assert.Contains("\"ACME\"", tasks, StringComparison.Ordinal);
        Assert.Contains("src/Acme.Erp.Web/Acme.Erp.Web.csproj", tasks, StringComparison.Ordinal);
    }

    [Fact]
    [DisplayName("The F5 launch configuration starts the site project")]
    public void Generate_Launch_StartsTheSite()
    {
        var output = Generate("SqlServer");

        using var launch = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, ".vscode", "launch.json")));
        var configuration = Assert.Single(launch.RootElement.GetProperty("configurations").EnumerateArray());
        Assert.Equal("${workspaceFolder}/src/Acme.Erp.Web/Acme.Erp.Web.csproj",
            configuration.GetProperty("projectPath").GetString());
    }
}
