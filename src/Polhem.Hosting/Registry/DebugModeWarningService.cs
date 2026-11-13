using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polhem.Core;

namespace Polhem.Hosting.Registry
{
    /// <summary>
    /// Hosted service that logs, once at startup, when <see cref="SysInfo.IsDebugMode"/> is on in a host that is not
    /// running in the Development environment.
    /// </summary>
    /// <remarks>
    /// Debug mode sends the text of a server exception to the caller instead of a fixed message, and allows the
    /// <c>none</c> payload encryptor. The project templates start with it on, so a deployment made from one keeps it
    /// unless someone turns it off in <c>SystemSettings.xml</c>. Logs only; never fails startup.
    /// </remarks>
    internal sealed class DebugModeWarningService : IHostedService
    {
        private readonly IHostEnvironment _environment;
        private readonly ILogger<DebugModeWarningService> _logger;
        private readonly Func<bool> _isDebugMode;

        /// <summary>
        /// Initializes a new <see cref="DebugModeWarningService"/>.
        /// </summary>
        /// <param name="environment">Tells a Development host apart from a deployment.</param>
        /// <param name="logger">Logger.</param>
        public DebugModeWarningService(IHostEnvironment environment, ILogger<DebugModeWarningService> logger)
            : this(environment, logger, () => SysInfo.IsDebugMode)
        {
        }

        /// <summary>
        /// Initializes a new <see cref="DebugModeWarningService"/> that reads the debug flag from
        /// <paramref name="isDebugMode"/>, so a test does not change the process-wide <see cref="SysInfo"/>.
        /// </summary>
        /// <param name="environment">Tells a Development host apart from a deployment.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="isDebugMode">Reads whether debug mode is on.</param>
        internal DebugModeWarningService(IHostEnvironment environment, ILogger<DebugModeWarningService> logger,
            Func<bool> isDebugMode)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _isDebugMode = isDebugMode ?? throw new ArgumentNullException(nameof(isDebugMode));
        }

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_isDebugMode() && !_environment.IsDevelopment())
            {
                // Error rather than Warning, as for an API key gate that is not in force: the setting decides what
                // every caller can read in an error message, and a Warning is the level operators filter out.
                _logger.LogError(
                    "Debug mode is on in the {Environment} environment: callers receive the text of server exceptions " +
                    "and the 'none' payload encryptor is allowed. Set IsDebugMode to false in SystemSettings.xml " +
                    "before deploying.", _environment.EnvironmentName);
            }
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
