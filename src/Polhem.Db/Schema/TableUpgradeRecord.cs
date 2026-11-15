namespace Polhem.Db.Schema
{
    /// <summary>
    /// The record of a table's last upgrade in one database, read by <see cref="TableUpgradeRecordStore.GetRecord"/>.
    /// </summary>
    public sealed class TableUpgradeRecord
    {
        /// <summary>
        /// Gets the name of the upgraded table, in lowercase.
        /// </summary>
        public required string TableName { get; init; }

        /// <summary>
        /// Gets the <see cref="SchemaFormat.Current"/> of the tool that last started an upgrade of the table.
        /// </summary>
        public int FormatVersion { get; init; }

        /// <summary>
        /// Gets the framework version of the tool that last started an upgrade of the table.
        /// </summary>
        public string FrameworkVersion { get; init; } = string.Empty;

        /// <summary>
        /// Gets the state of the last upgrade.
        /// </summary>
        public TableUpgradeStatus Status { get; init; }

        /// <summary>
        /// Gets the error of the last upgrade when it failed; otherwise an empty string.
        /// </summary>
        public string Error { get; init; } = string.Empty;

        /// <summary>
        /// Gets the table schema, as XML, that the last successful upgrade applied; an empty string when no upgrade of the
        /// table has succeeded yet.
        /// </summary>
        public string SchemaXml { get; init; } = string.Empty;

        /// <summary>
        /// Gets the SHA-256 of <see cref="SchemaXml"/> in lowercase hexadecimal; an empty string when no upgrade of the
        /// table has succeeded yet.
        /// </summary>
        public string SchemaHash { get; init; } = string.Empty;

        /// <summary>
        /// Gets when the record was last written, in UTC.
        /// </summary>
        public DateTime UpdateTime { get; init; }
    }
}
