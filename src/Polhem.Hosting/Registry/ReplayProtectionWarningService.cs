using System.Reflection;
using Polhem.JsonRpc.Payload;
using Polhem.JsonRpc.Server;
using Polhem.Api.Core.Validator;
using Polhem.Business;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Polhem.Hosting.Registry
{
    /// <summary>
    /// Hosted service that logs, once at startup, when API methods declare
    /// <see cref="ApiReplayProtection.UniqueSequence"/> while <see cref="PayloadOptions.RequireFrame"/>
    /// is off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The declaration reads as a guarantee, but without the wire frame no call carries a sequence
    /// number and the check never runs — for every format, not only the Plain and Encoded ones it
    /// cannot protect anyway. The switch is off by default and nothing in the framework turns it on,
    /// so a deployment that has not decided otherwise runs its write methods without replay
    /// protection. The warning names the methods so the decision is visible; it never stops the host.
    /// </para>
    /// <para>
    /// The candidates are the business object types behind the reserved progIds and the
    /// <see cref="ProgramSettings"/> entries, as the base layer resolves them. A progId that does not
    /// resolve is skipped: <see cref="ReservedProgIdRegistrationService"/> and the first request
    /// report that far more usefully.
    /// </para>
    /// </remarks>
    internal sealed class ReplayProtectionWarningService : IHostedService
    {
        private readonly IDefineAccess _defineAccess;
        private readonly IBoTypeResolver _resolver;
        private readonly ILogger<ReplayProtectionWarningService> _logger;
        private readonly PayloadOptions _payloadOptions;

        /// <summary>
        /// Initializes a new <see cref="ReplayProtectionWarningService"/>.
        /// </summary>
        /// <param name="defineAccess">Reads the registry.</param>
        /// <param name="resolver">Resolves each progId to its business object type.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="payloadOptions">The server's payload options; the warning applies only while frames are off.</param>
        public ReplayProtectionWarningService(
            IDefineAccess defineAccess,
            IBoTypeResolver resolver,
            ILogger<ReplayProtectionWarningService> logger,
            PayloadOptions payloadOptions)
        {
            _defineAccess = defineAccess ?? throw new ArgumentNullException(nameof(defineAccess));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _payloadOptions = payloadOptions ?? throw new ArgumentNullException(nameof(payloadOptions));
        }

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_payloadOptions.RequireFrame || !_logger.IsEnabled(LogLevel.Warning))
                return Task.CompletedTask;

            var methods = FindReplayProtectedMethods();
            if (methods.Count > 0)
            {
                _logger.LogWarning(
                    "PayloadOptions.RequireFrame is off, so methods declaring ReplayProtection = UniqueSequence are not checked for replayed calls: {Methods}. "
                    + "Enable the wire frame on server and clients to turn the check on. Once it is on, remote calls to these methods "
                    + "from a signed-in session are accepted only when Encrypted, so the client session needs an encryption key to call them; "
                    + "local and anonymous calls are not checked.",
                    string.Join(", ", methods));
            }
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// Returns <c>progId.action</c> for every resolvable action that declares
        /// <see cref="ApiReplayProtection.UniqueSequence"/>, in progId order.
        /// </summary>
        internal List<string> FindReplayProtectedMethods()
        {
            var result = new List<string>();
            foreach (var progId in CandidateProgIds())
            {
                var type = TryResolve(progId);
                if (type == null) { continue; }

                result.AddRange(type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(JsonRpcMethod.IsResolvableAction)
                    .Where(m => ApiAccessValidator.FindAccessControl(m)?.ReplayProtection == ApiReplayProtection.UniqueSequence)
                    .Select(m => $"{progId}.{m.Name}")
                    .OrderBy(name => name, StringComparer.Ordinal));
            }
            return result;
        }

        private SortedSet<string> CandidateProgIds()
        {
            var progIds = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var binding in ReservedProgIds.All)
                progIds.Add(binding.ProgId);

            try
            {
                foreach (var item in _defineAccess.GetProgramSettings().Items ?? [])
                    progIds.Add(item.ProgId);
            }
            catch (FileNotFoundException)
            {
                // No registry file: the reserved progIds are all there is.
            }
            return progIds;
        }

        private Type? TryResolve(string progId)
        {
            try
            {
                return _resolver.Resolve(progId);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }
    }
}
