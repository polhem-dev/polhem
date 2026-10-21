using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Definition.Storage;

namespace Polhem.DbManager;

/// <summary>
/// Finds the SQLite database items whose file path is relative.
/// </summary>
/// <remarks>
/// A relative SQLite path is resolved against the working directory of the process that opens it. For an application
/// that is the server's directory, which this tool cannot know, so it would read and write a different file while
/// reporting success. The tool refuses such items instead of guessing.
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
            if (info.DatabaseType != DatabaseType.SQLite) continue;

            var path = RelativePath(info.ConnectionString);
            if (path is not null)
            {
                problems.Add($"  {item.Id}: the SQLite path '{path}' is relative. It is resolved against the working "
                    + "directory of the process that opens it, which for an application is its server's directory. "
                    + "Use an absolute path, or run the db commands from the server project (dotnet run -- db ...).");
            }
        }
        return problems;
    }

    private static string? RelativePath(string connectionString)
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
        if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            path = path[5..];
            var query = path.IndexOf('?', StringComparison.Ordinal);
            if (query >= 0) path = path[..query];
        }
        if (path.Length == 0 || string.Equals(path, ":memory:", StringComparison.Ordinal)) return null;
        return Path.IsPathRooted(path) ? null : path;
    }
}
