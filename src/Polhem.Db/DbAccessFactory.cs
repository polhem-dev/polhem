using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Definition.Logging;

namespace Polhem.Db
{
    /// <summary>
    /// Default <see cref="IDbAccessFactory"/> implementation. Holds the per-app
    /// <see cref="System.Data.Common.DbCommand.CommandTimeout"/> cap and propagates it to each
    /// <see cref="DbAccess"/> instance it creates.
    /// </summary>
    /// <remarks>
    /// Typical host registration (per-app cap differs by deployment):
    /// <list type="bullet">
    /// <item>Mobile API backend: 30 sec</item>
    /// <item>Web backend: 60 sec</item>
    /// <item>Scheduled batch service: 120 sec</item>
    /// </list>
    /// </remarks>
    public sealed class DbAccessFactory : IDbAccessFactory
    {
        private readonly IDbConnectionManager _connectionManager;
        private readonly int _maxCommandTimeout;
        private readonly Func<IAnomalyLogWriter?>? _anomalyWriterFactory;
        private readonly DbAccessAnomalyLogOptions? _anomalyOptions;

        /// <summary>
        /// Initializes a new <see cref="DbAccessFactory"/> with no command timeout cap and no DB
        /// anomaly logging.
        /// </summary>
        /// <param name="connectionManager">The DI-resolved connection manager.</param>
        public DbAccessFactory(IDbConnectionManager connectionManager)
            : this(connectionManager, maxCommandTimeout: 0, anomalyWriterFactory: null, anomalyOptions: null)
        {
        }

        /// <summary>
        /// Initializes a new <see cref="DbAccessFactory"/>.
        /// </summary>
        /// <param name="connectionManager">The DI-resolved connection manager.</param>
        /// <param name="maxCommandTimeout">
        /// Per-app upper bound applied to each <see cref="System.Data.Common.DbCommand.CommandTimeout"/>;
        /// 0 disables the cap.
        /// </param>
        /// <param name="anomalyWriterFactory">
        /// Optional lazy resolver for the DB-anomaly audit writer; null disables DB anomaly logging.
        /// Lazy (a factory, not the instance) to break the construction cycle
        /// <c>IDbAccessFactory → IAnomalyLogWriter → AuditLogDbSink → IDbAccessFactory</c>.
        /// </param>
        /// <param name="anomalyOptions">DB anomaly thresholds / level, or null.</param>
        public DbAccessFactory(IDbConnectionManager connectionManager, int maxCommandTimeout,
            Func<IAnomalyLogWriter?>? anomalyWriterFactory, DbAccessAnomalyLogOptions? anomalyOptions)
        {
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _maxCommandTimeout = maxCommandTimeout;
            _anomalyWriterFactory = anomalyWriterFactory;
            _anomalyOptions = anomalyOptions;
        }

        /// <inheritdoc/>
        public DbAccess Create(string databaseId)
        {
            // The log database's own DbAccess must not anomaly-log — an anomaly write goes through
            // DbAccess against the log DB again, which would recurse. Everything else gets detection.
            bool logSelf = string.Equals(databaseId, DbCategoryIds.Log, StringComparison.Ordinal);
            if (logSelf)
                return new DbAccess(databaseId, _connectionManager, _maxCommandTimeout, anomalyWriter: null, anomalyOptions: null);

            return new DbAccess(databaseId, _connectionManager, _maxCommandTimeout,
                _anomalyWriterFactory?.Invoke(), _anomalyOptions);
        }
    }
}
