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

    /// <summary>
    /// The command that runs <paramref name="arguments"/> (a <c>polhem-db</c> command and its options) against the
    /// solution at <paramref name="definePath"/>.
    /// </summary>
    /// <param name="definePath">The DefinePath of the solution.</param>
    /// <param name="arguments">The command and its options, for example <c>upgrade --table ft_order</c>.</param>
    /// <param name="extraSource">An extra package source, or <see langword="null"/>; see <see cref="SourceVariable"/>.</param>
    public static ExternalCommand Command(string definePath, IEnumerable<string> arguments, string? extraSource = null)
    {
        var command = new ExternalCommand
        {
            Command = "dotnet",
            // --yes answers the download question dotnet asks itself; the editor asks the user before the first run.
            Arguments = ["tool", "exec", $"{PackageId}@{Version}", "--yes"],
            WorkingDirectory = definePath,
        };
        if (!string.IsNullOrWhiteSpace(extraSource)) command.Arguments.AddRange(["--add-source", extraSource]);
        command.Arguments.Add("--");
        command.Arguments.AddRange(arguments);
        command.Arguments.AddRange(["--define", definePath]);
        return command;
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
    /// True when the tool of <see cref="Version"/> is already in the NuGet global packages folder, so running it
    /// downloads nothing.
    /// </summary>
    /// <param name="packagesFolder">The global packages folder; <see langword="null"/> resolves it as NuGet does.</param>
    public static bool IsCached(string? packagesFolder = null)
        => Directory.Exists(Path.Combine(packagesFolder ?? GlobalPackagesFolder(),
            PackageId.ToLowerInvariant(), Version.ToLowerInvariant()));

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
