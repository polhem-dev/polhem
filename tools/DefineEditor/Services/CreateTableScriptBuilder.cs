using Polhem.Db.Providers;
using Polhem.Db.Providers.MySql;
using Polhem.Db.Providers.Oracle;
using Polhem.Db.Providers.PostgreSql;
using Polhem.Db.Providers.Sqlite;
using Polhem.Db.Providers.SqlServer;
using Polhem.Definition.Database;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Produces the <c>CREATE TABLE</c> statement of a <see cref="TableSchema"/> for a database type, through the
/// framework's own dialect builders, so the editor shows what the framework would run.
/// </summary>
public static class CreateTableScriptBuilder
{
    /// <summary>A database type and the name the editor shows for it.</summary>
    public sealed record DatabaseOption(DatabaseType Type, string Name);

    /// <summary>The database types the preview offers, in the order the menu lists them.</summary>
    public static IReadOnlyList<DatabaseOption> Options { get; } =
    [
        new(DatabaseType.SQLServer, "SQL Server"),
        new(DatabaseType.PostgreSQL, "PostgreSQL"),
        new(DatabaseType.MySQL, "MySQL"),
        new(DatabaseType.Oracle, "Oracle"),
        new(DatabaseType.SQLite, "SQLite"),
    ];

    /// <summary>
    /// Returns the statement for <paramref name="schema"/> in the dialect of <paramref name="type"/>, with
    /// <c>\n</c> line endings. A schema the builder rejects, as one half-way through an edit can be, gives a single
    /// SQL comment with the reason instead of throwing.
    /// </summary>
    public static string Build(TableSchema schema, DatabaseType type)
    {
        ArgumentNullException.ThrowIfNull(schema);
        try
        {
            return DialectFor(type).CreateCreateTableCommandBuilder().GetCommandText(schema)
                .Replace("\r\n", "\n", StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException
                                       or FormatException or KeyNotFoundException)
        {
            return $"-- {ex.Message}";
        }
    }

    // NOTE: the factories are created directly rather than taken from `DbDialectRegistry`, which a host fills at
    // startup; the editor is not a host and registers nothing.
    private static IDialectFactory DialectFor(DatabaseType type) => type switch
    {
        DatabaseType.SQLServer => new SqlDialectFactory(),
        DatabaseType.PostgreSQL => new PgDialectFactory(),
        DatabaseType.MySQL => new MySqlDialectFactory(),
        DatabaseType.Oracle => new OracleDialectFactory(),
        DatabaseType.SQLite => new SqliteDialectFactory(),
        _ => throw new NotSupportedException($"Database type not supported: {type}"),
    };
}
