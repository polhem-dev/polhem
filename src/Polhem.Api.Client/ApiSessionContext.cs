namespace Polhem.Api.Client
{
    /// <summary>
    /// The signed-in state of one <see cref="PolhemApiClient"/>: its credentials and the sequence
    /// numbers of its replay frames.
    /// </summary>
    /// <remarks>
    /// Every connector of a client reads the same instance, so signing in through
    /// <see cref="PolhemApiClient.System"/> is what every other connector of that client then calls
    /// with. A host that serves several users from one process gives each user a client of their own,
    /// and with it a session of their own.
    /// </remarks>
    public sealed class ApiSessionContext
    {
        private ApiSessionCredentials _credentials = ApiSessionCredentials.Anonymous;
        private long _sequence;

        /// <summary>
        /// Gets the current credentials; <see cref="ApiSessionCredentials.Anonymous"/> before sign-in
        /// and after <see cref="SignOut"/>.
        /// </summary>
        public ApiSessionCredentials Credentials => Volatile.Read(ref _credentials);

        /// <summary>
        /// Replaces the credentials with those of a new sign-in.
        /// </summary>
        /// <param name="credentials">The new credentials.</param>
        public void SignIn(ApiSessionCredentials credentials)
        {
            ArgumentNullException.ThrowIfNull(credentials);
            Volatile.Write(ref _credentials, credentials);
        }

        /// <summary>
        /// Returns the session to <see cref="ApiSessionCredentials.Anonymous"/>.
        /// </summary>
        public void SignOut() => Volatile.Write(ref _credentials, ApiSessionCredentials.Anonymous);

        /// <summary>
        /// Hands out the next sequence number for this session's replay frames.
        /// </summary>
        /// <returns>A number this session has not returned before.</returns>
        /// <remarks>
        /// The counter belongs here rather than on a connector because the server's window is keyed
        /// by session: an application holds several connectors at once and they share one session,
        /// so a per-connector counter would issue the same numbers twice and the second connector's
        /// requests would be refused as replays.
        /// <para>
        /// The counter is not reset by a new sign-in. The server's window is per access token, so a
        /// new token starts with an empty window and any number is new to it. Starting from zero on a
        /// fresh instance is safe for the same reason: the key lives only in memory, so a restarted
        /// client signs in again and receives a new token.
        /// </para>
        /// </remarks>
        public long NextSequence() => Interlocked.Increment(ref _sequence);
    }
}
