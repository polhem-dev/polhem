using System.Diagnostics;
using Polhem.Core.Security;
using Polhem.Definition.Logging;

namespace Polhem.Api.Core.JsonRpc
{
    /// <summary>
    /// Anomaly detection half of <see cref="JsonRpcExecutor"/>: slow and failed calls written to the
    /// API anomaly log. Split out for file size only; behaviour is unchanged.
    /// </summary>
    public sealed partial class JsonRpcExecutor
    {
        private bool AnomalyEnabled =>
            _anomalyWriter != null && _sessionService != null
            && _auditOptions is { Enabled: true, AnomalyEnabled: true };

        /// <summary>Records a Slow anomaly when a completed call exceeds the configured threshold.</summary>
        private void LogApiSlowAnomaly(string method, Stopwatch? stopwatch)
        {
            if (stopwatch == null || _auditOptions == null) { return; }
            stopwatch.Stop();
            int threshold = _auditOptions.ApiSlowThresholdMs;
            if (threshold > 0 && stopwatch.ElapsedMilliseconds > threshold)
                WriteApiAnomaly(method, AnomalyKind.Slow, stopwatch.ElapsedMilliseconds, thresholdMs: threshold);
        }

        /// <summary>Records an Error / Timeout anomaly for a failed call.</summary>
        private void LogApiFailureAnomaly(string method, Exception rootEx, Stopwatch? stopwatch)
        {
            if (stopwatch == null) { return; }
            stopwatch.Stop();
            WriteApiAnomaly(method, ClassifyFailure(rootEx), stopwatch.ElapsedMilliseconds,
                errorType: rootEx.GetType().Name, errorMessage: SanitizeMessage(rootEx.Message));
        }

        /// <summary>Decides which anomaly kind a failed call is filed under.</summary>
        /// <param name="rootEx">The unwrapped exception that ended the call.</param>
        /// <returns>The anomaly kind to record.</returns>
        /// <remarks>
        /// A replay rejection gets its own kind: unlike an Error it says nothing is broken, and a
        /// run of them points at a drifted client clock or a caller resending captured packets —
        /// neither of which is visible once folded into generic errors.
        /// </remarks>
        private static AnomalyKind ClassifyFailure(Exception rootEx)
        {
            if (rootEx is ReplayRejectedException) { return AnomalyKind.Replay; }
            return IsTimeout(rootEx) ? AnomalyKind.Timeout : AnomalyKind.Error;
        }

        private void WriteApiAnomaly(string method, AnomalyKind kind, long elapsedMs,
            int? thresholdMs = null, string? errorType = null, string? errorMessage = null)
        {
            if (_anomalyWriter == null || _sessionService == null) { return; }
            var session = _sessionService.Get(AccessToken);
            _anomalyWriter.Write(new ApiAnomalyEntry
            {
                UserId = session?.UserId,
                UserName = session?.UserName,
                CompanyId = session?.CompanyId,
                TokenFingerprint = AccessTokenHasher.ComputeFingerprint(AccessToken),
                ApiKeyId = NullIfEmpty(ApiKeyValidation.SysId),
                ApiKeyName = NullIfEmpty(ApiKeyValidation.SysName),
                Method = method,
                Kind = kind,
                ElapsedMs = elapsedMs > int.MaxValue ? int.MaxValue : (int)elapsedMs,
                ThresholdMs = thresholdMs,
                ErrorType = errorType,
                ErrorMessage = errorMessage,
                Source = method,
            });
        }

        /// <summary>
        /// Normalises an empty string to <c>null</c> so an audit column reads as "not applicable"
        /// rather than blank.
        /// </summary>
        /// <param name="value">The value to normalise.</param>
        private static string? NullIfEmpty(string? value)
            => string.IsNullOrEmpty(value) ? null : value;

        private static bool IsTimeout(Exception ex)
            => ex is TimeoutException
               || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);

        private static string SanitizeMessage(string message)
        {
            // Message text only (no stack trace); flattened and capped.
            var oneLine = message.Replace('\r', ' ').Replace('\n', ' ');
            return oneLine.Length <= 1000 ? oneLine : oneLine[..1000];
        }
    }
}
