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
    public sealed class AuthenticationRequiredException : UnauthorizedAccessException, ILocalizableMessage
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

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationRequiredException"/> class with a translatable
        /// message: a language key, its English composite format and the format's arguments.
        /// </summary>
        /// <param name="messageKey">The full language key, <c>"{namespace}.{subKey}"</c>.</param>
        /// <param name="defaultMessage">
        /// The English composite format. Formatted with <paramref name="arguments"/> under the
        /// invariant culture, it becomes <see cref="Exception.Message"/>, which is also the text a
        /// user sees when their culture has no translation.
        /// </param>
        /// <param name="arguments">The values the format places.</param>
        public AuthenticationRequiredException(string messageKey, string defaultMessage, params object?[] arguments)
            : base(LocalizableMessageFormat.Format(defaultMessage, arguments))
        {
            MessageKey = LocalizableMessageFormat.RequireKey(messageKey);
            MessageArguments = arguments ?? [];
        }

        /// <inheritdoc/>
        public string MessageKey { get; } = string.Empty;

        /// <inheritdoc/>
        public IReadOnlyList<object?> MessageArguments { get; } = [];
    }
}
