namespace Polhem.Core.Exceptions
{
    /// <summary>
    /// Thrown when a caller cannot enter the requested company — because the company does not
    /// exist, is disabled, or the user has not been granted access to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three causes are deliberately merged into one exception carrying one message, so that
    /// error text cannot be used to enumerate valid company identifiers. The JSON-RPC transport
    /// surfaces this via <c>CompanyAccessDenied</c> (HTTP 403 Forbidden
    /// semantics); the client reconstructs it from that code so callers can
    /// <c>catch (CompanyAccessDeniedException)</c> and route the user back to company selection.
    /// </para>
    /// <para>
    /// Distinct from <see cref="ForbiddenException"/>, which is the per-model+action check inside a
    /// company the caller has already entered.
    /// </para>
    /// </remarks>
    public sealed class CompanyAccessDeniedException : Exception, ILocalizableMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CompanyAccessDeniedException"/> class
        /// with the specified message.
        /// </summary>
        /// <param name="message">
        /// The message. Keep it identical for every cause — a message that distinguishes
        /// "no such company" from "not granted" reopens the enumeration channel this type exists
        /// to close.
        /// </param>
        public CompanyAccessDeniedException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompanyAccessDeniedException"/> class
        /// with the specified message and a reference to the underlying cause.
        /// </summary>
        /// <param name="message">The message; see the single-argument overload on wording.</param>
        /// <param name="innerException">The exception that caused this failure.</param>
        public CompanyAccessDeniedException(string message, Exception innerException)
            : base(message, innerException) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompanyAccessDeniedException"/> class with a translatable
        /// message: a language key, its English composite format and the format's arguments.
        /// </summary>
        /// <param name="messageKey">The full language key, <c>"{namespace}.{subKey}"</c>.</param>
        /// <param name="defaultMessage">
        /// The English composite format. Formatted with <paramref name="arguments"/> under the
        /// invariant culture, it becomes <see cref="Exception.Message"/>, which is also the text a
        /// user sees when their culture has no translation.
        /// </param>
        /// <param name="arguments">The values the format places.</param>
        public CompanyAccessDeniedException(string messageKey, string defaultMessage, params object?[] arguments)
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
