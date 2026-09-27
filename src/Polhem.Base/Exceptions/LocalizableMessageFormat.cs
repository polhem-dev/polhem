using System.Globalization;

namespace Polhem.Base.Exceptions
{
    /// <summary>
    /// Formats the English text of an <see cref="ILocalizableMessage"/> exception.
    /// </summary>
    internal static class LocalizableMessageFormat
    {
        /// <summary>
        /// Formats <paramref name="format"/> with <paramref name="arguments"/> under the invariant
        /// culture; a format with no arguments is returned as it is, so literal braces survive.
        /// </summary>
        internal static string Format(string format, object?[]? arguments)
        {
            ArgumentNullException.ThrowIfNull(format);
            return arguments is { Length: > 0 }
                ? string.Format(CultureInfo.InvariantCulture, format, arguments)
                : format;
        }

        /// <summary>
        /// Validates a message key: a keyed constructor needs a non-empty one.
        /// </summary>
        internal static string RequireKey(string messageKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(messageKey);
            return messageKey;
        }
    }
}
