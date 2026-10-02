using System.Data.Common;
using Polhem.Definition.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Polhem.Hosting.ApiKeys
{
    /// <summary>
    /// Hosted service that logs, once at startup, when no API key has been issued, which leaves the
    /// <c>X-Api-Key</c> header checked for presence only: an error, or a warning when the host runs in the
    /// Development environment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The remedy this points at is issuing a key, not writing code: the gate closes by itself as soon as one
    /// enabled key exists.
    /// </para>
    /// <para>
    /// NOTE: a store that cannot be reached is reported separately rather than as "no keys". Startup must not fail
    /// over this, but nor should an unreachable database read as an open gate, because at run time the same
    /// condition rejects calls.
    /// </para>
    /// </remarks>
    internal sealed class ApiKeyGateWarningService : IHostedService
    {
        private readonly IApiKeyGateStateProvider _gateState;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<ApiKeyGateWarningService> _logger;

        /// <summary>
        /// Initializes a new <see cref="ApiKeyGateWarningService"/>.
        /// </summary>
        /// <param name="gateState">Answers whether the API key gate is in force.</param>
        /// <param name="environment">Tells a Development host apart from a deployment.</param>
        /// <param name="logger">Logger.</param>
        public ApiKeyGateWarningService(
            IApiKeyGateStateProvider gateState,
            IHostEnvironment environment,
            ILogger<ApiKeyGateWarningService> logger)
        {
            _gateState = gateState ?? throw new ArgumentNullException(nameof(gateState));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_gateState.GetState() is { InForce: true }) { return Task.CompletedTask; }
                LogGateNotInForce();
            }
            catch (DbException ex)
            {
                LogStoreUnreachable(ex);
            }
            catch (InvalidOperationException ex)
            {
                LogStoreUnreachable(ex);
            }
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private void LogGateNotInForce()
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogWarning(
                    "No enabled API key exists, so the X-Api-Key header is only checked for presence. " +
                    "That is expected in Development; issue an API key before deploying.");
                return;
            }

            // Error rather than Warning: this is the difference between having an API key gate and not having one,
            // and a Warning is the level operators filter out. Startup is deliberately not failed, because the
            // presence-only fallback is the path that keeps existing deployments working across an upgrade.
            _logger.LogError(
                "No enabled API key exists, so the X-Api-Key header is only checked for presence, " +
                "not for its value: this deployment has no working API key gate. Issue an API key " +
                "to turn it into a real one; no code change is required, and existing callers keep " +
                "working until the first key exists.");
        }

        private void LogStoreUnreachable(Exception ex)
            => _logger.LogWarning(ex, "Could not determine whether any API key is enabled; API calls will be rejected while the store is unreachable.");
    }
}
