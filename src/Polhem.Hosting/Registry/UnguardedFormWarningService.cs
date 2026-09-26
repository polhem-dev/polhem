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
    /// progIds so the list can be reviewed; it never stops the host.
    /// </para>
    /// <para>
    /// The candidates are the <see cref="ProgramSettings"/> entries, minus the reserved progIds whose
    /// business object is not a form. An entry without a readable form schema is skipped.
    /// </para>
    /// </remarks>
    internal sealed class UnguardedFormWarningService : IHostedService
    {
        private readonly IDefineAccess _defineAccess;
        private readonly ILogger<UnguardedFormWarningService> _logger;

        /// <summary>
        /// Initializes a new <see cref="UnguardedFormWarningService"/>.
        /// </summary>
        /// <param name="defineAccess">Reads the registry and the form schemas.</param>
        /// <param name="logger">Logger.</param>
        public UnguardedFormWarningService(IDefineAccess defineAccess, ILogger<UnguardedFormWarningService> logger)
        {
            _defineAccess = defineAccess ?? throw new ArgumentNullException(nameof(defineAccess));
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
        /// Returns the registered progIds whose form schema declares no permission model.
        /// </summary>
        internal List<string> FindUnguardedForms()
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

            var progIds = new List<string>();
            foreach (var item in registry.Items ?? [])
            {
                if (!IsFormProgId(item.ProgId)) { continue; }

                var schema = TryGetFormSchema(item.ProgId);
                if (schema != null && string.IsNullOrEmpty(schema.PermissionModelId))
                    progIds.Add(item.ProgId);
            }
            return progIds;
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
