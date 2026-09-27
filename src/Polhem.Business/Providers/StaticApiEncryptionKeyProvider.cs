using Polhem.Definition.Security;

namespace Polhem.Business.Providers
{
    /// <summary>
    /// Static encryption key provider that always returns the shared key supplied at construction time.
    /// </summary>
    public sealed class StaticApiEncryptionKeyProvider : IApiEncryptionKeyProvider
    {
        private readonly byte[] _apiEncryptionKey;

        /// <summary>
        /// Initializes a new <see cref="StaticApiEncryptionKeyProvider"/>.
        /// </summary>
        /// <param name="apiEncryptionKey">The shared API encryption key (64-byte combined AES + HMAC).</param>
        public StaticApiEncryptionKeyProvider(byte[] apiEncryptionKey)
        {
            ArgumentNullException.ThrowIfNull(apiEncryptionKey);
            if (apiEncryptionKey.Length == 0)
                throw new ArgumentException("ApiEncryptionKey cannot be empty.", nameof(apiEncryptionKey));
            _apiEncryptionKey = apiEncryptionKey;
        }

        /// <summary>
        /// Gets the encryption key for API transmission data.
        /// </summary>
        /// <param name="accessToken">The access token, or <see cref="Guid.Empty"/>.</param>
        /// <returns>A 64-byte combined key (AES + HMAC).</returns>
        public byte[] GetKey(Guid accessToken) => _apiEncryptionKey;

        /// <summary>
        /// Generates the key for a newly issued session; always the shared key.
        /// </summary>
        /// <param name="accessToken">The access token of the session being created (unused).</param>
        /// <returns>A 64-byte combined key (AES + HMAC).</returns>
        public byte[] GenerateKeyForLogin(Guid accessToken) => _apiEncryptionKey;

        /// <summary>
        /// Always <c>true</c>: the shared key does not depend on the session.
        /// </summary>
        public bool SupportsSessionRebuild => true;
    }

}
