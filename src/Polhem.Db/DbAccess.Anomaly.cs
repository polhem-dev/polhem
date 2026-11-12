using System.Data.Common;
using System.Diagnostics;

using Polhem.Definition.Database;
using Polhem.Definition.Logging;

namespace Polhem.Db
{
    /// <summary>
    /// Anomaly detection around command execution: what counts as slow or oversized, and how a failure is recorded.
    /// </summary>
    /// <remarks>
    /// Kept apart from the execution path because it is entirely best-effort: nothing here may alter a
    /// command's outcome. `DbAnomalyDetail`, `AnomalySubject` and `AnomalyCounts` live with it; they have no other caller.
    /// </remarks>
    public sealed partial class DbAccess
    {
        /// <summary>
        /// Runs <paramref name="exec"/>, detecting and recording anomalies (Error / Timeout on
        /// failure, Slow / LargeAffected / LargeResult on success). A no-op wrapper when anomaly
        /// logging is disabled. Anomaly writes are best-effort and never alter the command outcome.
        /// </summary>
        private DbCommandResult RunWithAnomalyDetection(DbCommandSpec command, Func<DbCommandResult> exec)
            => RunWithAnomalyDetection(AnomalySubject.Of(command), exec, MeasureResult);

        private T RunWithAnomalyDetection<T>(AnomalySubject subject, Func<T> exec, Func<T, AnomalyCounts> measure)
        {
            if (!IsAnomalyLoggingEnabled) { return exec(); }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = exec();
                stopwatch.Stop();
                LogSuccessAnomalies(subject, measure(result), stopwatch.ElapsedMilliseconds);
                return result;
            }
            catch (DbException ex)
            {
                stopwatch.Stop();
                LogFailureAnomaly(subject, ex, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        private Task<DbCommandResult> RunWithAnomalyDetectionAsync(
            DbCommandSpec command, Func<Task<DbCommandResult>> exec, CancellationToken cancellationToken)
            => RunWithAnomalyDetectionAsync(AnomalySubject.Of(command), exec, MeasureResult, cancellationToken);

        // NOTE: a provider may report a cancelled command as a `DbException` (SQL Server does), so a failure seen
        // after the caller's token was cancelled is the caller's cancellation, not an anomaly.
        private async Task<T> RunWithAnomalyDetectionAsync<T>(
            AnomalySubject subject, Func<Task<T>> exec, Func<T, AnomalyCounts> measure, CancellationToken cancellationToken)
        {
            if (!IsAnomalyLoggingEnabled) { return await exec().ConfigureAwait(false); }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = await exec().ConfigureAwait(false);
                stopwatch.Stop();
                LogSuccessAnomalies(subject, measure(result), stopwatch.ElapsedMilliseconds);
                return result;
            }
            catch (DbException ex) when (!cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                LogFailureAnomaly(subject, ex, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        private bool IsAnomalyLoggingEnabled
            => _anomalyWriter != null && _anomalyOptions != null && _anomalyOptions.Level != DbAccessAnomalyLogLevel.None;

        private static AnomalyCounts NoCounts<T>(T _) => default;

        private static AnomalyCounts MeasureResult(DbCommandResult result)
            => new(result.RowsAffected, result.Table?.Rows.Count ?? 0);

        private void LogSuccessAnomalies(AnomalySubject command, AnomalyCounts result, long elapsedMs)
        {
            // Slow / large-row are "abnormal but succeeded" — only recorded at Warning level.
            if (_anomalyOptions!.Level != DbAccessAnomalyLogLevel.Warning) { return; }

            int slowThresholdMs = _anomalyOptions.ExecutionTimeThreshold > 0
                ? _anomalyOptions.ExecutionTimeThreshold * 1000 : 0;
            if (slowThresholdMs > 0 && elapsedMs > slowThresholdMs)
                WriteDbAnomaly(command, AnomalyKind.Slow, elapsedMs, new DbAnomalyDetail { ThresholdMs = slowThresholdMs });

            if (_anomalyOptions.AffectedRowThreshold > 0 && result.RowsAffected > _anomalyOptions.AffectedRowThreshold)
                WriteDbAnomaly(command, AnomalyKind.LargeAffected, elapsedMs, new DbAnomalyDetail { AffectedRows = result.RowsAffected });

            int resultRows = result.ResultRows;
            if (_anomalyOptions.ResultRowThreshold > 0 && resultRows > _anomalyOptions.ResultRowThreshold)
                WriteDbAnomaly(command, AnomalyKind.LargeResult, elapsedMs, new DbAnomalyDetail { ResultRows = resultRows });
        }

        private void LogFailureAnomaly(AnomalySubject command, DbException ex, long elapsedMs)
        {
            var kind = IsTimeout(ex, elapsedMs, command) ? AnomalyKind.Timeout : AnomalyKind.Error;
            WriteDbAnomaly(command, kind, elapsedMs,
                new DbAnomalyDetail { ErrorType = ex.GetType().Name, ErrorMessage = SanitizeMessage(ex.Message) });
        }

        private bool IsTimeout(DbException ex, long elapsedMs, AnomalySubject command)
        {
            if (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)) { return true; }
            if (!command.InferTimeoutFromElapsed) { return false; }
            int timeoutSec = ResolveTimeout(command.CommandTimeout);
            return timeoutSec > 0 && elapsedMs >= (long)timeoutSec * 1000 * 9 / 10;
        }

        private void WriteDbAnomaly(AnomalySubject command, AnomalyKind kind, long elapsedMs, DbAnomalyDetail detail = default)
        {
            try
            {
                _anomalyWriter!.Write(CreateEntry(command, kind, elapsedMs, detail));
            }
            catch (Exception ex)
            {
                // Boundary: recording an anomaly is best effort. A writer that throws would otherwise replace the
                // command's own exception, or fail a command that succeeded and roll back the batch around it.
                Trace.TraceWarning("A database anomaly could not be recorded: {0}", ex.GetType().Name);
            }
        }

        private DbAnomalyEntry CreateEntry(AnomalySubject command, AnomalyKind kind, long elapsedMs, DbAnomalyDetail detail)
            => new()
            {
                DatabaseId = _databaseId,
                Command = command.CommandText,   // {0} template only — never the parameter values
                Kind = kind,
                ElapsedMs = elapsedMs > int.MaxValue ? int.MaxValue : (int)elapsedMs,
                ThresholdMs = detail.ThresholdMs,
                AffectedRows = detail.AffectedRows,
                ResultRows = detail.ResultRows,
                ErrorType = detail.ErrorType,
                ErrorMessage = detail.ErrorMessage,
            };

        /// <summary>
        /// What an anomaly is recorded against: the command template (never its parameter values) and the
        /// timeout the command ran with, which tells a timeout from another failure. A subject that spans several
        /// round trips, or is not bound by a command timeout, turns off <see cref="InferTimeoutFromElapsed"/>, since its total
        /// time says nothing about one command's timeout. Slow is still recorded for it: a write or a wait that takes
        /// that long is worth seeing whatever its cause.
        /// </summary>
        private readonly record struct AnomalySubject(string CommandText, int CommandTimeout, bool InferTimeoutFromElapsed = true)
        {
            public static AnomalySubject Of(DbCommandSpec command) => new(command.CommandText, command.CommandTimeout);
        }

        /// <summary>The rows a successful command affected and returned, for the large-* anomaly kinds.</summary>
        private readonly record struct AnomalyCounts(int RowsAffected, int ResultRows);

        /// <summary>
        /// Optional per-kind detail for a <see cref="DbAnomalyEntry"/>: each anomaly kind sets only the
        /// fields it carries (threshold for Slow, affected/result rows for the large-* kinds, error
        /// type/message for failures), leaving the rest null.
        /// </summary>
        private readonly struct DbAnomalyDetail
        {
            public int? ThresholdMs { get; init; }
            public int? AffectedRows { get; init; }
            public int? ResultRows { get; init; }
            public string? ErrorType { get; init; }
            public string? ErrorMessage { get; init; }
        }

        private static string SanitizeMessage(string message)
        {
            // Provider error text only (no stack trace, no parameter values); flattened and capped.
            var oneLine = message.Replace('\r', ' ').Replace('\n', ' ');
            return oneLine.Length <= MaxLoggedMessageLength ? oneLine : oneLine[..MaxLoggedMessageLength];
        }
    }
}
