using Polhem.Definition.Security;

namespace Polhem.Business.Providers
{
    /// <summary>
    /// Static encryption key provider that always returns the shared key supplied at construction time.
    /// </summary>
    /// <remarks>
    /// Every client holds the same key, so with the wire frame on, a method that declares
    /// <see cref="ApiReplayProtection.UniqueSequence"/> does not stop a call captured in one session from running
    /// again in another. Sequence numbers are remembered per session, and the HMAC proves only that the frame was
    /// written with the shared key: whoever can present another session replays the captured call there once, without
    /// holding the key, and a client that holds it can also seal a captured call again with a fresh frame. One scope
    /// for the shared key would not work either, because the built-in replay store accepts a number only within a
    /// window below the highest it has seen, and clients that count on their own would refuse each other's calls. Use
    /// <see cref="DerivedApiEncryptionKeyProvider"/>, the default, when replay protection matters.
    /// </remarks>
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
