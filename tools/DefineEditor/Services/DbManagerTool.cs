using System.Reflection;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// The database manager, the dotnet tool <c>Polhem.DbManager</c> (<c>polhem-db</c>), which DefineEditor runs for the
/// database work it cannot do itself: it carries no database driver.
/// </summary>
/// <remarks>
/// It runs through <c>dotnet tool exec</c> at exactly this editor's version, so nothing has to be installed first and
/// the two always come from the same release. The first run downloads the package into the NuGet global packages
/// folder; <see cref="IsCached"/> tells whether that has happened. The tool needs the .NET SDK, which an editor
/// installed as a dotnet tool has by definition.
/// </remarks>
public static class DbManagerTool
{
    /// <summary>The NuGet package id of the tool.</summary>
    public const string PackageId = "Polhem.DbManager";

    /// <summary>
    /// An environment variable naming an extra package source, such as a folder of locally packed nupkgs, for running
    /// an editor built from source before the tool of its version is published.
    /// </summary>
    public const string SourceVariable = "POLHEM_DBMANAGER_SOURCE";

    /// <summary>The tool version to run: this editor's own version, without build metadata.</summary>
    public static string Version { get; } = GetVersion();

    /// <summary>The package source the tool comes from: nuget.org.</summary>
    public const string NuGetSource = "https://api.nuget.org/v3/index.json";

    /// <summary>
    /// The command that runs <paramref name="arguments"/> (a <c>polhem-db</c> command and its options) against the
    /// solution at <paramref name="definePath"/>.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: the command runs in <paramref name="workDirectory"/>, a folder of the editor's own, never in the
    /// solution. <c>dotnet</c> reads <c>nuget.config</c> and <c>global.json</c> from the working directory and its
    /// parents, so a folder opened from somewhere else could otherwise name the package source the tool is
    /// downloaded from, or the SDK that runs it. The package sources come from <paramref name="configFile"/> alone.
    /// </remarks>
    /// <param name="definePath">The DefinePath of the solution.</param>
    /// <param name="arguments">The command and its options, for example <c>upgrade --table ft_order</c>.</param>
    /// <param name="workDirectory">The folder the command runs in; see <see cref="PrepareWorkDirectory"/>.</param>
    /// <param name="configFile">The NuGet configuration the tool is restored with; see <see cref="PrepareWorkDirectory"/>.</param>
    public static ExternalCommand Command(
        string definePath, IEnumerable<string> arguments, string workDirectory, string configFile)
    {
        var command = new ExternalCommand
        {
            Command = "dotnet",
            // --yes answers the download question dotnet asks itself; the editor asks the user before the first run.
            Arguments = ["tool", "exec", $"{PackageId}@{Version}", "--yes", "--configfile", configFile, "--"],
            WorkingDirectory = workDirectory,
        };
        command.Arguments.AddRange(arguments);
        command.Arguments.AddRange(["--define", definePath]);
        return command;
    }

    /// <summary>
    /// Creates the editor's own folder for running the tool, with a NuGet configuration that lists nuget.org and,
    /// when given, <paramref name="extraSource"/> (see <see cref="SourceVariable"/>), and returns both paths.
    /// </summary>
    /// <param name="root">The folder to create it in, normally the user's temporary folder.</param>
    /// <param name="extraSource">An extra package source, or <see langword="null"/>.</param>
    public static (string WorkDirectory, string ConfigFile) PrepareWorkDirectory(string root, string? extraSource)
    {
        var workDirectory = Path.Combine(root, "Polhem.DefineEditor");
        Directory.CreateDirectory(workDirectory);
        var configFile = Path.Combine(workDirectory, "nuget.config");
        var sources = new System.Xml.Linq.XElement("packageSources",
            new System.Xml.Linq.XElement("clear"),
            Source("nuget.org", NuGetSource));
        if (!string.IsNullOrWhiteSpace(extraSource)) sources.Add(Source("polhem-dbmanager-extra", extraSource));
        new System.Xml.Linq.XDocument(new System.Xml.Linq.XElement("configuration", sources)).Save(configFile);
        return (workDirectory, configFile);
    }

    private static System.Xml.Linq.XElement Source(string key, string value)
        => new("add", new System.Xml.Linq.XAttribute("key", key), new System.Xml.Linq.XAttribute("value", value));

    /// <summary>The <c>upgrade</c> arguments: a <c>--table</c> for each of <paramref name="tableNames"/>, and <c>--dry-run</c> for a preview.</summary>
    public static IReadOnlyList<string> UpgradeArguments(IEnumerable<string> tableNames, bool dryRun)
    {
        var arguments = new List<string> { "upgrade" };
        foreach (var tableName in tableNames) arguments.AddRange(["--table", tableName]);
        if (dryRun) arguments.Add("--dry-run");
        return arguments;
    }

    /// <summary>
    /// True when the tool of <see cref="Version"/> is completely in the NuGet global packages folder, so running it
    /// downloads nothing. NuGet writes the package hash file after the package is extracted, so a download that was
    /// cut off does not count.
    /// </summary>
    /// <param name="packagesFolder">The global packages folder; <see langword="null"/> resolves it as NuGet does.</param>
    public static bool IsCached(string? packagesFolder = null)
    {
        var id = PackageId.ToLowerInvariant();
        var version = Version.ToLowerInvariant();
        return File.Exists(Path.Combine(packagesFolder ?? GlobalPackagesFolder(), id, version, $"{id}.{version}.nupkg.sha512"));
    }

    /// <summary>The NuGet global packages folder: <c>NUGET_PACKAGES</c>, or <c>.nuget/packages</c> in the user's home.</summary>
    public static string GlobalPackagesFolder()
    {
        var configured = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages")
            : configured;
    }

    private static string GetVersion()
    {
        var informational = typeof(DbManagerTool).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? informational : informational[..plus];
    }
}
