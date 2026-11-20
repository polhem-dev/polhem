using System.Globalization;

namespace Polhem.Api.Server.Dispatch
{
    /// <summary>
    /// Reads the language a caller asks its messages in from the <c>Accept-Language</c> header.
    /// </summary>
    /// <remarks>
    /// The value only picks a translation for a call without a session, so a header that cannot be read is ignored
    /// rather than refused.
    /// </remarks>
    internal static class RequestCultureParser
    {
        // A language tag such as `zh-Hant-TW` is far shorter. The limit keeps a hostile header from reaching the
        // culture lookup at all.
        private const int MaxTagLength = 35;

        /// <summary>
        /// Returns the name of the first culture in <paramref name="header"/> this runtime knows, or an empty string.
        /// </summary>
        /// <param name="header">The header value, such as <c>en-US,en;q=0.9</c>.</param>
        public static string FromAcceptLanguage(string? header)
        {
            if (string.IsNullOrWhiteSpace(header)) return string.Empty;

            // The first entry is the caller's preferred language; quality values only order the rest.
            var first = header.Split(',', 2)[0];
            var tag = first.Split(';', 2)[0].Trim();
            return Normalize(tag);
        }

        /// <summary>
        /// Returns the name of <paramref name="name"/> when it is a specific culture this runtime knows, or an empty
        /// string.
        /// </summary>
        /// <param name="name">A culture name.</param>
        public static string Normalize(string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > MaxTagLength || name == "*") return string.Empty;

            try
            {
                return CultureInfo.GetCultureInfo(name, predefinedOnly: true).Name;
            }
            catch (CultureNotFoundException)
            {
                return string.Empty;
            }
        }
    }
}
