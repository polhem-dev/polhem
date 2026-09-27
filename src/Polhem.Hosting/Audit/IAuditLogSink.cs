using Polhem.Definition.Logging;

namespace Polhem.Hosting.Audit
{
    /// <summary>
    /// Terminal writer that persists audit and anomaly entries. Abstracted from the writers
    /// (background / synchronous) so the durability target can be replaced without touching the
    /// queueing logic.
    /// </summary>
    /// <remarks>
    /// The framework registers a sink that writes to the log database and falls back to a file. A
    /// deployment that ships its records elsewhere (a SIEM, a message bus) registers its own
    /// implementation in the service collection; the framework's registration is a <c>TryAdd</c>, so
    /// the deployment's wins whether it is added before or after <c>AddPolhemFramework</c>.
    /// <para>
    /// The background writer catches whatever a sink throws, logs it and drops that batch, so a
    /// broken sink loses records rather than stopping the host (pinned by <c>AuditLogWriterServiceTests</c>).
    /// The synchronous writer calls the sink
    /// on the request thread, so an exception there reaches the business operation that logged.
    /// </para>
    /// </remarks>
    public interface IAuditLogSink
    {
        /// <summary>
        /// Persists a batch of entries. Implementations must not throw for expected persistence
        /// failures — a failed write is logged and (optionally) spilled to a file, so a log-store
        /// outage never propagates into the business flow.
        /// </summary>
        /// <param name="entries">The entries to persist.</param>
        void WriteBatch(IReadOnlyList<AuditEntry> entries);
    }
}
