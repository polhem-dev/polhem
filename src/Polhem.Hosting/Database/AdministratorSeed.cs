namespace Polhem.Hosting.Database
{
    /// <summary>
    /// The first administrator, company and access grant that
    /// <see cref="IDatabaseInitializer.SeedAdministrator"/> creates.
    /// </summary>
    public sealed class AdministratorSeed
    {
        /// <summary>
        /// Gets or sets the user id the administrator signs in with.
        /// </summary>
        public string UserId { get; set; } = "admin";

        /// <summary>
        /// Gets or sets the administrator's display name; empty means the user id.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the password. The initializer stores only its hash, and the <c>db</c> commands keep it
        /// out of their output (checked by <c>DatabaseCommandLineTests</c>).
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the company id.
        /// </summary>
        public string CompanyId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the company's display name; empty means the company id.
        /// </summary>
        public string CompanyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the id of the database item that holds the company's data. Empty means the
        /// only database item in the <c>company</c> category.
        /// </summary>
        public string CompanyDatabaseId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the company's default currency code.
        /// </summary>
        public string DefaultCurrency { get; set; } = "USD";

        /// <summary>
        /// Gets or sets whether the user is created as a deployment administrator, who may manage
        /// installation-wide assets such as API keys.
        /// </summary>
        public bool DeploymentAdmin { get; set; } = true;
    }
}
