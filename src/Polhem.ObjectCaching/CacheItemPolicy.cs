namespace Polhem.ObjectCaching
{
    /// <summary>
    /// Cache item expiration policy.
    /// </summary>
    public class CacheItemPolicy
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheItemPolicy"/> class.
        /// </summary>
        public CacheItemPolicy()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheItemPolicy"/> class with a time-based expiration.
        /// </summary>
        /// <param name="kind">The time type for the expiration condition. Only one of AbsoluteExpiration or SlidingExpiration can be set.</param>
        /// <param name="minutes">The number of minutes until expiration.</param>
        public CacheItemPolicy(CacheTimeKind  kind, int minutes)
        {
            if (kind == CacheTimeKind.AbsoluteTime)
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(minutes);  // Absolute time expiration
            else
                SlidingExpiration = TimeSpan.FromMinutes(minutes);  // Sliding time expiration
        }

        #endregion

        /// <summary>
        /// Gets or sets the absolute expiration time, indicating when the cache item should be evicted.
        /// </summary>
        public DateTimeOffset AbsoluteExpiration { get; set; } = DateTimeOffset.MaxValue;

        /// <summary>
        /// Gets or sets the sliding expiration duration, indicating whether cache items that have not been accessed for a period of time should be evicted.
        /// </summary>
        public TimeSpan SlidingExpiration { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Gets or sets the array of directory and file paths to monitor for changes.
        /// </summary>
        public string[]? ChangeMonitorFilePaths { get; set; } = null;

        /// <summary>
        /// Cache-notify key (<c>"{group}:{entity}"</c>) whose version bump invalidates this entry;
        /// <c>null</c> when the entry has no notification-based dependency.
        /// </summary>
        /// <remarks>
        /// The database-backed counterpart to <see cref="ChangeMonitorFilePaths"/>: a file-backed
        /// entry watches files, a database-backed entry watches the notify version that other
        /// processes bump when they write. Both are evaluated the same lazy way, on read.
        /// </remarks>
        public string? ChangeNotifyKey { get; set; } = null;

        /// <summary>
        /// Gets the notify version observed before the value was loaded, or <c>null</c> when no
        /// baseline was captured and the entry compares against the version current when it is stored.
        /// </summary>
        internal long? NotifyVersionBaseline { get; private set; }

        /// <summary>
        /// Gets the write time of each watched file observed before the value was loaded, or
        /// <c>null</c> when no baseline was captured.
        /// </summary>
        internal IReadOnlyDictionary<string, DateTime>? FileWriteTimeBaselines { get; private set; }

        /// <summary>
        /// Records the current state of <see cref="ChangeNotifyKey"/> and
        /// <see cref="ChangeMonitorFilePaths"/>, so the stored entry is compared against what was
        /// true <b>before</b> its value was loaded.
        /// </summary>
        /// <remarks>
        /// Without it the comparison point is taken when the entry is stored, after the load. A change
        /// that lands between the two — another process bumping the notify version, the file being
        /// rewritten — then becomes the entry's own baseline, and the old value it loaded looks current.
        /// </remarks>
        internal void CaptureChangeBaseline()
        {
            if (!string.IsNullOrEmpty(ChangeNotifyKey))
                NotifyVersionBaseline = CacheInfo.NotifyVersions.GetVersion(ChangeNotifyKey);

            if (ChangeMonitorFilePaths != null)
            {
                var baselines = new Dictionary<string, DateTime>(StringComparer.Ordinal);
                foreach (var path in ChangeMonitorFilePaths)
                {
                    if (!string.IsNullOrEmpty(path))
                        baselines[path] = FileWriteTime.Get(path);
                }
                FileWriteTimeBaselines = baselines;
            }
        }
    }
}
