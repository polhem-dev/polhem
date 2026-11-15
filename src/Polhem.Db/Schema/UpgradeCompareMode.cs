namespace Polhem.Db.Schema
{
    /// <summary>
    /// How an upgrade decides whether a table differs from its definition.
    /// </summary>
    public enum UpgradeCompareMode
    {
        /// <summary>
        /// Takes a table as unchanged without reading the database's catalog when its record in
        /// <see cref="TableUpgradeRecordStore"/> says the last upgrade succeeded, with this
        /// <see cref="SchemaFormat"/> and the definition's current schema; otherwise compares as
        /// <see cref="Full"/> does.
        /// </summary>
        /// <remarks>
        /// A change made to the table outside the framework (a column or a table dropped by hand) is not seen until
        /// the definition changes or a <see cref="Full"/> compare runs.
        /// </remarks>
        Fast,

        /// <summary>
        /// Reads the table's actual schema from the database's catalog and compares it with the definition.
        /// </summary>
        Full,
    }
}
