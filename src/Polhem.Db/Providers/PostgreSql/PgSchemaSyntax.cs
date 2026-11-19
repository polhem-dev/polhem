using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Definition.Database;

namespace Polhem.Db.Providers.PostgreSql
{
    /// <summary>
    /// Shared PostgreSQL identifier, literal, and column-definition primitives used by
    /// the CREATE and ALTER schema builders. Counterpart to
    /// <see cref="SqlServer.SqlSchemaSyntax"/> for the SQL Server provider.
    /// </summary>
    internal static class PgSchemaSyntax
    {
        /// <summary>
        /// Quotes a PostgreSQL identifier by escaping <c>"</c> as <c>""</c> and wrapping in double quotes.
        /// </summary>
        /// <param name="identifier">The identifier to quote.</param>
        public static string QuoteName(string identifier)
        {
            return $"\"{identifier.Replace("\"", "\"\"")}\"";
        }

        /// <summary>
        /// Escapes a string value for use inside a '...' literal by doubling single quotes.
        /// </summary>
        /// <param name="value">The string value to escape.</param>
        public static string EscapeSqlString(string value)
        {
            return value.Replace("'", "''");
        }

        /// <summary>
        /// Gets the PostgreSQL built-in default value expression for the specified field type
        /// (e.g. <c>CURRENT_TIMESTAMP</c> for DateTime, the empty GUID for Guid (the row id's generated default is
        /// <see cref="GetBuiltInDefault(DbField)"/>),
        /// <c>0</c> for numeric).
        /// </summary>
        /// <param name="dbType">The field data type.</param>
        public static string GetDefaultValueExpression(FieldDbType dbType)
        {
            switch (dbType)
            {
                case FieldDbType.String:
                case FieldDbType.Text:
                case FieldDbType.Time:
                    return string.Empty;
                case FieldDbType.Boolean:
                case FieldDbType.Short:
                case FieldDbType.Integer:
                case FieldDbType.Long:
                case FieldDbType.Decimal:
                case FieldDbType.Currency:
                    return "0";
                case FieldDbType.Date:
                case FieldDbType.DateTime:
                    // The framework stores every instant in UTC (ADR-032 D1), and this DEFAULT is
                    // the path that writes when a hand-written INSERT omits the column or an
                    // ALTER TABLE ADD COLUMN backfills existing rows. The server's local clock
                    // would put a non-UTC value into a column defined as UTC, so the UTC-returning
                    // form is used. (This is about the storage basis, not the user's zone — the
                    // database has no session and cannot know the user; user-facing defaults come
                    // from `FormRowDefaults`, see D12.)
                    return "(NOW() AT TIME ZONE 'UTC')";
                case FieldDbType.Guid:
                    return GuidColumnDefault.EmptyGuidLiteral;
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// The default that generates a new GUID, used only for the row id column.
        /// </summary>
        internal const string NewGuidExpression = "gen_random_uuid()";

        /// <summary>
        /// Gets the built-in default of a field: <see cref="NewGuidExpression"/> for the row id column, otherwise
        /// <see cref="GetDefaultValueExpression(FieldDbType)"/> for its type.
        /// </summary>
        /// <param name="field">The field definition.</param>
        public static string GetBuiltInDefault(DbField field)
        {
            return GuidColumnDefault.GeneratesNewGuid(field) ? NewGuidExpression : GetDefaultValueExpression(field.DbType);
        }

        /// <summary>
        /// Gets the default value expression for a field, honoring <see cref="DbField.AllowNull"/>
        /// (nullable fields have no default). Returns an empty string when no default should be applied.
        /// </summary>
        /// <param name="field">The field definition.</param>
        public static string GetDefaultExpression(DbField field)
        {
            if (field.AllowNull)
                return string.Empty;
            string originalDefaultValue = GetBuiltInDefault(field);
            switch (field.DbType)
            {
                case FieldDbType.String:
                case FieldDbType.Text:
                case FieldDbType.Time:
                    return StringUtilities.Format("'{0}'", StringUtilities.IsEmpty(field.DefaultValue) ? originalDefaultValue : EscapeSqlString(field.DefaultValue));
                case FieldDbType.AutoIncrement:
                    return string.Empty;
                case FieldDbType.Boolean:
                {
                    // PG `BOOLEAN` only accepts `TRUE`/`FALSE` literals, not the integer `1`/`0`
                    // that other dialects accept. The framework keeps `"1"`/`"0"` as the canonical
                    // user-facing form; PG translates here at the SQL emission boundary.
                    string raw = StringUtilities.IsEmpty(field.DefaultValue) ? originalDefaultValue : DefaultValueLiteral.Require(field);
                    return StringUtilities.IsEquals(raw, "1") ? "TRUE" : "FALSE";
                }
                default:
                    return StringUtilities.IsEmpty(field.DefaultValue) ? originalDefaultValue : DefaultValueLiteral.Require(field);
            }
        }

        /// <summary>
        /// Generates a full column definition fragment (name + type + nullability + optional inline DEFAULT).
        /// </summary>
        /// <param name="field">The field definition.</param>
        public static string GetColumnDefinition(DbField field)
        {
            string dbType = PgTypeMapping.GetPgType(field);
            string nullability = field.AllowNull ? "NULL" : "NOT NULL";
            string defaultExpression = GetDefaultExpression(field);
            string defaultClause = StringUtilities.IsNotEmpty(defaultExpression) ? $" DEFAULT {defaultExpression}" : string.Empty;
            return $"{QuoteName(field.FieldName)} {dbType} {nullability}{defaultClause}";
        }

        /// <summary>
        /// Generates the <c>COMMENT ON TABLE "table" IS 'description';</c> statement.
        /// </summary>
        /// <param name="tableName">The table name.</param>
        /// <param name="description">The description to store.</param>
        public static string GetTableCommentStatement(string tableName, string description)
        {
            return $"COMMENT ON TABLE {QuoteName(tableName)} IS '{EscapeSqlString(description)}';";
        }

        /// <summary>
        /// Generates the <c>COMMENT ON COLUMN "table"."column" IS 'description';</c> statement.
        /// </summary>
        /// <param name="tableName">The table name.</param>
        /// <param name="columnName">The column name.</param>
        /// <param name="description">The description to store.</param>
        public static string GetColumnCommentStatement(string tableName, string columnName, string description)
        {
            return $"COMMENT ON COLUMN {QuoteName(tableName)}.{QuoteName(columnName)} IS '{EscapeSqlString(description)}';";
        }
    }
}
