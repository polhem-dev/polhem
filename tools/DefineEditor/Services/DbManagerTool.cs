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
    /// IMPORTANT: <c>dotnet</c> reads <c>NuGet.Config</c> and <c>global.json</c> from the working directory and every
    /// folder above it, and <c>--source</c> only replaces the package sources. So the command runs in
    /// <paramref name="workDirectory"/>, a folder of the user's own (<see cref="WorkDirectory"/>), never in the opened
    /// solution or a shared temporary folder, where someone else's files could choose the SDK or the packages folder.
    /// The sources are passed on the command line, and the packages folder is pinned with <c>NUGET_PACKAGES</c>, the
    /// one <see cref="IsCached"/> looks in.
    /// </remarks>
    /// <param name="definePath">The DefinePath of the solution.</param>
    /// <param name="arguments">The command and its options, for example <c>upgrade --table ft_order</c>.</param>
    /// <param name="workDirectory">The folder the command runs in.</param>
    /// <param name="extraSource">An extra package source, or <see langword="null"/>; see <see cref="SourceVariable"/>.</param>
    public static ExternalCommand Command(
        string definePath, IEnumerable<string> arguments, string workDirectory, string? extraSource = null)
    {
        var command = new ExternalCommand
        {
            Command = "dotnet",
            // --yes answers the download question dotnet asks itself; the editor asks the user before the first run.
            Arguments = ["tool", "exec", $"{PackageId}@{Version}", "--yes", "--source", NuGetSource],
            WorkingDirectory = workDirectory,
        };
        if (!string.IsNullOrWhiteSpace(extraSource)) command.Arguments.AddRange(["--source", FullSource(extraSource)]);
        command.Arguments.Add("--");
        command.Arguments.AddRange(arguments);
        command.Arguments.AddRange(["--define", definePath]);
        command.Environment["NUGET_PACKAGES"] = GlobalPackagesFolder();
        return command;
    }

    /// <summary>
    /// The folder the tool runs in, created when missing: <c>Polhem.DefineEditor/dbmanager</c> in the user's local
    /// application data folder, which only the user and the system can write to or above.
    /// </summary>
    public static string WorkDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);
        // An empty result (no profile, no HOME) would give a relative folder, resolved against wherever the editor runs.
        if (!Path.IsPathFullyQualified(root))
            throw new IOException("The local application data folder cannot be found, so the database manager has no folder to run in.");
        var folder = Path.Combine(root, "Polhem.DefineEditor", "dbmanager");
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>
    /// A local source as a full path, since the tool runs in another folder; a URL as it is.
    /// </summary>
    public static string FullSource(string source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)) return Path.GetFullPath(source);
        return uri.IsFile ? uri.LocalPath : source;
    }

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
            : Path.GetFullPath(configured);
    }

    private static string GetVersion()
    {
        var informational = typeof(DbManagerTool).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? informational : informational[..plus];
    }
}
