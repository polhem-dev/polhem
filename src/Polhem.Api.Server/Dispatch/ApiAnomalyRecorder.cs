using Microsoft.Extensions.DependencyInjection;
using Polhem.Core.Security;
using Polhem.Definition.Identity;
using Polhem.Definition.Logging;
using Polhem.Definition.Settings;

namespace Polhem.Api.Server.Dispatch
{
    /// <summary>
    /// Writes the API anomaly log entries of a call: a slow call, and a failed one.
    /// </summary>
    /// <remarks>
    /// Recording needs an anomaly writer, a session service and audit options with both <c>Enabled</c> and <c>AnomalyEnabled</c> set.
    /// </remarks>
    internal static class ApiAnomalyRecorder
    {
        public static bool IsEnabled(IServiceProvider services) =>
            services.GetService<IAnomalyLogWriter>() != null
            && services.GetService<ISessionInfoService>() != null
            && services.GetService<AuditLogOptions>() is { Enabled: true, AnomalyEnabled: true };

        public static void RecordSlow(IServiceProvider services, string method, PolhemCallState state)
        {
            var stopwatch = state.Stopwatch;
            var options = services.GetService<AuditLogOptions>();
            if (stopwatch == null || options == null) { return; }
            stopwatch.Stop();
            int threshold = options.ApiSlowThresholdMs;
            if (threshold > 0 && stopwatch.ElapsedMilliseconds > threshold)
            {
                Write(services, method, state, new AnomalyDetail(AnomalyKind.Slow, stopwatch.ElapsedMilliseconds, ThresholdMs: threshold));
            }
        }

        public static void RecordFailure(IServiceProvider services, string method, PolhemCallState state, Exception rootEx)
        {
            var stopwatch = state.Stopwatch;
            if (stopwatch == null) { return; }
            stopwatch.Stop();
            Write(services, method, state, new AnomalyDetail(Classify(rootEx), stopwatch.ElapsedMilliseconds,
                ErrorType: rootEx.GetType().Name, ErrorMessage: Sanitize(rootEx.Message)));
        }

        private static AnomalyKind Classify(Exception rootEx)
        {
            if (rootEx is Polhem.JsonRpc.Payload.ReplayRejectedException) { return AnomalyKind.Replay; }
            return rootEx is TimeoutException || rootEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                ? AnomalyKind.Timeout
                : AnomalyKind.Error;
        }

        private static void Write(IServiceProvider services, string method, PolhemCallState state, AnomalyDetail detail)
        {
            var writer = services.GetService<IAnomalyLogWriter>();
            var sessions = services.GetService<ISessionInfoService>();
            if (writer == null || sessions == null) { return; }
            var session = sessions.Get(state.AccessToken);
            writer.Write(new ApiAnomalyEntry
            {
                UserId = session?.UserId,
                UserName = session?.UserName,
                CompanyId = session?.CompanyId,
                TokenFingerprint = AccessTokenHasher.ComputeFingerprint(state.AccessToken),
                ApiKeyId = NullIfEmpty(state.ApiKeyValidation.SysId),
                ApiKeyName = NullIfEmpty(state.ApiKeyValidation.SysName),
                Method = method,
                Kind = detail.Kind,
                ElapsedMs = detail.ElapsedMs > int.MaxValue ? int.MaxValue : (int)detail.ElapsedMs,
                ThresholdMs = detail.ThresholdMs,
                ErrorType = detail.ErrorType,
                ErrorMessage = detail.ErrorMessage,
                Source = method,
            });
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

        private static string Sanitize(string message)
        {
            // Message text only (no stack trace); flattened and capped.
            var oneLine = message.Replace('\r', ' ').Replace('\n', ' ');
            return oneLine.Length <= 1000 ? oneLine : oneLine[..1000];
        }

        private readonly record struct AnomalyDetail(AnomalyKind Kind, long ElapsedMs, int? ThresholdMs = null,
            string? ErrorType = null, string? ErrorMessage = null);
    }
}
