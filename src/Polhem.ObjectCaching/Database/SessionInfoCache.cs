using Polhem.Definition;
using Polhem.Definition.Identity;

namespace Polhem.ObjectCaching.Database
{
    /// <summary>
    /// Session information cache.
    /// </summary>
    public sealed class SessionInfoCache : KeyObjectCache<SessionInfo>
    {
        private readonly Func<ICacheDataSourceProvider>? _dataSource;

        /// <summary>
        /// Initializes a new <see cref="SessionInfoCache"/> with no data source, so every miss
        /// stays a miss.
        /// </summary>
        /// <param name="cachePrefix">Per-owner cache namespace (see <see cref="KeyObjectCache{T}"/>).</param>
        public SessionInfoCache(string cachePrefix = "") : this(null, cachePrefix) { }

        /// <summary>
        /// Initializes a new <see cref="SessionInfoCache"/> that rebuilds sessions through the
        /// supplied data source.
        /// </summary>
        /// <param name="dataSource">
        /// Factory resolving the data source on the first miss. WARNING: it must stay a factory —
        /// resolving the provider while this container is still under construction closes a
        /// dependency cycle back onto the container.
        /// </param>
        /// <param name="cachePrefix">Per-owner cache namespace (see <see cref="KeyObjectCache{T}"/>).</param>
        internal SessionInfoCache(Func<ICacheDataSourceProvider>? dataSource, string cachePrefix)
            : base(cachePrefix)
        {
            _dataSource = dataSource;
        }

        /// <summary>
        /// Rebuilds the session from its persisted seed on a cache miss.
        /// </summary>
        /// <param name="key">The access token.</param>
        /// <remarks>
        /// WARNING: this makes every writer of <c>st_session</c> a way to mint a token that
        /// satisfies <c>AccessTokenValidator</c> — before this existed, only sign-in could put a
        /// session in the cache, and rows in the table were inert. Any new writer must therefore
        /// authenticate for itself or be confined to trusted callers, which is why
        /// <c>SystemBO.CreateSession</c> (a token from a user id alone) is <c>LocalOnly</c>.
        ///
        /// The rebuild returns <c>null</c> for anything that is not a live session — no seed, an
        /// expired seed, revoked company access, or a key provider that cannot recover the session
        /// key — so an unknown token still fails to authenticate.
        /// </remarks>
        protected override SessionInfo? CreateInstance(string key)
        {
            return Guid.TryParse(key, out var accessToken)
                ? _dataSource?.Invoke().GetSessionInfo(accessToken)
                : null;
        }

        /// <summary>
        /// Lifetime, in minutes, of a cached miss for an unknown access token.
        /// </summary>
        public const int NegativeMinutes = 1;

        /// <summary>
        /// Upper bound on the number of unknown access tokens remembered at once.
        /// </summary>
        public const int MaxNegativeTokens = 10_000;

        /// <summary>
        /// Caches a miss for one minute, in a set capped at <see cref="MaxNegativeTokens"/>.
        /// </summary>
        /// <remarks>
        /// Every request resolves its session more than once before its access is decided: the
        /// business-object factory reads the customization code, then the token validator checks the
        /// session. Without a miss marker an unknown token paid one <c>st_session</c> read for each,
        /// and again on every repeat. With it, a distinct unknown token costs one read and a repeated
        /// one costs none. A stream of distinct random tokens still reaches the database once per
        /// token; the marker cannot help there, and nothing short of a rate limit at the edge does.
        /// <para>
        /// The markers live in a capped set rather than in the shared cache provider (see
        /// <see cref="MaxNegativeEntries"/>), because the key is whatever the caller put in its
        /// <c>Authorization</c> header. Signing in writes the new session with <c>Set</c>, which
        /// clears any marker for that token, and a token cannot be probed before sign-in issues it.
        /// </para>
        /// </remarks>
        /// <param name="key">The access token (unused).</param>
        protected override CacheItemPolicy? GetNegativePolicy(string key)
            => new CacheItemPolicy(CacheTimeKind.AbsoluteTime, NegativeMinutes);

        /// <inheritdoc/>
        protected override int MaxNegativeEntries => MaxNegativeTokens;

        /// <summary>
        /// Gets the session information for the specified access token.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        public SessionInfo? Get(Guid accessToken)
        {
            return Get(accessToken.ToString());
        }

        /// <summary>
        /// Removes the session information from the cache.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        public void Remove(Guid accessToken)
        {
            Remove(accessToken.ToString());
        }
    }
}
