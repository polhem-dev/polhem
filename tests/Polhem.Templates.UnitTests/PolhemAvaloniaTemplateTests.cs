using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Polhem.Core.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Settings;

namespace Polhem.Templates.UnitTests;

public class PolhemAvaloniaTemplateTests : IClassFixture<TemplatePackageFixture>
{
    private readonly TemplatePackageFixture _fx;

    public PolhemAvaloniaTemplateTests(TemplatePackageFixture fx) => _fx = fx;

    private string Generate(string database, params string[] options)
    {
        var output = Path.Combine(_fx.Root, $"{database}-{Guid.NewGuid():N}");
        _fx.Run(_fx.Root, ["new", "polhem-avalonia", "--name", "Acme.Erp", "--output", output, "--database", database,
            "--no-restore", "--debug:custom-hive", _fx.Hive, .. options]);
        return output;
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSql")]
    [InlineData("MySql")]
    [InlineData("Oracle")]
    [InlineData("Sqlite")]
    [DisplayName("polhem-avalonia generates an application that builds for each database")]
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

    [Fact]
    [DisplayName("The clients start with the server's endpoint and the first company, and the desktop bundle id comes from the name")]
    public void Generate_Desktop_PointsAtTheServerAndCompany()
    {
        var output = Generate("SqlServer", "--company", "ACME");

        using var launch = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(output, "src", "Acme.Erp.Server", "Properties", "launchSettings.json")));
        var serverUrl = launch.RootElement.GetProperty("profiles").GetProperty("http").GetProperty("applicationUrl").GetString();
        var app = File.ReadAllText(Path.Combine(output, "src", "Acme.Erp.UI", "App.cs"));
        Assert.Contains($"DefaultEndpoint = \"{serverUrl}/api\"", app, StringComparison.Ordinal);
        Assert.Contains("DefaultCompanyId = \"ACME\"", app, StringComparison.Ordinal);
        var project = File.ReadAllText(Path.Combine(output, "src", "Acme.Erp.Desktop", "Acme.Erp.Desktop.csproj"));
        Assert.Matches(new Regex("<PolhemBundleIdentifier>com\\.example\\.acme\\.erp</PolhemBundleIdentifier>",
            RegexOptions.None, TimeSpan.FromSeconds(1)), project);
    }

    [Fact]
    [DisplayName("Without client options polhem-avalonia generates only the server, the desktop client and the UI they share")]
    public void Generate_NoClientOptions_HasOnlyTheDesktopClient()
    {
        var output = Generate("SqlServer");

        Assert.Equal(["Acme.Erp.Desktop", "Acme.Erp.Server", "Acme.Erp.UI"],
            Directory.GetDirectories(Path.Combine(output, "src")).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.DoesNotContain("Cors", File.ReadAllText(Path.Combine(output, "src", "Acme.Erp.Server", "Program.cs")),
            StringComparison.Ordinal);
        Assert.DoesNotContain("Browser", File.ReadAllText(Path.Combine(output, ".vscode", "launch.json")),
            StringComparison.Ordinal);
        Assert.DoesNotContain("browser", File.ReadAllText(Path.Combine(output, ".vscode", "tasks.json")),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [DisplayName("The Browser launch configuration serves the browser client and opens its own URL, and stopping it ends the server")]
    public void Generate_Browser_LaunchOpensTheBrowserClientUrl()
    {
        var output = Generate("SqlServer", "--browser");

        using var settings = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(output, "src", "Acme.Erp.Browser", "Properties", "launchSettings.json")));
        var browserUrl = settings.RootElement.GetProperty("profiles").GetProperty("Acme.Erp.Browser")
            .GetProperty("applicationUrl").GetString();
        var launch = ParseJsonWithComments(Path.Combine(output, ".vscode", "launch.json"));
        var browser = launch.RootElement.GetProperty("configurations").EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == "Browser");
        Assert.Equal(browserUrl, browser.GetProperty("url").GetString());
        Assert.Equal("polhem-serve-browser", browser.GetProperty("preLaunchTask").GetString());
        Assert.Equal("polhem-stop-browser", browser.GetProperty("postDebugTask").GetString());

        var tasks = ParseJsonWithComments(Path.Combine(output, ".vscode", "tasks.json"));
        var labels = tasks.RootElement.GetProperty("tasks").EnumerateArray().Select(t => t.GetProperty("label").GetString()).ToList();
        Assert.Contains("polhem-serve-browser", labels);
        Assert.Contains("polhem-stop-browser", labels);
        Assert.Equal("polhem-serve-browser",
            tasks.RootElement.GetProperty("inputs")[0].GetProperty("args").GetString());
    }

    // VS Code reads its settings files as JSON with comments; the template's conditional markers are comments too.
    private static JsonDocument ParseJsonWithComments(string path)
        => JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });

    [Fact]
    [DisplayName("The browser, iOS and Android options add their clients to the solution, with the endpoint each one reaches the server at")]
    public void Generate_AllClientOptions_AddTheClients()
    {
        var output = Generate("SqlServer", "--browser", "--ios", "--android");

        var solution = File.ReadAllText(Path.Combine(output, "Acme.Erp.slnx"));
        foreach (var head in new[] { "Android", "Browser", "Desktop", "iOS", "Server", "UI" })
        {
            Assert.Contains($"src/Acme.Erp.{head}/Acme.Erp.{head}.csproj", solution, StringComparison.Ordinal);
        }
        var serverUrl = ServerUrl(output);
        var port = new Uri(serverUrl).Port;
        // The Android emulator reaches the machine that runs it at 10.0.2.2; its own localhost is the emulator.
        Assert.Contains($"DefaultEndpoint = \"http://10.0.2.2:{port}/api\"",
            File.ReadAllText(Path.Combine(output, "src", "Acme.Erp.Android", "AndroidApp.cs")), StringComparison.Ordinal);
        Assert.Contains("<ApplicationId>com.example.acme.erp</ApplicationId>",
            File.ReadAllText(Path.Combine(output, "src", "Acme.Erp.Android", "Acme.Erp.Android.csproj")), StringComparison.Ordinal);
        Assert.Contains("<string>com.example.acme.erp</string>",
            File.ReadAllText(Path.Combine(output, "src", "Acme.Erp.iOS", "Info.plist")), StringComparison.Ordinal);
        Assert.Contains("UseCors", File.ReadAllText(Path.Combine(output, "src", "Acme.Erp.Server", "Program.cs")),
            StringComparison.Ordinal);
        Assert.Contains("DefaultLanguage = \"en-US\"",
            File.ReadAllText(Path.Combine(output, "src", "Acme.Erp.UI", "App.cs")), StringComparison.Ordinal);
        var tasks = File.ReadAllText(Path.Combine(output, ".vscode", "tasks.json"));
        Assert.Contains("Polhem: Run iOS simulator", tasks, StringComparison.Ordinal);
        Assert.Contains("Polhem: Run Android emulator", tasks, StringComparison.Ordinal);
        foreach (var task in new[] { "polhem-run-server", "polhem-run-browser", "polhem-run-ios", "polhem-run-android",
            "polhem-run-desktop" })
        {
            Assert.Contains($"\"{task}\"", tasks, StringComparison.Ordinal);
        }
    }

    [Fact]
    [DisplayName("An Android package name derived from a name with hyphens has no hyphens")]
    public void Generate_HyphenatedName_GivesAValidAndroidPackageName()
    {
        var output = Path.Combine(_fx.Root, $"hyphen-{Guid.NewGuid():N}");
        _fx.Run(_fx.Root, ["new", "polhem-avalonia", "--name", "Acme-Sales", "--output", output, "--android",
            "--no-restore", "--debug:custom-hive", _fx.Hive]);

        Assert.Contains("<ApplicationId>com.example.acme_sales</ApplicationId>",
            File.ReadAllText(Path.Combine(output, "src", "Acme-Sales.Android", "Acme-Sales.Android.csproj")),
            StringComparison.Ordinal);
    }

    [WorkloadFact("wasm-tools")]
    [DisplayName("The generated browser client builds")]
    public void Generate_Browser_Builds() => BuildHead("--browser", "Browser");

    [WorkloadFact("ios")]
    [DisplayName("The generated iOS client builds")]
    public void Generate_iOS_Builds() => BuildHead("--ios", "iOS");

    [WorkloadFact("android", "maui-android")]
    [DisplayName("The generated Android client builds")]
    public void Generate_Android_Builds() => BuildHead("--android", "Android");

    private void BuildHead(string option, string head)
    {
        var output = Generate("SqlServer", option);

        _fx.Run(output, "build", Path.Combine("src", $"Acme.Erp.{head}", $"Acme.Erp.{head}.csproj"),
            $"-p:RestoreAdditionalProjectFallbackFolders={TemplatePackageFixture.GlobalPackages}");
    }

    private static string ServerUrl(string output)
    {
        using var launch = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(output, "src", "Acme.Erp.Server", "Properties", "launchSettings.json")));
        return launch.RootElement.GetProperty("profiles").GetProperty("http").GetProperty("applicationUrl").GetString()!;
    }
}
