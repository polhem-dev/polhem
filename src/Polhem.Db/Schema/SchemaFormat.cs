namespace Polhem.Db.Schema
{
    /// <summary>
    /// The version of the table upgrade's behaviour: the DDL each dialect emits, the shape each provider reads back, and
    /// how a rebuild works.
    /// </summary>
    /// <remarks>
    /// The upgrade records the version in <see cref="TableUpgradeRecordStore"/> before it changes a table, and refuses
    /// to change a database that a newer version has upgraded. A tool of an older version would otherwise read the newer
    /// shape as a difference and change it back, and the two versions would undo each other on every run.
    /// The version is raised only when that behaviour changes, not with every release of the framework.
    /// </remarks>
    public static class SchemaFormat
    {
        /// <summary>
        /// Gets the version of the table upgrade's behaviour in this build of the framework.
        /// </summary>
        public static int Current { get; } = 1;
    }
}
