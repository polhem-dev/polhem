namespace Polhem.Db.Schema
{
    /// <summary>
    /// The state of a table's last upgrade, as <see cref="TableUpgradeRecordStore"/> records it.
    /// </summary>
    public enum TableUpgradeStatus
    {
        /// <summary>
        /// The upgrade started and has not finished: it is running, or the process stopped before it could record the
        /// outcome.
        /// </summary>
        Upgrading,

        /// <summary>
        /// The upgrade finished and the table has the recorded schema.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The upgrade failed; the record keeps the error and the schema of the last upgrade that succeeded.
        /// </summary>
        Failed,
    }
}
