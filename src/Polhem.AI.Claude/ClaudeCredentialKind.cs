namespace Polhem.AI.Claude
{
    /// <summary>
    /// Where the credential the Anthropic SDK will use comes from.
    /// </summary>
    public enum ClaudeCredentialKind
    {
        /// <summary>No credential was found; requests will fail until the user signs in or sets an API key.</summary>
        None,

        /// <summary>The <c>ANTHROPIC_API_KEY</c> environment variable.</summary>
        ApiKey,

        /// <summary>The <c>ANTHROPIC_AUTH_TOKEN</c> environment variable.</summary>
        AuthToken,

        /// <summary>A profile saved by <c>ant auth login</c>.</summary>
        Profile,

        /// <summary>Workload identity federation configured through environment variables.</summary>
        WorkloadIdentity,
    }
}
