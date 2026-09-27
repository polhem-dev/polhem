using System.Collections.Concurrent;

namespace Polhem.ObjectCaching
{
    /// <summary>
    /// Answers "does this tenant supply an override file" for the customization layer, remembering a
    /// "no" for <see cref="FileWriteTime.RecheckInterval"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="CustomizeDefineReader"/> probes for an override file before it touches the cache,
    /// and it is consulted on every request of a tenant that has a customization code. Most tenants
    /// override nothing, so without this every such request paid a <see cref="File.Exists(string)"/>
    /// per customizable type.
    /// </para>
    /// <para>
    /// Only the absence is remembered. A file that exists is probed each time, so a deleted override
    /// stops being served on the next read rather than after the interval. <see cref="CustomizeDefineWriter"/>
    /// calls <see cref="Forget"/> after it writes, so its save is visible to the next read; a file copied
    /// in by other means can take up to the interval to be noticed.
    /// </para>
    /// <para>
    /// Process-wide because the reader and the writer are separate services that must see the same
    /// answer. The keys are override file paths, one per tenant and customizable type.
    /// </para>
    /// </remarks>
    internal static class OverrideFileProbe
    {
        private static readonly ConcurrentDictionary<string, long> s_absentUntil = new(StringComparer.Ordinal);

        /// <summary>
        /// Returns whether the override file exists.
        /// </summary>
        /// <param name="path">The override file path.</param>
        public static bool Exists(string path)
        {
            long now = Environment.TickCount64;
            if (s_absentUntil.TryGetValue(path, out long until) && now < until)
                return false;

            if (File.Exists(path))
            {
                s_absentUntil.TryRemove(path, out _);
                return true;
            }

            s_absentUntil[path] = now + (long)FileWriteTime.RecheckInterval.TotalMilliseconds;
            return false;
        }

        /// <summary>
        /// Drops a remembered absence, so the next <see cref="Exists"/> looks at the file again.
        /// </summary>
        /// <param name="path">The override file path that was just written.</param>
        public static void Forget(string path) => s_absentUntil.TryRemove(path, out _);
    }
}
