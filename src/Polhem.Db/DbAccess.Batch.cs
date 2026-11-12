using System.Data.Common;

namespace Polhem.Db
{
    /// <summary>
    /// Running several commands as one batch, optionally in one transaction.
    /// </summary>
    /// <remarks>
    /// A failure is rolled back and then rethrown as it is, so a caller still tells a cancellation from a database
    /// error by its type; the index of the failing command is added to the exception's
    /// <see cref="Exception.Data"/> under <see cref="DbBatchSpec.FailedIndexDataKey"/>. Opening the connection,
    /// beginning the transaction and committing it go through anomaly detection like the commands do.
    /// </remarks>
    public partial class DbAccess
    {
        // Opening a connection, beginning a transaction and committing it have no command text of their own, so the
        // anomaly log names each with fixed text. None is bound by a command timeout, so elapsed time does not imply one.
        private static readonly AnomalySubject s_openSubject = new("OPEN", 0, InferTimeoutFromElapsed: false);
        private static readonly AnomalySubject s_beginSubject = new("BEGIN TRANSACTION", 0, InferTimeoutFromElapsed: false);
        private static readonly AnomalySubject s_commitSubject = new("COMMIT", 0, InferTimeoutFromElapsed: false);

        /// <summary>
        /// Executes multiple database commands as a batch.
        /// </summary>
        /// <param name="batch">The batch command specification.</param>
        /// <returns>The result of each command, in order.</returns>
        /// <remarks>
        /// When a command fails, the transaction (if any) is rolled back and the command's exception is rethrown
        /// unchanged, with the command's index in <see cref="Exception.Data"/> under
        /// <see cref="DbBatchSpec.FailedIndexDataKey"/>. A failed commit is rethrown unchanged.
        /// </remarks>
        public DbBatchResult ExecuteBatch(DbBatchSpec batch)
        {
            ValidateBatch(batch);

            var result = new DbBatchResult();
            using var scope = RunWithAnomalyDetection(s_openSubject, CreateScope, NoCounts);
            var connection = scope.Connection!;
            using var tran = batch.UseTransaction
                ? RunWithAnomalyDetection(s_beginSubject, () => BeginBatchTransaction(connection, batch), NoCounts)
                : null;

            for (int i = 0; i < batch.Commands.Count; i++)
            {
                var spec = batch.Commands[i];
                try
                {
                    AddResult(result, spec, RunWithAnomalyDetection(spec, () => DispatchExecute(spec, connection, tran)));
                }
                catch (Exception ex)
                {
                    TryRollbackQuiet(tran);
                    MarkFailedIndex(ex, i);
                    throw;
                }
            }

            if (tran != null) { CommitWithAnomalyDetection(tran); }
            return result;
        }

        /// <summary>
        /// Asynchronously executes multiple database commands as a batch.
        /// </summary>
        /// <param name="batch">The batch command specification.</param>
        /// <param name="cancellationToken">A cancellation token for cancelling long-running commands.</param>
        /// <returns>The result of each command, in order.</returns>
        /// <remarks>
        /// When a command fails or is cancelled, the transaction (if any) is rolled back and the exception is
        /// rethrown unchanged, with the failing command's index in <see cref="Exception.Data"/> under
        /// <see cref="DbBatchSpec.FailedIndexDataKey"/>. A <see cref="DbException"/> that arrives after
        /// <paramref name="cancellationToken"/> was cancelled is rethrown as an <see cref="OperationCanceledException"/>
        /// that carries it, because SQL Server reports a cancelled command that way; an error that happened just
        /// before the cancellation is reported the same way and is not recorded as an anomaly. Once every command
        /// has run the commit is not cancelled, and a failed commit is rethrown unchanged.
        /// </remarks>
        public async Task<DbBatchResult> ExecuteBatchAsync(DbBatchSpec batch, CancellationToken cancellationToken = default)
        {
            ValidateBatch(batch);
            try
            {
                return await RunBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            }
            catch (DbException ex) when (cancellationToken.IsCancellationRequested)
            {
                var cancelled = new OperationCanceledException(ex.Message, ex, cancellationToken);
                if (ex.Data.Contains(DbBatchSpec.FailedIndexDataKey))
                    cancelled.Data[DbBatchSpec.FailedIndexDataKey] = ex.Data[DbBatchSpec.FailedIndexDataKey];
                throw cancelled;
            }
        }

        private async Task<DbBatchResult> RunBatchAsync(DbBatchSpec batch, CancellationToken cancellationToken)
        {
            var result = new DbBatchResult();
            using var scope = await RunWithAnomalyDetectionAsync(
                s_openSubject, () => CreateScopeAsync(cancellationToken), NoCounts, cancellationToken).ConfigureAwait(false);
            var connection = scope.Connection!;
            var tran = batch.UseTransaction
                ? await RunWithAnomalyDetectionAsync(
                    s_beginSubject, () => BeginBatchTransactionAsync(connection, batch, cancellationToken), NoCounts, cancellationToken)
                    .ConfigureAwait(false)
                : null;
            try
            {
                for (int i = 0; i < batch.Commands.Count; i++)
                {
                    var spec = batch.Commands[i];
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var item = await RunWithAnomalyDetectionAsync(
                            spec, () => DispatchExecuteAsync(spec, connection, tran, cancellationToken), cancellationToken)
                            .ConfigureAwait(false);
                        AddResult(result, spec, item);
                    }
                    catch (Exception ex)
                    {
                        TryRollbackQuiet(tran);
                        MarkFailedIndex(ex, i);
                        throw;
                    }
                }

                if (tran != null)
                {
                    // NOTE: once every command has run, the commit is not cancelled. A commit cancelled mid-flight
                    // leaves the outcome unknown: the server may have committed while the caller is told it did not.
                    await RunWithAnomalyDetectionAsync(s_commitSubject, async () =>
                    {
                        await tran.CommitAsync(CancellationToken.None).ConfigureAwait(false);
                        return true;
                    }, NoCounts, CancellationToken.None).ConfigureAwait(false);
                }
                return result;
            }
            finally
            {
                if (tran != null) await tran.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static DbTransaction BeginBatchTransaction(DbConnection connection, DbBatchSpec batch)
            => batch.IsolationLevel.HasValue
                ? connection.BeginTransaction(batch.IsolationLevel.Value)
                : connection.BeginTransaction();

        private static async Task<DbTransaction> BeginBatchTransactionAsync(
            DbConnection connection, DbBatchSpec batch, CancellationToken cancellationToken)
            => batch.IsolationLevel.HasValue
                ? await connection.BeginTransactionAsync(batch.IsolationLevel.Value, cancellationToken).ConfigureAwait(false)
                : await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        private static void ValidateBatch(DbBatchSpec batch)
        {
            ArgumentNullException.ThrowIfNull(batch);
            if (batch.Commands == null) throw new ArgumentException("batch.Commands cannot be null.", nameof(batch));
            if (batch.Commands.Count == 0) throw new ArgumentException("Batch contains no commands.", nameof(batch));
        }

        private static void AddResult(DbBatchResult result, DbCommandSpec spec, DbCommandResult item)
        {
            if (spec.Kind == DbCommandKind.NonQuery) { result.RowsAffectedSum += item.RowsAffected; }
            result.Results.Add(item);
        }

        private static void MarkFailedIndex(Exception ex, int index)
        {
            if (!ex.Data.Contains(DbBatchSpec.FailedIndexDataKey)) { ex.Data[DbBatchSpec.FailedIndexDataKey] = index; }
        }
    }
}
