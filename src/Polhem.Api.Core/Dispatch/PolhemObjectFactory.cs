using System.Data.Common;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polhem.Api.Core.Authorization;
using Polhem.Api.Core.Messages;
using Polhem.Core;
using Polhem.Definition;
using Polhem.Definition.Logging;
using Polhem.Definition.Settings;
using Polhem.Definition.Security;
using Polhem.JsonRpc;
using Polhem.JsonRpc.Server;

namespace Polhem.Api.Core.Dispatch
{
    /// <summary>
    /// Creates the business object for the ProgId of a method name, after checking who is calling.
    /// </summary>
    /// <remarks>
    /// A call that arrives over HTTP is checked first: the <c>X-Api-Key</c> header against the registered
    /// <see cref="IApiKeyValidator"/>, then the whole request against <see cref="ApiServiceOptions.AuthorizationValidator"/>,
    /// which also yields the access token from the <c>Authorization</c> header. Only a call that passes gets a business
    /// object, as it did when the controller ran these checks before the executor. An in-process call takes its access
    /// token from <see cref="PolhemJsonRpc.AccessTokenItem"/> and is not checked, as a local call never was.
    /// </remarks>
    public sealed class PolhemObjectFactory : IJsonRpcObjectFactory
    {
        /// <inheritdoc/>
        public object? CreateObject(string progId, JsonRpcRequestContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            var services = context.Services
                ?? throw new InvalidOperationException("A Polhem call needs the services of its scope.");

            var state = PolhemCallState.Create(context);
            if (ApiAnomalyRecorder.IsEnabled(services))
            {
                state.Stopwatch = Stopwatch.StartNew();
            }

            if (context.Transport.Kind == JsonRpcTransportKind.InProcess)
            {
                state.IsLocalCall = true;
                state.AccessToken = context.Transport.Items.TryGetValue(PolhemJsonRpc.AccessTokenItem, out var token) && token is Guid guid
                    ? guid
                    : Guid.Empty;
            }
            else
            {
                Authorize(context, state, services);
            }

            var businessObject = services.GetRequiredService<IBusinessObjectFactory>()
                .CreateBusinessObject(state.AccessToken, progId, state.IsLocalCall);

            // Hand the caller's application identity to business objects that ask for it. A setter rather than a
            // constructor argument: only a few methods care, and widening every business-object constructor would
            // break each application subclass for their sake.
            if (businessObject is IApiKeyContextAware apiKeyAware)
            {
                apiKeyAware.ApiKeyValidation = state.ApiKeyValidation;
            }
            return businessObject;
        }

        private static void Authorize(JsonRpcRequestContext context, PolhemCallState state, IServiceProvider services)
        {
            var headers = context.Transport.Headers;
            headers.TryGetValue(ApiHeaders.ApiKey, out var apiKey);
            headers.TryGetValue(ApiHeaders.Authorization, out var authorization);

            state.ApiKeyValidation = ValidateApiKey(apiKey, services);
            var result = ApiServiceOptions.AuthorizationValidator.Validate(new ApiAuthorizationContext
            {
                ApiKey = apiKey ?? string.Empty,
                Authorization = authorization ?? string.Empty,
                Method = context.Request.Method,
                ApiKeyValidation = state.ApiKeyValidation,
            });

            if (!result.IsValid)
            {
                if (state.ApiKeyValidation.Status == ApiKeyStatus.Invalid)
                {
                    WriteApiKeyAnomaly(context, state, services);
                }
                throw new JsonRpcErrorException((int)result.Code, result.ErrorMessage);
            }
            state.AccessToken = result.AccessToken;
        }

        private static ApiKeyValidationResult ValidateApiKey(string? apiKey, IServiceProvider services)
        {
            var validator = services.GetService<IApiKeyValidator>();
            if (validator == null)
            {
                return ApiKeyValidationResult.NotChecked;
            }

            // WARNING: a failed lookup is turned into a rejection here, never into "no keys configured". Reporting that
            // the deployment has no keys when the database is merely unreachable would reopen the gate for the
            // duration of an outage.
            try
            {
                return validator.Validate(apiKey);
            }
            catch (DbException ex)
            {
                LogApiKeyLookupFailure(services, ex);
                return new ApiKeyValidationResult(ApiKeyStatus.Invalid);
            }
            catch (InvalidOperationException ex)
            {
                LogApiKeyLookupFailure(services, ex);
                return new ApiKeyValidationResult(ApiKeyStatus.Invalid);
            }
        }

        private static void LogApiKeyLookupFailure(IServiceProvider services, Exception ex)
        {
            services.GetService<ILoggerFactory>()?.CreateLogger<PolhemObjectFactory>()
                .LogWarning(ex, "API key validation failed to reach its store; the request was rejected.");
        }

        private static void WriteApiKeyAnomaly(JsonRpcRequestContext context, PolhemCallState state, IServiceProvider services)
        {
            var options = services.GetService<AuditLogOptions>();
            if (options is not { Enabled: true, AnomalyEnabled: true }) { return; }
            var writer = services.GetService<IAnomalyLogWriter>();
            if (writer == null) { return; }

            var method = context.Request.Method;
            string sysId = state.ApiKeyValidation.SysId;
            writer.Write(new ApiAnomalyEntry
            {
                Method = method,
                Kind = AnomalyKind.Unauthorized,
                ErrorType = "ApiKeyRejected",
                ApiKeyId = StringUtilities.IsEmpty(sysId) ? null : sysId,
                ClientIp = context.Transport.RemoteAddress,
                Source = method,
            });
        }
    }
}
