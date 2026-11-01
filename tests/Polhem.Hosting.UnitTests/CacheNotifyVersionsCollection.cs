namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// Serializes the test classes that run a <c>CacheNotifyPollSession</c> against a real
    /// <c>st_cache_notify</c> table.
    /// </summary>
    /// <remarks>
    /// Every session publishes to the process-wide <c>CacheInfo.NotifyVersions</c> and reads all recent rows of the
    /// table, not only its own test's keys. A session that read a key at version 1 can therefore publish after another
    /// test's session published version 2. The version moves back to the value the cached entry was loaded at, so the
    /// entry looks current and the test reads the old value.
    /// </remarks>
    [CollectionDefinition(Name)]
    public static class CacheNotifyVersionsCollection
    {
        /// <summary>The collection name.</summary>
        public const string Name = "CacheNotifyVersions";
    }
}
