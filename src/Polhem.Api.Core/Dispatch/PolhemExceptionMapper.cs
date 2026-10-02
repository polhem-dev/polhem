using System.Data.Common;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polhem.Api.Core.JsonRpc;
using Polhem.Core;
using Polhem.Core.Exceptions;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.JsonRpc.Server;
using RpcError = Polhem.JsonRpc.JsonRpcError;

namespace Polhem.Api.Core.Dispatch
{
    /// <summary>
    /// Turns an exception from a Polhem call into the error the caller receives, through the error contract
    /// (<see cref="JsonRpcErrorContract"/>, adr-043).
    /// </summary>
    /// <remarks>
    /// The framework's user-facing exceptions keep their message, translated into the session's language when the
    /// exception names a message key; every other exception gets a generic message, and its real one is logged. A
    /// failed call is also written to the API anomaly log.
    /// </remarks>
    public static class PolhemExceptionMapper
    {
        /// <summary>
        /// Maps an exception; assign it to <see cref="JsonRpcServerOptions.ExceptionMapper"/>.
        /// </summary>
        /// <param name="exception">The exception the call threw.</param>
        /// <param name="context">The call.</param>
        /// <returns>The error to answer with.</returns>
        public static RpcError Map(Exception exception, JsonRpcRequestContext context)
        {
            ArgumentNullException.ThrowIfNull(exception);
            ArgumentNullException.ThrowIfNull(context);
            var rootEx = exception.Unwrap();
            var services = context.Services;
            var state = PolhemCallState.Find(context);
            var method = context.Request.Method;

            var (code, message) = MapCode(rootEx);
            message = Localize(rootEx, message, services, state?.AccessToken ?? Guid.Empty);
            if (services != null)
            {
                LogMaskedFailure(services, method, rootEx, code);
                if (state != null) { ApiAnomalyRecorder.RecordFailure(services, method, state, rootEx); }
            }
            return new RpcError((int)code, message);
        }

        internal static (JsonRpcErrorCode Code, string Message) MapCode(Exception ex)
        {
            if (JsonRpcErrorContract.TryGetCode(ex, out var code, out var fixedMessage))
                return (code, fixedMessage == null || SysInfo.IsDebugMode ? ex.Message : fixedMessage);
            return (JsonRpcErrorCode.InternalError, SysInfo.IsDebugMode ? ex.Message : "Internal server error");
        }

        private static string Localize(Exception ex, string message, IServiceProvider? services, Guid accessToken)
        {
            var languageService = services?.GetService<ILanguageService>();
            if (languageService is null
                || ex is not ILocalizableMessage localizable
                || string.IsNullOrEmpty(localizable.MessageKey)
                || !JsonRpcErrorContract.TryGetCode(ex, out _, out var fixedMessage)
                || fixedMessage != null)
            {
                return message;
            }

            var session = FindSession(ex, services!, accessToken);
            (string @namespace, string subKey) = LanguageKey.Split(localizable.MessageKey);
            if (!languageService.TryResolveLangText(session?.CustomizeId ?? string.Empty, session?.Culture ?? string.Empty,
                    @namespace, subKey, out string template))
            {
                return message;
            }

            try
            {
                return localizable.MessageArguments.Count == 0
                    ? template
                    : string.Format(CultureInfo.InvariantCulture, template, [.. localizable.MessageArguments]);
            }
            catch (FormatException)
            {
                // A translation that names more placeholders than the throw site supplies must not turn a clear
                // refusal into a server error; the English text is still correct.
                return message;
            }
        }

        private static SessionInfo? FindSession(Exception ex, IServiceProvider services, Guid accessToken)
        {
            // An authentication failure means there is no usable session; looking it up again would repeat the
            // failure on the error path.
            var sessions = services.GetService<ISessionInfoService>();
            if (sessions is null || accessToken == Guid.Empty || ex is AuthenticationRequiredException)
                return null;

            try
            {
                return sessions.Get(accessToken);
            }
            catch (Exception lookupEx) when (lookupEx is DbException or InvalidOperationException or TimeoutException)
            {
                // The error being reported matters more than its language: a session store that cannot answer here
                // leaves the message in the default language.
                return null;
            }
        }

        private static void LogMaskedFailure(IServiceProvider services, string method, Exception rootEx, JsonRpcErrorCode code)
        {
            var logger = services.GetService<ILoggerFactory>()?.CreateLogger(typeof(PolhemExceptionMapper));
            if (logger == null) { return; }
            if (JsonRpcErrorContract.TryGetCode(rootEx, out _, out var fixedMessage) && fixedMessage == null) { return; }

            if (code == JsonRpcErrorCode.InternalError)
                logger.LogError(rootEx, "JSON-RPC method {Method} failed; the caller received a generic error message.", method);
            else
                logger.LogWarning(rootEx, "JSON-RPC method {Method} was refused; the caller received a generic message for error code {Code}.", method, (int)code);
        }
    }
}
