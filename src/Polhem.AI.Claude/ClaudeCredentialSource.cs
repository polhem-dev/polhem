namespace Polhem.AI.Claude
{
    /// <summary>
    /// Describes the credential the Anthropic SDK will use, for showing to the user. It never holds a key or a token.
    /// </summary>
    public sealed class ClaudeCredentialSource
    {
        internal ClaudeCredentialSource(ClaudeCredentialKind kind, string description, IReadOnlyList<string> warnings)
        {
            Kind = kind;
            Description = description;
            Warnings = warnings;
        }

        /// <summary>
        /// Gets where the credential comes from.
        /// </summary>
        public ClaudeCredentialKind Kind { get; }

        /// <summary>
        /// Gets a one-line English description, such as <c>Signed in with ant auth login (profile: default)</c>.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the name of the profile, when <see cref="Kind"/> is <see cref="ClaudeCredentialKind.Profile"/>.
        /// </summary>
        public string? ProfileName { get; init; }

        /// <summary>
        /// Gets the workspace the profile is bound to, when its configuration names one.
        /// </summary>
        public string? WorkspaceId { get; init; }

        /// <summary>
        /// Gets the organization the profile belongs to, when its configuration names one.
        /// </summary>
        public string? OrganizationId { get; init; }

        /// <summary>
        /// Gets the API address requests go to when it is not Anthropic's public API, or <see langword="null"/>.
        /// </summary>
        public string? BaseUrl { get; init; }

        /// <summary>
        /// Gets English notes about the setup that may surprise the user, such as an environment variable that hides a
        /// signed-in profile.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <inheritdoc />
        public override string ToString() => Description;
    }
}
