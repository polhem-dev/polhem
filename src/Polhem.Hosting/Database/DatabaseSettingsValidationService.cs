using Polhem.Definition;
using Microsoft.Extensions.Hosting;

namespace Polhem.Hosting.Database
{
    /// <summary>
    /// Hosted service that checks, at startup, that the database settings contain the items the
    /// framework cannot run without, through <see cref="IDatabaseSettingsProvider.ValidateRequired"/>.
    /// Registered by <c>AddPolhemFramework</c> ahead of every other hosted service.
    /// </summary>
    /// <remarks>
    /// Without this check a missing <c>common</c> item surfaced only on the first request that touched a
    /// framework table, as an <see cref="InvalidOperationException"/> from the connection manager, which
    /// points at the request rather than at the deployment. Failing here stops the host before it accepts
    /// a request, with the same message. <see cref="StartAsync"/> rather than
    /// <c>BackgroundService.ExecuteAsync</c>, so the check completes before the host starts listening.
    /// </remarks>
    internal sealed class DatabaseSettingsValidationService : IHostedService
    {
        private readonly IDatabaseSettingsProvider _settingsProvider;

        /// <summary>
        /// Initializes a new <see cref="DatabaseSettingsValidationService"/>.
        /// </summary>
        /// <param name="settingsProvider">The provider whose settings are validated.</param>
        public DatabaseSettingsValidationService(IDatabaseSettingsProvider settingsProvider)
        {
            _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        }

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a required database item is missing, which stops the host from starting.
        /// </exception>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _settingsProvider.ValidateRequired();
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
