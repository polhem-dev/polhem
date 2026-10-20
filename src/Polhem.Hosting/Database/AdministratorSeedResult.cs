namespace Polhem.Hosting.Database
{
    /// <summary>
    /// The result of <see cref="IDatabaseInitializer.SeedAdministrator"/>. A <c>false</c> flag means
    /// the row already existed and was left unchanged.
    /// </summary>
    public sealed class AdministratorSeedResult
    {
        /// <summary>
        /// Gets whether the user was created.
        /// </summary>
        public bool UserCreated { get; init; }

        /// <summary>
        /// Gets whether the company was created.
        /// </summary>
        public bool CompanyCreated { get; init; }

        /// <summary>
        /// Gets whether the user's access to the company was granted.
        /// </summary>
        public bool AccessGranted { get; init; }

        /// <summary>
        /// Gets the id of the database item the company's data lives in.
        /// </summary>
        public string CompanyDatabaseId { get; init; } = string.Empty;
    }
}
