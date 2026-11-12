using System.Data;
using System.Data.Common;
using Polhem.Core.Data;

namespace Polhem.Db
{
    /// <summary>
    /// Asynchronous half of <see cref="DbAccess"/>. Split from the synchronous members purely for
    /// file size; behaviour and public surface are unchanged.
    /// </summary>
    public partial class DbAccess
    {
        #region Async methods

        /// <summary>
        /// Asynchronously executes a database command.
        /// </summary>
        /// <param name="command">The database command specification.</param>
        /// <param name="cancellationToken">A cancellation token for cancelling long-running commands.</param>
        public Task<DbCommandResult> ExecuteAsync(
            DbCommandSpec command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            return RunWithAnomalyDetectionAsync(command, async () =>
            {
                using var scope = await CreateScopeAsync(cancellationToken).ConfigureAwait(false);
                return await DispatchExecuteAsync(command, scope.Connection!, null, cancellationToken).ConfigureAwait(false);
            }, cancellationToken);
        }

        /// <summary>
        /// Asynchronously executes a database command using the specified <see cref="DbTransaction"/> on an external connection.
        /// Use this overload when you need explicit transaction control; the command is bound to the given transaction.
        /// </summary>
        /// <param name="command">The database command specification.</param>
        /// <param name="transaction">The required database transaction; the command is bound to this transaction.</param>
        /// <param name="cancellationToken">A cancellation token for cancelling long-running commands.</param>
        public Task<DbCommandResult> ExecuteAsync(
            DbCommandSpec command, DbTransaction transaction, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(transaction);

            var conn = transaction.Connection
                       ?? throw new InvalidOperationException("Transaction has no associated connection.");

            return RunWithAnomalyDetectionAsync(
                command, () => DispatchExecuteAsync(command, conn, transaction, cancellationToken), cancellationToken);
        }

        private Task<DbCommandResult> DispatchExecuteAsync(
            DbCommandSpec command, DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
            => command.Kind switch
            {
                DbCommandKind.NonQuery => ExecuteNonQueryCoreAsync(command, connection, transaction, cancellationToken),
                DbCommandKind.Scalar => ExecuteScalarCoreAsync(command, connection, transaction, cancellationToken),
                DbCommandKind.DataTable => ExecuteDataTableCoreAsync(command, connection, transaction, cancellationToken),
                _ => throw new NotSupportedException($"Unsupported DbCommandKind: {command.Kind}."),
            };

        /// <summary>
        /// Asynchronously executes a NonQuery database command and returns the number of rows affected.
        /// </summary>
        /// <param name="command">The database command specification.</param>
        /// <param name="connection">The database connection.</param>
        /// <param name="transaction">An optional transaction; pass null for no transaction.</param>
        /// <param name="cancellationToken">A cancellation token for cancelling long-running commands.</param>
        private async Task<DbCommandResult> ExecuteNonQueryCoreAsync(
            DbCommandSpec command, DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
        {
            using (var cmd = command.CreateCommand(DatabaseType, connection, _companyId))
            {
                cmd.CommandTimeout = ResolveTimeout(command.CommandTimeout);
                if (transaction != null) cmd.Transaction = transaction;
                var rows = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return DbCommandResult.ForRowsAffected(rows);
            }
        }

        /// <summary>
        /// Asynchronously executes a Scalar database command and returns the single result value.
        /// </summary>
        /// <param name="command">The database command specification.</param>
        /// <param name="connection">The database connection.</param>
        /// <param name="transaction">An optional transaction; pass null for no transaction.</param>
        /// <param name="cancellationToken">A cancellation token for cancelling long-running commands.</param>
        private async Task<DbCommandResult> ExecuteScalarCoreAsync(
            DbCommandSpec command, DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
        {
            using (var cmd = command.CreateCommand(DatabaseType, connection, _companyId))
            {
                cmd.CommandTimeout = ResolveTimeout(command.CommandTimeout);
                if (transaction != null) cmd.Transaction = transaction;
                var value = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                return DbCommandResult.ForScalar(value);
            }
        }

        /// <summary>
        /// Asynchronously executes a DataTable database command and returns the result set.
        /// </summary>
        /// <param name="command">The database command specification.</param>
        /// <param name="connection">The database connection.</param>
        /// <param name="transaction">An optional transaction; pass null for no transaction.</param>
        /// <param name="cancellationToken">A cancellation token for cancelling long-running commands.</param>
        private async Task<DbCommandResult> ExecuteDataTableCoreAsync(
            DbCommandSpec command, DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
        {
            using (var cmd = command.CreateCommand(DatabaseType, connection, _companyId))
            {
                cmd.CommandTimeout = ResolveTimeout(command.CommandTimeout);
                if (transaction != null) cmd.Transaction = transaction; // Set the transaction first

                using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    DataTable table;
                    if (command.ColumnTypes.Count > 0)
                    {
                        table = await LoadTypedTableAsync(reader, command, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        table = new DataTable("DataTable");
                        table.Load(reader);
                    }
                    table.LowercaseColumnNames();
                    table.NormalizeDateTimeMode();
                    ApplyDateColumns(command, table);
                    return DbCommandResult.ForTable(table);
                }
            }
        }

        /// <summary>
        /// Asynchronously executes a database command and maps each result row to an instance of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target mapping type.</typeparam>
        /// <param name="command">The database command specification.</param>
        /// <param name="cancellationToken">A cancellation token for cancelling long-running commands.</param>
        /// <returns>A <see cref="List{T}"/> containing the mapped results.</returns>
        public Task<List<T>> QueryAsync<T>(DbCommandSpec command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            return RunWithAnomalyDetectionAsync(AnomalySubject.Of(command), async () =>
            {
                using var scope = await CreateScopeAsync(cancellationToken).ConfigureAwait(false);
                using var cmd = command.CreateCommand(DatabaseType, scope.Connection!, _companyId);
                cmd.CommandTimeout = ResolveTimeout(command.CommandTimeout);
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                var list = new List<T>();
                var mapper = ILMapper<T>.CreateMapFunc(reader); // Build a mapper based on the current column set
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    list.Add(mapper(reader));
                }
                return list;
            }, list => new AnomalyCounts(0, list.Count), cancellationToken);
        }

        #endregion

        #region Async convenience overloads

        /// <summary>
        /// Asynchronously executes a SQL statement and returns the number of rows affected.
        /// </summary>
        /// <param name="commandText">The SQL statement to execute; use {0}, {1} positional placeholders.</param>
        /// <param name="values">Positional parameter values corresponding to {0}, {1}, ...</param>
        /// <returns>The number of rows affected.</returns>
        public async Task<int> ExecuteNonQueryAsync(string commandText, params object[] values)
        {
            var spec = new DbCommandSpec(DbCommandKind.NonQuery, commandText, values);
            return (await ExecuteAsync(spec).ConfigureAwait(false)).RowsAffected;
        }

        /// <summary>
        /// Asynchronously executes a SQL statement and returns a single scalar value.
        /// </summary>
        /// <param name="commandText">The SQL statement to execute; use {0}, {1} positional placeholders.</param>
        /// <param name="values">Positional parameter values corresponding to {0}, {1}, ...</param>
        /// <returns>The first column value of the first result row.</returns>
        public async Task<object?> ExecuteScalarAsync(string commandText, params object[] values)
        {
            var spec = new DbCommandSpec(DbCommandKind.Scalar, commandText, values);
            return (await ExecuteAsync(spec).ConfigureAwait(false)).Scalar;
        }

        /// <summary>
        /// Asynchronously executes a SQL statement and returns the result as a <see cref="DataTable"/>.
        /// </summary>
        /// <param name="commandText">The SQL statement to execute; use {0}, {1} positional placeholders.</param>
        /// <param name="values">Positional parameter values corresponding to {0}, {1}, ...</param>
        /// <returns>The query result as a <see cref="DataTable"/>.</returns>
        public async Task<DataTable?> ExecuteDataTableAsync(string commandText, params object[] values)
        {
            var spec = new DbCommandSpec(DbCommandKind.DataTable, commandText, values);
            return (await ExecuteAsync(spec).ConfigureAwait(false)).Table;
        }

        #endregion
    }
}
