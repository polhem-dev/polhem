using System.Text;
using Polhem.Definition;
using Polhem.Db;
using Polhem.Definition.Database;
using Polhem.Core.Data;
using Polhem.Definition.Logging;
using Polhem.Repository.Abstractions.AuditLog;

namespace Polhem.Repository.AuditLog
{
    /// <summary>
    /// Default <see cref="IAuditLogWriteRepository"/>. Writes parameterised INSERTs into the
    /// conventional log database via <see cref="IDbAccessFactory"/>. Table and column names match
    /// the read side (<see cref="AuditLogRepository"/>) so reads line up with writes across every
    /// provider. A batch is written in one transaction, so a failure leaves none of its entries behind.
    /// </summary>
    internal sealed class AuditLogWriteRepository : RepositoryBase, IAuditLogWriteRepository
    {
        /// <summary>
        /// Initializes a new <see cref="AuditLogWriteRepository"/>.
        /// </summary>
        /// <param name="ctx">The shared repository context.</param>
        /// <param name="accessToken">The current request's access token.</param>
        /// <param name="progId">Unused on the framework axis; accepted for signature uniformity.</param>
        public AuditLogWriteRepository(IRepositoryContext ctx, Guid accessToken, string progId)
            : base(ctx, accessToken, progId, DbScope.Log)
        {
        }

        /// <inheritdoc/>
        public void WriteBatch(IReadOnlyList<AuditEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);
            if (entries.Count == 0) { return; }

            // Log tables live in the conventional 'log' database (a fixed databaseId, like
            // 'common'); the physical mapping is resolved by DatabaseSettings, not configured here.
            // One connection and one commit for the whole batch. Executing each INSERT on its own
            // made every entry a separate pool checkout and log flush, which capped the drain at one
            // commit latency per entry. It also made a failure half-written: the entries before the
            // failing one were committed and then spilled to the fallback file a second time.
            var batch = new DbBatchSpec { UseTransaction = true };
            var schemas = new Dictionary<string, TableSchema?>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                if (!schemas.TryGetValue(entry.TableName, out var schema))
                {
                    schema = FindSchema(entry.TableName);
                    schemas[entry.TableName] = schema;
                }
                batch.Commands.Add(BuildInsert(entry, schema));
            }

            var dbAccess = Context.DbAccessFactory.Create(DbCategoryIds.Log);
            dbAccess.ExecuteBatch(batch);
        }

        // The log table's definition gives the column widths. Without one, nothing is truncated and an over-long value
        // fails its batch as before, which the sink then retries entry by entry.
        private TableSchema? FindSchema(string tableName)
        {
            try
            {
                return Context.DefineAccess.GetTableSchema(DbCategoryIds.Log, tableName);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }

        /// <summary>
        /// Builds the INSERT for one entry without truncating its values.
        /// </summary>
        /// <param name="entry">The entry to persist.</param>
        public static DbCommandSpec BuildInsert(AuditEntry entry) => BuildInsert(entry, schema: null);

        /// <summary>
        /// Builds a parameterised INSERT for one entry, cutting each string value to the width its column declares
        /// in <paramref name="schema"/>. Column order is the entry's own stable order; values bind positionally
        /// through <c>{@Parameters}</c>, with null mapped to <see cref="DBNull.Value"/> so nullable columns are
        /// written as SQL NULL.
        /// </summary>
        /// <param name="entry">The entry to persist.</param>
        /// <param name="schema">The log table's definition, or <c>null</c> to write the values as they are.</param>
        /// <remarks>
        /// <para>
        /// Public because it is the canonical write-side column contract: tests that seed audit rows for read-side
        /// queries build them through this method rather than restating the column list, so the two sides cannot
        /// drift apart.
        /// </para>
        /// <para>
        /// IMPORTANT: several values come straight from an unauthenticated caller (a sign-in's user id, a JSON-RPC
        /// method name). Written as they are, one over-long value made SQL Server, PostgreSQL, MySQL and Oracle refuse
        /// the whole batch, and every other entry in it was lost with it, so a caller could erase other users' audit
        /// records. A NUL character did the same on PostgreSQL, and a lone UTF-16 surrogate made Npgsql fail to encode
        /// the statement. All are made acceptable here, whatever the schema. SQLite enforces none of these, so they do
        /// not show there.
        /// </para>
        /// </remarks>
        public static DbCommandSpec BuildInsert(AuditEntry entry, TableSchema? schema)
        {
            ArgumentNullException.ThrowIfNull(entry);

            var columns = entry.GetColumns();

            var sb = new StringBuilder();
            sb.Append("INSERT INTO ").Append(entry.TableName).Append(" (");
            for (int i = 0; i < columns.Count; i++)
            {
                if (i > 0) { sb.Append(", "); }
                sb.Append(columns[i].Name);
            }
            sb.Append(") VALUES (").Append(CommandTextVariable.Parameters).Append(')');

            var values = new object[columns.Count];
            for (int i = 0; i < columns.Count; i++)
            {
                values[i] = Fit(columns[i].Value, schema?.Fields?.GetOrDefault(columns[i].Name)) ?? DBNull.Value;
            }

            return new DbCommandSpec(DbCommandKind.NonQuery, sb.ToString(), values);
        }

        // Makes a string value one every supported database accepts: a NUL character, which PostgreSQL refuses in text
        // columns, and a lone surrogate, which PostgreSQL cannot encode as UTF-8, become U+FFFD, and the value is cut
        // to the column's declared width in characters without splitting a surrogate pair.
        private static object? Fit(object? value, DbField? field)
        {
            if (value is not string text) { return value; }
            text = ReplaceUnencodable(text);
            if (field is null || field.DbType != FieldDbType.String || field.Length <= 0 || text.Length <= field.Length)
            {
                return text;
            }
            int length = field.Length;
            if (char.IsHighSurrogate(text[length - 1])) { length--; }
            return text[..length];
        }

        private static string ReplaceUnencodable(string text)
        {
            int i = 0;
            while (i < text.Length && !IsUnencodableAt(text, i)) { i += char.IsHighSurrogate(text[i]) ? 2 : 1; }
            if (i >= text.Length) { return text; }

            var sb = new StringBuilder(text.Length);
            sb.Append(text, 0, i);
            while (i < text.Length)
            {
                if (IsUnencodableAt(text, i)) { sb.Append('\uFFFD'); i++; }
                else if (char.IsHighSurrogate(text[i])) { sb.Append(text, i, 2); i += 2; }
                else { sb.Append(text[i]); i++; }
            }
            return sb.ToString();
        }

        // True for a NUL, a low surrogate, or a high surrogate not followed by a low one. A well-formed pair is read
        // from its high half, so its low half is never tested on its own.
        private static bool IsUnencodableAt(string text, int index)
        {
            char c = text[index];
            if (c == '\0' || char.IsLowSurrogate(c)) { return true; }
            return char.IsHighSurrogate(c) && (index + 1 >= text.Length || !char.IsLowSurrogate(text[index + 1]));
        }
    }
}
