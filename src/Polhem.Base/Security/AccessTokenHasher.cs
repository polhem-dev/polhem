using System.Security.Cryptography;

namespace Polhem.Base.Security
{
    /// <summary>
    /// Derives the non-reversible forms of an access token that the framework persists in place of
    /// the token itself: the session storage key and the log fingerprint.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: an access token is a bearer credential. Anyone who reads one can act as that session,
    /// and with the deriving key provider it is also the only per-session input to the payload key. So
    /// the raw value is never written to a table: <c>st_session</c> is keyed by
    /// <see cref="ComputeStorageKey"/>, and the log tables carry <see cref="ComputeFingerprint"/>.
    /// <para>
    /// Both are cut from one digest, SHA-256 over the token's 16 bytes in RFC 4122 (big-endian) order.
    /// The byte order is fixed rather than the platform's <see cref="Guid.ToByteArray()"/> layout, so a
    /// tool in another language computes the same values from the token's canonical form.
    /// </para>
    /// <para>
    /// A plain hash is enough here, unlike for passwords. The token is 122 bits of randomness chosen by
    /// the server, so there is nothing to guess offline, and a lookup key has to be deterministic anyway.
    /// </para>
    /// <para>
    /// The fingerprint is the first half of the storage key's digest. A log row can therefore be
    /// correlated with its <c>st_session</c> row by prefix (the first 16 hex digits of the storage key
    /// in <c>"N"</c> format), without either one revealing the token.
    /// </para>
    /// </remarks>
    public static class AccessTokenHasher
    {
        private const int StorageKeySize = 16;
        private const int FingerprintSize = 8;

        /// <summary>
        /// Computes the key under which a session is stored: the first 16 bytes of the token's digest,
        /// read as a big-endian <see cref="Guid"/> so the storage column keeps its type.
        /// </summary>
        /// <param name="accessToken">The access token presented by the client.</param>
        /// <returns>The storage key. Deterministic: the same token always yields the same key.</returns>
        public static Guid ComputeStorageKey(Guid accessToken)
        {
            Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
            ComputeDigest(accessToken, digest);
            return new Guid(digest[..StorageKeySize], bigEndian: true);
        }

        /// <summary>
        /// Computes the fingerprint written to the audit and anomaly logs: the first 8 bytes of the
        /// token's digest as 16 lowercase hex digits.
        /// </summary>
        /// <param name="accessToken">The access token of the session the event belongs to.</param>
        /// <returns>
        /// The fingerprint, or <c>null</c> for <see cref="Guid.Empty"/>, which stands for "no session"
        /// and must read as such in a log row rather than as the fingerprint of an all-zero token.
        /// </returns>
        public static string? ComputeFingerprint(Guid accessToken)
        {
            if (accessToken == Guid.Empty) { return null; }

            Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
            ComputeDigest(accessToken, digest);
            return Convert.ToHexStringLower(digest[..FingerprintSize]);
        }

        private static void ComputeDigest(Guid accessToken, Span<byte> destination)
        {
            Span<byte> tokenBytes = stackalloc byte[16];
            accessToken.TryWriteBytes(tokenBytes, bigEndian: true, out _);
            SHA256.HashData(tokenBytes, destination);
        }
    }
}
