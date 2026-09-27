namespace Polhem.Base.Exceptions
{
    /// <summary>
    /// Thrown when a call that needs a signed-in caller arrives without a usable access token: none
    /// was supplied, or the one supplied is unknown, invalid or expired.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Authentication only. A caller who is signed in but lacks the right to an action gets
    /// <see cref="ForbiddenException"/> instead; the two call for different responses — sign in
    /// again, as opposed to ask for access.
    /// </para>
    /// <para>
    /// The JSON-RPC transport sends it as <c>Unauthorized</c> (-32001) with its message, and the
    /// client rebuilds this type from that code. It derives from
    /// <see cref="UnauthorizedAccessException"/>, so a caller that already catches that type — on
    /// either side of the wire — keeps catching it.
    /// </para>
    /// </remarks>
    public sealed class AuthenticationRequiredException : UnauthorizedAccessException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationRequiredException"/> class.
        /// </summary>
        /// <param name="message">The message to report to the caller.</param>
        public AuthenticationRequiredException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationRequiredException"/> class with
        /// a reference to the underlying cause.
        /// </summary>
        /// <param name="message">The message to report to the caller.</param>
        /// <param name="innerException">The exception that caused this failure.</param>
        public AuthenticationRequiredException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
