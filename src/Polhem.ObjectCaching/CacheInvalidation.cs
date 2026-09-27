namespace Polhem.ObjectCaching
{
    /// <summary>
    /// Process-wide invalidation generations for cache keys, so a fill that started before an
    /// invalidation does not store what it loaded after the invalidation has run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The race this closes: a fill misses and reads the old data; a writer commits and calls
    /// <c>Remove</c>, which finds nothing to remove because the fill has not stored yet; the fill then
    /// stores the old data, and the entry looks current until it expires. With sliding expiry a hot
    /// key never expires. A fill therefore reads the generation before loading and only keeps its
    /// store if the generation is unchanged afterwards; every <c>Remove</c> and explicit <c>Set</c>
    /// advances it first.
    /// </para>
    /// <para>
    /// Generations are striped by key hash rather than kept per key. Keys include caller-supplied
    /// values such as access tokens, so a per-key table would grow without bound. A collision only
    /// makes an unrelated fill skip its store once, which costs one extra load and nothing else.
    /// </para>
    /// <para>
    /// Process-wide like <see cref="CacheInfo.Provider"/>, whose entries it guards.
    /// </para>
    /// </remarks>
    internal static class CacheInvalidation
    {
        private const int StripeCount = 1024;
        private static readonly long[] s_generations = new long[StripeCount];

        private static int StripeOf(string cacheKey)
            => (StringComparer.OrdinalIgnoreCase.GetHashCode(cacheKey) & int.MaxValue) % StripeCount;

        /// <summary>
        /// Reads the current generation of <paramref name="cacheKey"/>.
        /// </summary>
        /// <param name="cacheKey">The fully qualified cache key.</param>
        public static long Read(string cacheKey) => Volatile.Read(ref s_generations[StripeOf(cacheKey)]);

        /// <summary>
        /// Advances the generation of <paramref name="cacheKey"/>. Call before removing or replacing the entry.
        /// </summary>
        /// <param name="cacheKey">The fully qualified cache key.</param>
        public static void Advance(string cacheKey) => Interlocked.Increment(ref s_generations[StripeOf(cacheKey)]);

        /// <summary>
        /// Stores a freshly loaded value unless <paramref name="cacheKey"/> was invalidated since
        /// <paramref name="generation"/> was read, and returns whether the value stayed stored.
        /// </summary>
        /// <param name="cacheKey">The fully qualified cache key.</param>
        /// <param name="generation">The generation read before the value was loaded.</param>
        /// <param name="value">The loaded value.</param>
        /// <param name="policy">The entry policy, with its change baseline already captured.</param>
        /// <remarks>
        /// The check runs both before and after the store. The second one is what makes it correct:
        /// an invalidation that lands between the first check and the store has already run its
        /// <c>Remove</c>, so the store would outlive it; re-reading afterwards catches exactly that case.
        /// </remarks>
        public static bool StoreIfCurrent(string cacheKey, long generation, object value, CacheItemPolicy policy)
        {
            if (Read(cacheKey) != generation) { return false; }
            CacheInfo.Provider.Set(cacheKey, value, policy);
            if (Read(cacheKey) == generation) { return true; }
            CacheInfo.Provider.Remove(cacheKey);
            return false;
        }
    }
}
