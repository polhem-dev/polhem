using System.Globalization;
using Polhem.Core.Data;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Definition.Storage;

namespace Polhem.Northwind.Backend;

public static partial class NorthwindSchemaSeeder
{
    /// <summary>
    /// Reads the declared type of every column of a company table from its <c>TableSchema</c> definition.
    /// </summary>
    private static Dictionary<string, FieldDbType> GetFieldTypes(IDefineAccess defineAccess, string table)
    {
        var result = new Dictionary<string, FieldDbType>(StringComparer.OrdinalIgnoreCase);
        var schema = defineAccess.GetTableSchema(CompanyDatabaseId, table);
        if (schema?.Fields is null) return result;
        foreach (var field in schema.Fields)
            result[field.FieldName] = field.DbType;
        return result;
    }

    /// <summary>
    /// Converts one seed value, which the JSON file always carries as a string, to the CLR type of the column it
    /// lands in.
    /// </summary>
    /// <remarks>
    /// The seed has to be written the way the framework writes the same column, or the framework's own reads miss it.
    /// SQLite has no date type and compares dates as text: the framework stores a date as <c>1996-07-08 00:00:00</c>
    /// and filters with that form (<see cref="DbCommandSpec"/> turns a <see cref="DateOnly"/> filter value into
    /// midnight), so a seed date written as the string <c>1996-07-08</c> sorts before its own day and a list filtered
    /// from that day leaves it out. The other databases convert the string to the column's type on insert; the test
    /// fixture's seed (<c>NorthwindTestSeed</c>), which binds these same types, seeds each of them in CI.
    /// </remarks>
    /// <returns>
    /// False when the value is empty and the column is not text: the column is then left out of the insert and takes
    /// its default, because an empty string has no value of the column's type.
    /// </returns>
    private static bool TryConvertSeedValue(
        Dictionary<string, FieldDbType> fieldTypes, string column, string raw, out object value)
    {
        if (!fieldTypes.TryGetValue(column, out var dbType) || dbType is FieldDbType.String or FieldDbType.Text)
        {
            value = raw;
            return true;
        }

        if (raw.Length == 0)
        {
            value = raw;
            return false;
        }

        value = dbType switch
        {
            FieldDbType.Short => short.Parse(raw, CultureInfo.InvariantCulture),
            FieldDbType.Integer => int.Parse(raw, CultureInfo.InvariantCulture),
            FieldDbType.Long => long.Parse(raw, CultureInfo.InvariantCulture),
            FieldDbType.Decimal or FieldDbType.Currency => decimal.Parse(raw, CultureInfo.InvariantCulture),
            FieldDbType.Boolean => raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase),
            FieldDbType.Date => DateOnly.ParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                .ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            // Stored times are UTC (ADR-032 D1); a seed time has no zone of its own, so it is read as one.
            FieldDbType.DateTime => DateTime.Parse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal),
            FieldDbType.Guid => Guid.Parse(raw),
            _ => raw,
        };
        return true;
    }

    /// <summary>
    /// Rewrites the bare <c>yyyy-MM-dd</c> dates that an earlier seeder left in a SQLite <c>northwind.db</c> into the
    /// <c>yyyy-MM-dd 00:00:00</c> form the framework stores and filters with.
    /// </summary>
    /// <remarks>
    /// Before the seed values were typed, every seeded date column held the bare form, and a filter from a date left
    /// out the rows of that day (see <see cref="TryConvertSeedValue"/>). A row the framework saved already carries the
    /// time, so the length check leaves it alone. Only SQLite stores dates as text; the other databases
    /// converted the string to a date on insert and have nothing to rewrite.
    /// </remarks>
    private static void ConvergeSqliteDates(
        DbAccess dbAccess, IDbConnectionManager connectionManager, IDefineAccess defineAccess)
    {
        if (connectionManager.GetConnectionInfo(CompanyDatabaseId).DatabaseType != DatabaseType.SQLite) return;

        foreach (var seed in s_seeds)
        {
            var dateColumns = GetFieldTypes(defineAccess, seed.Table)
                .Where(pair => pair.Value is FieldDbType.Date or FieldDbType.DateTime)
                .Select(pair => pair.Key);
            foreach (var column in dateColumns)
            {
                var sql = $"UPDATE {seed.Table} SET {column} = {column} || ' 00:00:00' WHERE length({column}) = 10";
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql));
            }
        }
    }
}
