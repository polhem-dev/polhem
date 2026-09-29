namespace Polhem.Core.Exceptions
{
    /// <summary>
    /// Represents a user-facing message produced by business logic, intended to be
    /// shown to the end user (for example: validation failure, business-rule
    /// violation, or workflow interruption).
    /// </summary>
    /// <remarks>
    /// Conceptually this is a "business flow interruption signal" rather than a
    /// genuine program error: control flow is aborted because the operation cannot
    /// be completed, and the message is meant to reach the user as-is. The C# layer
    /// still throws (matching .NET conventions for flow control), and the JSON-RPC
    /// transport layer surfaces it via <c>UserMessage</c>.
    ///
    /// <para>
    /// Use this type, not a BCL exception (<see cref="InvalidOperationException"/>,
    /// <see cref="ArgumentException"/>, etc.), for any message that is meant to be
    /// surfaced to end users: a remote caller receives a BCL exception only as a fixed,
    /// generic message.
    /// </para>
    /// </remarks>
    public class UserMessageException : Exception, ILocalizableMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserMessageException"/> class
        /// with the specified user-facing message.
        /// </summary>
        /// <param name="message">The message to display to the end user.</param>
        public UserMessageException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserMessageException"/> class
        /// with the specified user-facing message and a reference to the underlying
        /// cause.
        /// </summary>
        /// <param name="message">The message to display to the end user.</param>
        /// <param name="innerException">The exception that caused this failure.</param>
        public UserMessageException(string message, Exception innerException)
            : base(message, innerException) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserMessageException"/> class with a translatable
        /// message: a language key, its English composite format and the format's arguments.
        /// </summary>
        /// <param name="messageKey">The full language key, <c>"{namespace}.{subKey}"</c>.</param>
        /// <param name="defaultMessage">
        /// The English composite format. Formatted with <paramref name="arguments"/> under the
        /// invariant culture, it becomes <see cref="Exception.Message"/>, which is also the text a
        /// user sees when their culture has no translation.
        /// </param>
        /// <param name="arguments">The values the format places.</param>
        public UserMessageException(string messageKey, string defaultMessage, params object?[] arguments)
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
