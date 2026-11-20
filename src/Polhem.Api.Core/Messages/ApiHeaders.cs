
namespace Polhem.Api.Core.Messages
{
    /// <summary>
    /// API header key constants.
    /// </summary>
    public static class ApiHeaders
    {
        /// <summary>
        /// The API key header name.
        /// </summary>
        public const string ApiKey = "X-Api-Key";
        /// <summary>
        /// The Authorization header, used for authentication or authorization.
        /// </summary>
        public const string Authorization = "Authorization";
        /// <summary>
        /// The standard <c>Accept-Language</c> header. The server translates the messages of a call that has no
        /// session, such as a failed sign-in, into its first language; a call with a session uses the account's
        /// culture.
        /// </summary>
        public const string AcceptLanguage = "Accept-Language";
    }
}
