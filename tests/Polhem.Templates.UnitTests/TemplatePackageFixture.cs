using System.Diagnostics;
using System.Reflection;

namespace Polhem.Templates.UnitTests;

/// <summary>
/// Packs the template and the framework packages a generated application references into a local feed, and installs
/// the template into a private template hive.
/// </summary>
/// <remarks>
/// The packages get a prerelease version of their own, and the generated applications restore into a package folder
/// of their own (with the user's global folder as a read-only fallback for everything else), so the user's global
/// package folder never holds a test build under a version that could be mistaken for a release.
/// </remarks>
public sealed class TemplatePackageFixture : IDisposable
{
    // The framework packages a generated server and desktop client reference, directly or transitively.
    private static readonly string[] s_frameworkProjects =
    [
        "Polhem.Core", "Polhem.Expressions", "Polhem.Definition", "Polhem.ObjectCaching", "Polhem.Db",
        "Polhem.Api.Contracts", "Polhem.Business", "Polhem.Api.Core", "Polhem.Api.Server",
        "Polhem.Repository.Abstractions", "Polhem.Repository", "Polhem.Hosting",
        "Polhem.Api.Client", "Polhem.UI.Core", "Polhem.UI.ApiBridge", "Polhem.UI.Avalonia", "Polhem.UI.Avalonia.Shell",
    ];

    private static readonly TimeSpan s_timeout = TimeSpan.FromMinutes(10);

    public TemplatePackageFixture()
    {
        Root = Directory.CreateTempSubdirectory("polhem-templates-").FullName;
        Feed = Path.Combine(Root, "feed");
        Hive = Path.Combine(Root, "hive");
        Packages = Path.Combine(Root, "packages");
        var version = typeof(TemplatePackageFixture).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        PackageVersion = $"{version}-templatetest";

        var solution = Path.Combine(Root, "framework.slnx");
        File.WriteAllLines(solution,
        [
            "<Solution>",
            .. s_frameworkProjects.Select(p =>
                $"  <Project Path=\"{Path.Combine(TemplatePaths.RepositoryRoot, "src", p, p + ".csproj")}\" />"),
            "</Solution>",
        ]);
        // Build first: outside CI, src/Directory.Build.props turns on GeneratePackageOnBuild, under which `dotnet pack`
        // does not build, so a project not yet built in Release would have nothing to pack.
        Run(Root, "build", solution, "--configuration", "Release");
        Run(Root, "pack", solution, "--configuration", "Release", "--no-build", "--output", Feed,
            $"-p:PackageVersion={PackageVersion}");
        Run(Root, "pack", TemplatePaths.TemplateProject, "--configuration", "Release", "--output", Feed,
            $"-p:PackageVersion={PackageVersion}");
        Run(Root, "new", "install", Path.Combine(Feed, $"Polhem.Templates.{PackageVersion}.nupkg"),
            "--debug:custom-hive", Hive);

        File.WriteAllText(Path.Combine(Root, "nuget.config"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="local" value="{Feed}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """);
    }

    /// <summary>The working folder; generated applications go under it and find its nuget.config.</summary>
    public string Root { get; }

    public string Feed { get; }

    public string Hive { get; }

    public string Packages { get; }

    public string PackageVersion { get; }

    /// <summary>Runs <c>dotnet</c> and fails the test with its output when it does not succeed.</summary>
    public string Run(string workingDirectory, params string[] args)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args) startInfo.ArgumentList.Add(arg);
        // The test host runs under MSBuild's environment; a child build must resolve its own SDK.
        foreach (var name in startInfo.Environment.Keys
                     .Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            startInfo.Environment.Remove(name);
        }
        startInfo.Environment["NUGET_PACKAGES"] = Packages;
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(s_timeout))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"dotnet {string.Join(' ', args)} did not finish within {s_timeout}.");
        }
        var text = output.Result + error.Result;
        Assert.True(process.ExitCode == 0, $"dotnet {string.Join(' ', args)} exited with {process.ExitCode}:\n{text}");
        return text;
    }

    /// <summary>The user's global package folder, which generated applications fall back to for third-party packages.</summary>
    public static string GlobalPackages =>
        Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A file still held by a build server; the folder is under the temporary directory.
        }
        catch (UnauthorizedAccessException)
        {
            // As above.
        }
    }
}
