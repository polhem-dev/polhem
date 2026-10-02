namespace Polhem.Core.Exceptions
{
    /// <summary>
    /// An exception whose user-facing message can be translated: it carries a language key and the
    /// arguments of its message besides the English text in <see cref="Exception.Message"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The framework's user-facing exceptions implement this. The JSON-RPC server resolves
    /// <see cref="MessageKey"/> in the session's culture before the message leaves the server,
    /// formats the translation with <see cref="MessageArguments"/>, and falls back to
    /// <see cref="Exception.Message"/> when no culture of the fall-back chain translates the key.
    /// Logs and anything else on the server keep reading the English <see cref="Exception.Message"/>.
    /// </para>
    /// <para>
    /// The key is a full language key, <c>"{namespace}.{subKey}"</c>, so it can name any language
    /// namespace: the framework's own messages live in <c>PolhemMessages</c>, and a form rule's
    /// message in the form's own namespace.
    /// </para>
    /// </remarks>
    public interface ILocalizableMessage
    {
        /// <summary>
        /// Gets the full language key of the message; empty when the message is literal text with
        /// nothing to translate.
        /// </summary>
        string MessageKey { get; }

        /// <summary>
        /// Gets the values the message's composite format places, in order.
        /// </summary>
        IReadOnlyList<object?> MessageArguments { get; }
    }
}
