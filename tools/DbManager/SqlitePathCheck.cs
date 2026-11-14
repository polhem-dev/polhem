using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Definition.Storage;
using Polhem.Hosting.Database;

namespace Polhem.DbManager;

/// <summary>
/// Finds the SQLite database items whose file path is relative.
/// </summary>
/// <remarks>
/// A relative SQLite path is resolved against the working directory of the process that opens it. For an application
/// that is the server's directory, which this tool cannot know, so it would read and write a different file while
/// reporting success. The tool refuses such items instead of guessing. A path under <c>|DataDirectory|</c> is resolved
/// the way the provider resolves it, against <see cref="DataDirectory.Current"/>, which the tool sets to the folder
/// that holds the definition folder, as the server does.
/// </remarks>
internal static class SqlitePathCheck
{
    /// <summary>One line per SQLite item with a relative path; empty when there is none.</summary>
    public static IReadOnlyList<string> FindRelativePaths(IServiceProvider services)
    {
        var settings = services.GetRequiredService<IDefineAccess>().GetDatabaseSettings();
        var connections = services.GetRequiredService<IDbConnectionManager>();
        var problems = new List<string>();
        foreach (var item in settings.Items ?? [])
        {
            DbConnectionInfo info;
            try
            {
                info = connections.GetConnectionInfo(item.Id);
            }
            catch (KeyNotFoundException)
            {
                continue; // The db command reports an item it cannot resolve.
            }
            catch (InvalidOperationException)
            {
                continue;
            }
            catch (ArgumentException)
            {
                continue; // A connection string or template the db command reports as that item's failure.
            }
            if (info.DatabaseType != DatabaseType.SQLite) continue;

            var path = RelativePath(info.ConnectionString, DataDirectory.Current);
            if (path is not null)
            {
                problems.Add($"  {item.Id}: the SQLite path '{path}' is relative. It is resolved against the working "
                    + "directory of the process that opens it, which for an application is its server's directory. "
                    + $"Use an absolute path, a path under {DataDirectory.Prefix} (the folder that holds the definition "
                    + "folder), or run the db commands from the server project (dotnet run -- db ...).");
            }
        }
        return problems;
    }

    /// <summary>The file path of a SQLite connection string when it is relative; otherwise <see langword="null"/>.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="dataDirectory">
    /// The folder <c>|DataDirectory|</c> stands for, or <see langword="null"/> when nothing has set it. Unset, the
    /// provider falls back to the build output of the process, so a path under the prefix counts as relative.
    /// </param>
    internal static string? RelativePath(string connectionString, string? dataDirectory)
    {
        SqliteConnectionStringBuilder builder;
        try
        {
            builder = new SqliteConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            return null; // The db command reports a connection string it cannot parse.
        }
        if (builder.Mode == SqliteOpenMode.Memory) return null;

        var path = builder.DataSource;
        if (path.StartsWith(DataDirectory.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(dataDirectory)) return path;
            // Microsoft.Data.Sqlite combines the folder with the rest of the path in the same way.
            path = Path.Combine(dataDirectory, path[DataDirectory.Prefix.Length..]);
        }
        if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile && !string.IsNullOrEmpty(uri.LocalPath)
                && path.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return null; // file:///absolute/path
            }
            path = path[5..];
            var query = path.IndexOf('?', StringComparison.Ordinal);
            if (query >= 0)
            {
                // file:name?mode=memory is an in-memory database, whatever the name looks like.
                if (path[query..].Contains("mode=memory", StringComparison.OrdinalIgnoreCase)) return null;
                path = path[..query];
            }
            // file:/C:/x.db is a Windows drive path written as a URI path.
            if (path.Length > 3 && path[0] == '/' && char.IsAsciiLetter(path[1]) && path[2] == ':') path = path[1..];
        }
        if (path.Length == 0 || string.Equals(path, ":memory:", StringComparison.Ordinal)) return null;
        // IsPathFullyQualified rather than IsPathRooted: on Windows `C:x.db` and `\x.db` are rooted yet still depend on
        // the current drive or directory.
        return Path.IsPathFullyQualified(path) ? null : path;
    }
}
