using System.Reflection;
using Polhem.Hosting.Database;

namespace Polhem.DbManager;

/// <summary>
/// Entry point of <c>polhem-db</c>: runs a <c>db</c> command of <see cref="DatabaseCommandLine"/> against the definition
/// folder named by <c>--define</c>, or the current directory.
/// </summary>
internal static class Program
{
    private const string Usage =
        "Usage: polhem-db <command> [options] [--define <DefinePath>]\n" +
        "\n" +
        "Commands:\n" +
        "  create                       Create the databases named in DatabaseSettings that do not exist.\n" +
        "  check                        Open a connection to each database in DatabaseSettings.\n" +
        "  upgrade [--dry-run] [--allow-narrowing] [--table <name>]...\n" +
        "                               Build or upgrade the tables registered in DbCategorySettings.\n" +
        "  seed-admin --company <id> [options]\n" +
        "                               Create the first administrator, a company and the access between them.\n" +
        "\n" +
        "Options:\n" +
        "  --define <DefinePath>        The definition folder (holds SystemSettings.xml). Default: the current directory.\n" +
        "  --help, -h                   Print this help text.\n" +
        "  --version                    Print the version.\n";

    public static int Main(string[] args) => Run(args, Console.Out, Environment.CurrentDirectory);

    /// <summary>Runs the command line with its output and working directory supplied by the caller.</summary>
    internal static int Run(string[] args, TextWriter output, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            output.Write(Usage);
            return args.Length == 0 ? DatabaseCommandLine.UsageError : DatabaseCommandLine.Success;
        }
        if (args[0] == "--version")
        {
            output.WriteLine(typeof(Program).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown");
            return DatabaseCommandLine.Success;
        }

        var rest = args.ToList();
        var definePath = currentDirectory;
        var index = rest.IndexOf("--define");
        if (index >= 0)
        {
            if (index + 1 >= rest.Count)
            {
                output.WriteLine("--define needs a value.");
                output.Write(Usage);
                return DatabaseCommandLine.UsageError;
            }
            definePath = Path.GetFullPath(rest[index + 1], currentDirectory);
            rest.RemoveRange(index, 2);
        }

        if (!File.Exists(Path.Combine(definePath, "SystemSettings.xml")))
        {
            output.WriteLine($"No SystemSettings.xml in {definePath}; pass the definition folder with --define.");
            return DatabaseCommandLine.UsageError;
        }

        try
        {
            using var host = DbManagerHost.Build(definePath);
            var relative = SqlitePathCheck.FindRelativePaths(host.Services);
            if (relative.Count > 0)
            {
                foreach (var line in relative) output.WriteLine(line);
                output.WriteLine("Failed: SQLite database paths must be absolute for polhem-db.");
                return DatabaseCommandLine.Failure;
            }
            return DatabaseCommandLine.TryRun(host.Services, ["db", .. rest]) ?? DatabaseCommandLine.UsageError;
        }
        catch (InvalidOperationException ex)
        {
            // A master key that cannot be found or read, or settings the framework rejects at startup.
            output.WriteLine($"Failed: {ex.Message}");
            return DatabaseCommandLine.Failure;
        }
        catch (IOException ex)
        {
            output.WriteLine($"Failed: {ex.Message}");
            return DatabaseCommandLine.Failure;
        }
        catch (UnauthorizedAccessException ex)
        {
            output.WriteLine($"Failed: {ex.Message}");
            return DatabaseCommandLine.Failure;
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            // An encrypted key in SystemSettings that the master key in use cannot decrypt.
            output.WriteLine($"Failed: the master key cannot decrypt the keys in SystemSettings ({ex.Message}). "
                + "Use the master key the server uses.");
            return DatabaseCommandLine.Failure;
        }
    }
}
