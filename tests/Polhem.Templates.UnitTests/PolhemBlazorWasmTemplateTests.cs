using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Polhem.Templates.UnitTests;

[Collection(TemplatePackageCollection.Name)]
public class PolhemBlazorWasmTemplateTests
{
    private static readonly JsonDocumentOptions s_jsonc = new() { CommentHandling = JsonCommentHandling.Skip };

    private readonly TemplatePackageFixture _fx;

    public PolhemBlazorWasmTemplateTests(TemplatePackageFixture fx) => _fx = fx;

    private string Generate(string database, params string[] options) => GenerateFrom("polhem-blazor-wasm", database, options);

    private string GenerateFrom(string template, string database, params string[] options)
    {
        var output = Path.Combine(_fx.Root, $"wasm-{database}-{Guid.NewGuid():N}");
        _fx.Run(_fx.Root, ["new", template, "--name", "Acme.Erp", "--output", output, "--database", database,
            "--no-restore", "--debug:custom-hive", _fx.Hive, .. options]);
        return output;
    }

    private void Build(string output, string target) =>
        _fx.Run(output, "build", target, $"-p:RestoreAdditionalProjectFallbackFolders={TemplatePackageFixture.GlobalPackages}");

    // The database blocks of the server are the ones TemplateContentTests keeps equal across templates, and the other
    // templates build them for every database, so one database is enough here.
    [Theory]
    [InlineData("mud")]
    [InlineData("fluent")]
    [DisplayName("polhem-blazor-wasm generates an application that builds with each UI library")]
    public void Generate_EachUi_Builds(string ui)
    {
        var output = Generate("Sqlite", "--ui", ui);

        // Both projects set TreatWarningsAsErrors, so a warning, a NuGet audit warning included, fails the build.
        Build(output, "Acme.Erp.slnx");
    }

    // CI installs no workload; a WebAssembly publish without wasm-tools must still succeed.
    [Theory]
    [InlineData("mud")]
    [InlineData("fluent")]
    [DisplayName("The generated server publishes with the browser client in it")]
    public void Generate_EachUi_PublishesTheClientWithTheServer(string ui)
    {
        var output = Generate("Sqlite", "--ui", ui);
        var publish = Path.Combine(output, "publish");

        _fx.Run(output, "publish", Path.Combine("src", "Acme.Erp.Server", "Acme.Erp.Server.csproj"), "--configuration", "Release",
            "--output", publish, $"-p:RestoreAdditionalProjectFallbackFolders={TemplatePackageFixture.GlobalPackages}");

        Assert.True(File.Exists(Path.Combine(publish, "wwwroot", "index.html")));
        Assert.True(File.Exists(Path.Combine(publish, "wwwroot", "_framework", "blazor.webassembly.js")));
    }

    [Fact]
    [DisplayName("polhem-blazor-wasm generates a server and a browser client, the definitions and the workbook folder")]
    public void Generate_HasServerAndClient()
    {
        var output = Generate("SqlServer");

        Assert.Equal(["Acme.Erp.Client", "Acme.Erp.Server"],
            Directory.GetDirectories(Path.Combine(output, "src")).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.True(File.Exists(Path.Combine(output, "Define", "SystemSettings.xml")));
        Assert.True(File.Exists(Path.Combine(output, "Excel", "README.md")));
    }

    [Fact]
    [DisplayName("The generated projects pin the tools and packages to the template's own version")]
    public void Generate_ToolsAndPackages_UseThePackageVersion()
    {
        var output = Generate("SqlServer");

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, ".config", "dotnet-tools.json")));
        foreach (var tool in manifest.RootElement.GetProperty("tools").EnumerateObject())
        {
            Assert.Equal(_fx.PackageVersion, tool.Value.GetProperty("version").GetString());
        }
        var server = File.ReadAllText(Path.Combine(output, "src", "Acme.Erp.Server", "Acme.Erp.Server.csproj"));
        Assert.Contains($"Include=\"Polhem.Hosting\" Version=\"{_fx.PackageVersion}\"", server, StringComparison.Ordinal);
        var client = File.ReadAllText(Path.Combine(output, "src", "Acme.Erp.Client", "Acme.Erp.Client.csproj"));
        Assert.Contains($"Include=\"Polhem.Web.Blazor.Wasm\" Version=\"{_fx.PackageVersion}\"", client, StringComparison.Ordinal);
        Assert.Contains($"Include=\"Polhem.Web.Blazor.Mud\" Version=\"{_fx.PackageVersion}\"", client, StringComparison.Ordinal);
    }

    // The UI library is chosen by conditional blocks in several files; a block left out of one of them would leave the
    // other library's package, namespace or stylesheet behind.
    [Theory]
    [InlineData("mud", "Mud", "Fluent")]
    [InlineData("fluent", "Fluent", "Mud")]
    [DisplayName("The generated browser client references only the chosen UI library")]
    public void Generate_EachUi_ReferencesOnlyItsLibrary(string ui, string library, string otherLibrary)
    {
        var output = Generate("SqlServer", "--ui", ui);

        var client = Path.Combine(output, "src", "Acme.Erp.Client");
        Assert.Contains($"Include=\"Polhem.Web.Blazor.{library}\"",
            File.ReadAllText(Path.Combine(client, "Acme.Erp.Client.csproj")), StringComparison.Ordinal);
        Assert.All(Directory.GetFiles(client, "*", SearchOption.AllDirectories),
            file => Assert.DoesNotContain(otherLibrary, File.ReadAllText(file), StringComparison.Ordinal));
    }

    [Fact]
    [DisplayName("The company and default language options reach the sign-in screen and the VS Code task")]
    public void Generate_CompanyAndLanguage_AreWrittenWhereTheyAreUsed()
    {
        var output = Generate("SqlServer", "--company", "ACME", "--default-language", "zh-TW");

        var program = File.ReadAllText(Path.Combine(output, "src", "Acme.Erp.Client", "Program.cs"));
        Assert.Contains("shell.DefaultCompanyId = \"ACME\"", program, StringComparison.Ordinal);
        Assert.Contains("shell.DefaultCulture = \"zh-TW\"", program, StringComparison.Ordinal);
        Assert.Contains("using Acme.Erp.Client;", program, StringComparison.Ordinal);
        var tasks = File.ReadAllText(Path.Combine(output, ".vscode", "tasks.json"));
        Assert.Contains("\"ACME\"", tasks, StringComparison.Ordinal);
        Assert.Contains("src/Acme.Erp.Server/Acme.Erp.Server.csproj", tasks, StringComparison.Ordinal);
    }

    // The C# Dev Kit's "dotnet" launch type debugs a server that references a WebAssembly client in its WebAssembly
    // mode, which waited for a browser and did not start the server when tried in VS Code. The configuration runs the
    // built server with the debugger instead.
    [Fact]
    [DisplayName("The F5 launch configuration builds and runs the server, which serves the browser client")]
    public void Generate_Launch_BuildsAndRunsTheServer()
    {
        var output = Generate("SqlServer");

        using var launch = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, ".vscode", "launch.json")), s_jsonc);
        var configuration = Assert.Single(launch.RootElement.GetProperty("configurations").EnumerateArray());
        Assert.Equal("coreclr", configuration.GetProperty("type").GetString());
        Assert.Equal("${workspaceFolder}/src/Acme.Erp.Server/bin/Debug/net10.0/Acme.Erp.Server.dll",
            configuration.GetProperty("program").GetString());
        var buildTask = configuration.GetProperty("preLaunchTask").GetString();

        using var tasks = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, ".vscode", "tasks.json")), s_jsonc);
        var task = Assert.Single(tasks.RootElement.GetProperty("tasks").EnumerateArray(),
            t => t.GetProperty("label").GetString() == buildTask);
        Assert.Equal(["build", "${workspaceFolder}/src/Acme.Erp.Server/Acme.Erp.Server.csproj"],
            task.GetProperty("args").EnumerateArray().Select(a => a.GetString()));

        // The server listens where its launch profile says, so `dotnet run` and F5 open the same address.
        using var settings = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(output, "src", "Acme.Erp.Server", "Properties", "launchSettings.json")));
        var profileUrl = settings.RootElement.GetProperty("profiles").EnumerateObject().Single().Value
            .GetProperty("applicationUrl").GetString();
        Assert.Equal(profileUrl, configuration.GetProperty("env").GetProperty("ASPNETCORE_URLS").GetString());
    }

    [Fact]
    [DisplayName("polhem-blazor-wasm-fluent generates what polhem-blazor-wasm generates with the Fluent UI library")]
    public void GenerateFluentTemplate_EqualsTheFluentChoice()
    {
        var chosen = Generate("SqlServer", "--ui", "fluent");
        var derived = GenerateFrom("polhem-blazor-wasm-fluent", "SqlServer");

        var server = Path.Combine("src", "Acme.Erp.Server");
        var chosenFiles = GeneratedOutput.Files(chosen, server);
        var derivedFiles = GeneratedOutput.Files(derived, server);
        Assert.NotEmpty(chosenFiles);
        Assert.Equal(chosenFiles.Keys.Order(StringComparer.Ordinal), derivedFiles.Keys.Order(StringComparer.Ordinal));
        Assert.All(chosenFiles, file => Assert.Equal(file.Value, derivedFiles[file.Key]));
    }

    [Theory]
    [InlineData("mud", "_content/Polhem.Web.Blazor.Mud/polhem-mud.css")]
    [InlineData("fluent", "_content/Polhem.Web.Blazor.Fluent/polhem-fluent.css")]
    [DisplayName("The generated server starts on a new SQLite database and serves the browser client and the API from one origin")]
    public async Task Generate_EachUi_ServesTheClientAndTheApi(string ui, string stylesheet)
    {
        var output = Generate("Sqlite", "--ui", ui);
        var project = Path.Combine("src", "Acme.Erp.Server", "Acme.Erp.Server.csproj");
        Build(output, project);
        _fx.Run(output, "run", "--project", project, "--no-build", "--", "db", "create");
        _fx.Run(output, "run", "--project", project, "--no-build", "--", "db", "upgrade");

        using var site = await RunningSite.StartAsync(_fx, output, project);

        var page = await site.Client.GetStringAsync(new Uri("/", UriKind.Relative));
        Assert.Contains(stylesheet, page, StringComparison.Ordinal);
        await AssertOkAsync(site, "/" + stylesheet);
        await AssertOkAsync(site, "/_framework/blazor.webassembly.js");
        // A form's address is the client's route, so a reload there gets the page, not a 404.
        Assert.Contains(stylesheet, await site.Client.GetStringAsync(new Uri("/form/Department", UriKind.Relative)),
            StringComparison.Ordinal);

        // The API answers with a JSON-RPC response, not with the page the fallback serves.
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api", UriKind.Relative))
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"NoSuch.Method","params":{}}""",
                Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Api-Key", "development");
        using var response = await site.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"jsonrpc\"", body, StringComparison.Ordinal);
    }

    private static async Task AssertOkAsync(RunningSite site, string path)
    {
        using var response = await site.Client.GetAsync(new Uri(path, UriKind.Relative));
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {path} returned {(int)response.StatusCode}:\n{site.Output}");
    }
}
