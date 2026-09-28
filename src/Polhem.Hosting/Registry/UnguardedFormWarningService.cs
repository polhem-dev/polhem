using System.Data.Common;
using Polhem.Business;
using Polhem.Business.Form;
using Polhem.Definition.Forms;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Polhem.Hosting.Registry
{
    /// <summary>
    /// Hosted service that logs, once at startup, the registered forms whose <see cref="FormSchema"/>
    /// declares no <see cref="FormSchema.PermissionModelId"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Such a form is not permission-checked at all: every authenticated user of the company can read
    /// and write every row of it. That is intended during gradual adoption, but it is also what a
    /// forgotten attribute looks like, and nothing else would ever say so. The warning names the
    /// progIds so the list can be reviewed; finding such forms does not stop the host.
    /// </para>
    /// <para>
    /// The candidates are the form schemas the definition storage holds
    /// (<see cref="IDefineStorage.GetFormSchemaIds"/>) together with the <see cref="ProgramSettings"/>
    /// entries, minus the reserved progIds whose business object is not a form. A form does not have
    /// to be registered to be served: a progId the registry does not name resolves to
    /// <see cref="FormBusinessObject"/>. A candidate without a readable form schema is skipped.
    /// </para>
    /// </remarks>
    internal sealed class UnguardedFormWarningService : IHostedService
    {
        private readonly IDefineAccess _defineAccess;
        private readonly IDefineStorage _defineStorage;
        private readonly ILogger<UnguardedFormWarningService> _logger;

        /// <summary>
        /// Initializes a new <see cref="UnguardedFormWarningService"/>.
        /// </summary>
        /// <param name="defineAccess">Reads the registry and the form schemas.</param>
        /// <param name="defineStorage">Lists the form schemas the deployment holds.</param>
        /// <param name="logger">Logger.</param>
        public UnguardedFormWarningService(IDefineAccess defineAccess, IDefineStorage defineStorage,
            ILogger<UnguardedFormWarningService> logger)
        {
            _defineAccess = defineAccess ?? throw new ArgumentNullException(nameof(defineAccess));
            _defineStorage = defineStorage ?? throw new ArgumentNullException(nameof(defineStorage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_logger.IsEnabled(LogLevel.Warning)) { return Task.CompletedTask; }

            var progIds = FindUnguardedForms();
            if (progIds.Count > 0)
            {
                _logger.LogWarning(
                    "Forms without a PermissionModelId are readable and writable by every authenticated user of the company: {ProgIds}.",
                    string.Join(", ", progIds));
            }
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// Returns the progIds, stored or registered, whose form schema declares no permission model.
        /// </summary>
        internal List<string> FindUnguardedForms()
            => CandidateProgIds()
                .Where(IsFormProgId)
                .Where(progId => TryGetFormSchema(progId) is { } schema && string.IsNullOrEmpty(schema.PermissionModelId))
                .ToList();

        /// <summary>
        /// The stored form schemas and the registry entries, each progId once whatever its casing,
        /// in the stored spelling when the storage holds it.
        /// </summary>
        /// <remarks>
        /// The stored spelling comes first because it is the one the schema is read by: on a
        /// case-sensitive file system a registry entry spelled differently from the file does not find
        /// it, and the form would be skipped instead of reported. <c>Distinct</c> keeps the first
        /// occurrence of each progId; <c>FindUnguardedForms_StoredAndRegisteredInOtherCasing_NamesItOnce</c>
        /// fails if the registry spelling wins instead.
        /// </remarks>
        private IEnumerable<string> CandidateProgIds()
            => StoredFormIds().Concat(RegisteredProgIds())
                .Distinct(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The form schemas the storage holds; empty, with a warning, when a database-backed storage
        /// cannot be read at startup.
        /// </summary>
        private IReadOnlyList<string> StoredFormIds()
        {
            try
            {
                return _defineStorage.GetFormSchemaIds();
            }
            catch (DbException ex)
            {
                _logger.LogWarning(ex,
                    "The stored form schemas could not be listed, so only the ProgramSettings entries were checked for a PermissionModelId.");
                return [];
            }
        }

        private IEnumerable<string> RegisteredProgIds()
        {
            ProgramSettings registry;
            try
            {
                registry = _defineAccess.GetProgramSettings();
            }
            catch (FileNotFoundException)
            {
                return [];
            }
            return (registry.Items ?? []).Select(item => item.ProgId);
        }

        private static bool IsFormProgId(string progId)
        {
            var reserved = ReservedProgIds.Find(progId);
            return reserved == null || typeof(FormBusinessObject).IsAssignableFrom(reserved.ExpectedBaseType);
        }

        private FormSchema? TryGetFormSchema(string progId)
        {
            try
            {
                return _defineAccess.GetFormSchema(progId);
            }
            catch (FileNotFoundException)
            {
                // File-backed storage: a registry entry for a business object that has no form.
                return null;
            }
            catch (InvalidOperationException)
            {
                // Database-backed storage reports a missing definition this way.
                return null;
            }
        }
    }
}
