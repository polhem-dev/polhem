namespace Polhem.Api.Contracts.System
{
    /// <summary>
    /// Contract interface for create session request parameters.
    /// </summary>
    public interface ICreateSessionRequest
    {
        /// <summary>
        /// Gets the user identifier.
        /// </summary>
        string UserId { get; }

        /// <summary>
        /// Gets the session expiration time in seconds.
        /// </summary>
        int ExpiresIn { get; }
    }
}
