namespace Polhem.Api.Client
{
    /// <summary>
    /// One signed-in identity as the client holds it: the access token, the transmission key
    /// exchanged with it, and the user's time zone.
    /// </summary>
    /// <remarks>
    /// The three values are replaced together, as one instance, so a reader never sees a token from
    /// one sign-in paired with the key of another. <see cref="ApiSessionContext.SignIn"/> swaps the
    /// instance, and a connector reads it once per call.
    /// </remarks>
    public sealed class ApiSessionCredentials
    {
        /// <summary>
        /// The credentials of a client that has not signed in.
        /// </summary>
        public static ApiSessionCredentials Anonymous { get; } = new(Guid.Empty, [], string.Empty);

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiSessionCredentials"/> class.
        /// </summary>
        /// <param name="accessToken">The access token; <see cref="Guid.Empty"/> before sign-in.</param>
        /// <param name="apiEncryptionKey">The transmission key; empty when none was exchanged.</param>
        /// <param name="userTimeZoneId">The user's IANA time zone id; blank disables time zone conversion.</param>
        public ApiSessionCredentials(Guid accessToken, byte[] apiEncryptionKey, string userTimeZoneId)
        {
            ArgumentNullException.ThrowIfNull(apiEncryptionKey);
            ArgumentNullException.ThrowIfNull(userTimeZoneId);
            AccessToken = accessToken;
            ApiEncryptionKey = apiEncryptionKey;
            UserTimeZoneId = userTimeZoneId;
        }

        /// <summary>
        /// Gets the access token; <see cref="Guid.Empty"/> before sign-in.
        /// </summary>
        public Guid AccessToken { get; }

        /// <summary>
        /// Gets the transmission key exchanged through the RSA handshake at sign-in; empty when none
        /// was exchanged, as on a local connection or in a browser.
        /// </summary>
        public byte[] ApiEncryptionKey { get; }

        /// <summary>
        /// Gets the signed-in user's IANA time zone id; blank disables time zone conversion.
        /// </summary>
        /// <remarks>
        /// Blank is the correct state before sign-in: there is no user whose zone could apply, and
        /// adopting the device's would reintroduce the second source of truth ADR-032 D4 rejects.
        /// </remarks>
        public string UserTimeZoneId { get; }
    }
}
