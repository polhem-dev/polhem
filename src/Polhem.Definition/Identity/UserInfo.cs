namespace Polhem.Definition.Identity
{
    /// <summary>
    /// Frontend user information.
    /// </summary>
    public sealed class UserInfo : IUserInfo
    {
        /// <summary>
        /// Gets or sets the user account ID.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user name.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user culture (e.g., zh-TW, en-US). An empty value means the client
        /// keeps its own UI culture.
        /// </summary>
        /// <remarks>
        /// Empty by default, matching <see cref="SessionInfo.Culture"/>: the effective culture is
        /// supplied by the server at login, from <c>st_user.culture</c> or the deployment's default
        /// language.
        /// </remarks>
        public string Culture { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user time zone (IANA format recommended, e.g., Asia/Taipei).
        /// An empty value means UTC.
        /// </summary>
        /// <remarks>
        /// Empty by default, matching <see cref="SessionInfo.TimeZone"/>: the effective zone is supplied
        /// by the server at login, and the conversion layer already treats a blank zone as UTC.
        /// </remarks>
        public string TimeZone { get; set; } = string.Empty;
    }

}
