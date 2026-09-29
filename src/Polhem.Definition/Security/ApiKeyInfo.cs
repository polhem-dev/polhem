using Polhem.Base;

namespace Polhem.Definition.Security
{
    /// <summary>
    /// One API key record from <c>st_api_key</c>, keyed by its <c>sys_id</c>.
    /// </summary>
    /// <remarks>
    /// Carries the hashed secret, never the plaintext key: the framework cannot recover a key once
    /// issued (see <see cref="Polhem.Base.Security.ApiKeyHasher"/>). Disabled rows are excluded by the repository, so an
    /// instance of this type always represents an enabled key — the same treatment
    /// <c>CompanyRepository</c> gives disabled companies, and the reason there is no
    /// <c>Enabled</c> property here.
    /// <para>
    /// <see cref="ExpiredAt"/> is checked on every validation rather than through cache expiry, so
    /// a key stops working the moment it expires regardless of how long it stays cached.
    /// </para>
    /// <para>
    /// This is a cache-shared instance: every caller presenting the same key receives the same
    /// reference, and this record decides whether the call is authenticated. Its properties are
    /// init-only, so the compiler rejects a change after it is loaded. See
    /// <c>docs/en/architecture/development-constraints.md</c> § <i>Cached Data Immutability After Init</i>.
    /// </para>
    /// </remarks>
    public sealed class ApiKeyInfo : IKeyObject
    {
        /// <summary>
        /// Gets the key identifier (<c>sys_id</c>), which is also the leading segment of the
        /// plaintext key. Not a secret: it appears in logs and audit records by design.
        /// </summary>
        public string SysId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the display name of the calling application (for example
        /// <c>"Northwind Desktop"</c>).
        /// </summary>
        public string SysName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the hashed secret segment, in the <c>v1.{salt}.{hash}</c> form produced by
        /// <see cref="Polhem.Base.Security.ApiKeyHasher.HashSecret"/>.
        /// </summary>
        public string HashedKey { get; init; } = string.Empty;

        /// <summary>
        /// Gets the key classification (internal application or third party). A label for
        /// operators; it carries no authorization meaning.
        /// </summary>
        public ApiKeyType KeyType { get; init; } = ApiKeyType.Internal;

        /// <summary>
        /// Gets the contact for the third party holding this key, so an incident has
        /// someone to reach.
        /// </summary>
        public string Contact { get; init; } = string.Empty;

        /// <summary>
        /// Gets the UTC expiry, or <c>null</c> when the key does not expire.
        /// </summary>
        public DateTime? ExpiredAt { get; init; }

        /// <summary>
        /// Gets the cache key (the <see cref="SysId"/>).
        /// </summary>
        public string GetKey()
        {
            return this.SysId;
        }

        /// <summary>
        /// Returns whether the key has passed its <see cref="ExpiredAt"/> at the supplied UTC time.
        /// </summary>
        /// <param name="utcNow">The current UTC time.</param>
        public bool IsExpired(DateTime utcNow)
        {
            return ExpiredAt.HasValue && ExpiredAt.Value <= utcNow;
        }
    }
}
