using System.Globalization;
using System.Text;
using Polhem.Definition.Database;
using Polhem.Definition.Logging;
using Polhem.Definition.Settings;
using Polhem.Repository.Abstractions.AuditLog;
using Microsoft.Extensions.Logging;

namespace Polhem.Hosting.Audit
{
    /// <summary>
    /// Persists <see cref="AuditEntry"/> rows through <see cref="IAuditLogWriteRepository"/>.
    /// A failed write is logged and, when a fallback path is configured, spilled to a file so audit
    /// entries survive a transient log-database outage. This is the terminal sink shared by both the
    /// background and synchronous writers.
    /// </summary>
    /// <remarks>
    /// Holds no SQL: statement construction and execution live in the repository layer. What stays
    /// here is the hosting-layer concern — deciding what happens when the write fails.
    /// </remarks>
    internal sealed class AuditLogDbSink : IAuditLogSink
    {
        private readonly IAuditLogWriteRepository _repository;
        private readonly AuditLogOptions _options;
        private readonly ILogger<AuditLogDbSink> _logger;

        /// <summary>
        /// Initializes a new <see cref="AuditLogDbSink"/>.
        /// </summary>
        public AuditLogDbSink(IAuditLogWriteRepository repository, AuditLogOptions options, ILogger<AuditLogDbSink> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Serializes appends to the fallback file across every writer in this process.
        /// </summary>
        private static readonly Lock s_fileGate = new();

        /// <inheritdoc/>
        public void WriteBatch(IReadOnlyList<AuditEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);
            if (entries.Count == 0) { return; }

            var failure = TryWrite(entries);
            if (failure is null) { return; }

            _logger.LogError(failure, "Audit log write failed against the '{DatabaseId}' database.", DbCategoryIds.Log);
            if (entries.Count == 1)
            {
                SpillToFile(entries);
                return;
            }
            SpillToFile(RetryOneByOne(entries));
        }

        /// <summary>
        /// Writes the entries of a failed batch one at a time and returns the ones that still fail, so one entry the
        /// database refuses does not take the rest of its batch with it.
        /// </summary>
        /// <remarks>
        /// Two failures in a row are taken for an outage rather than for bad entries: every entry not yet tried is
        /// returned as failed without trying it, so a database that is down costs two more attempts, not one per
        /// entry.
        /// </remarks>
        private List<AuditEntry> RetryOneByOne(IReadOnlyList<AuditEntry> entries)
        {
            var failed = new List<AuditEntry>();
            int consecutiveFailures = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (consecutiveFailures >= 2)
                {
                    failed.AddRange(entries.Skip(i));
                    break;
                }
                var failure = TryWrite([entries[i]]);
                if (failure is null)
                {
                    consecutiveFailures = 0;
                    continue;
                }
                consecutiveFailures++;
                failed.Add(entries[i]);
                _logger.LogError(failure, "Audit log entry for '{TableName}' could not be written to the '{DatabaseId}' database.",
                    entries[i].TableName, DbCategoryIds.Log);
            }
            return failed;
        }

        private Exception? TryWrite(IReadOnlyList<AuditEntry> entries)
        {
            try
            {
                _repository.WriteBatch(entries);
                return null;
            }
            catch (Exception ex)
            {
                // Boundary: a log-store failure must not surface into the business flow, and whatever the provider
                // throws (a refusal, a value it cannot encode or bind, a connection it cannot open) is answered the
                // same way, by the entry-by-entry retry and the file fallback. Nothing here takes a cancellation token.
                return ex;
            }
        }

        /// <summary>
        /// Appends entries to the configured fallback file (one tab-delimited line per entry) so a
        /// database outage does not lose audit records. No-op when no fallback path is configured.
        /// </summary>
        /// <param name="entries">The batch that could not reach the database.</param>
        /// <remarks>
        /// <para>
        /// WARNING: the append is serialized, and it has to be. Three paths reach this file at once —
        /// the background drain, every request thread once the queue saturates, and
        /// <see cref="SynchronousAuditLogWriter"/> in deployments that run without the background writer.
        /// <see cref="File.AppendAllText(string, string)"/> opens with <c>FileShare.Read</c>, so
        /// concurrent appends fail with <see cref="IOException"/> — and the batch that lost the race
        /// would be dropped after a single log line.
        /// </para>
        /// <para>
        /// The timing is what makes it matter: this runs only when the log database is already
        /// failing, which is exactly when the queue saturates and every request thread arrives here
        /// at once. Peak concurrency and the only path that reaches this code coincide.
        /// </para>
        /// <para>
        /// The lock is process-wide, not machine-wide. Two processes writing the same fallback path
        /// can still collide; a deployment that scales out should give each instance its own path.
        /// </para>
        /// </remarks>
        private void SpillToFile(IReadOnlyList<AuditEntry> entries)
        {
            var path = _options.FileFallbackPath;
            if (string.IsNullOrEmpty(path) || entries.Count == 0) { return; }

            var sb = new StringBuilder();
            foreach (var entry in entries)
            {
                sb.Append(entry.TableName);
                foreach (var column in entry.GetColumns())
                {
                    sb.Append('\t').Append(column.Name).Append('=')
                      .Append(Escape(Convert.ToString(column.Value, CultureInfo.InvariantCulture)));
                }
                sb.Append('\n');
            }

            try
            {
                lock (s_fileGate)
                {
                    File.AppendAllText(path, sb.ToString());
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                          or NotSupportedException or ArgumentException)
            {
                // NotSupportedException / ArgumentException cover a malformed FileFallbackPath, which
                // previously escaped this method, faulted the writer's background task and — under
                // .NET's default BackgroundServiceExceptionBehavior — stopped the host. A fallback
                // path typo must not be able to do that.
                _logger.LogError(ex, "Audit log file fallback write failed at '{Path}'.", path);
            }
        }

        /// <summary>
        /// Escapes a value for the tab-delimited fallback line: a backslash, a tab, a carriage return or a line feed
        /// becomes <c>\\</c>, <c>\t</c>, <c>\r</c> or <c>\n</c>, and every other control character, and the
        /// Unicode line and paragraph separators, becomes <c>\uXXXX</c>.
        /// </summary>
        /// <remarks>
        /// IMPORTANT: values such as a sign-in's user id come from an unauthenticated caller. Written as they are, a
        /// line feed in one started a new line that read as an audit record of its own, and terminal escape sequences
        /// or separators some log readers treat as line breaks could do the same.
        /// </remarks>
        internal static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value)) { return string.Empty; }
            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                _ = c switch
                {
                    '\\' => sb.Append(@"\\"),
                    '\t' => sb.Append(@"\t"),
                    '\r' => sb.Append(@"\r"),
                    '\n' => sb.Append(@"\n"),
                    _ when char.IsControl(c) || c is '\u2028' or '\u2029'
                        => sb.Append(CultureInfo.InvariantCulture, $@"\u{(int)c:X4}"),
                    _ => sb.Append(c),
                };
            }
            return sb.ToString();
        }
    }
}
