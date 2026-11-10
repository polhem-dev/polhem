namespace Polhem.Repository.Abstractions.System
{
    /// <summary>
    /// The first administrator, the company and the access between them that a new deployment's seed writes to the
    /// common database.
    /// </summary>
    /// <remarks>
    /// A class rather than a record, so no generated <c>ToString</c> prints <see cref="PasswordHash"/> into a log.
    /// </remarks>
    public sealed class AdministratorSeedRecord
    {
        /// <summary>Gets the administrator's user id (<c>st_user.sys_id</c>).</summary>
        public required string UserId { get; init; }

        /// <summary>Gets the administrator's display name.</summary>
        public required string UserName { get; init; }

        /// <summary>Gets the hashed password; the repository never sees the password itself.</summary>
        public required string PasswordHash { get; init; }

        /// <summary>Gets whether the administrator administers the deployment as well.</summary>
        public bool DeploymentAdmin { get; init; }

        /// <summary>Gets the company id, in its stored lowercase form.</summary>
        public required string CompanyId { get; init; }

        /// <summary>Gets the company's display name.</summary>
        public required string CompanyName { get; init; }

        /// <summary>Gets the database item the company's data lives in.</summary>
        public required string CompanyDatabaseId { get; init; }

        /// <summary>Gets the company's default currency code.</summary>
        public required string DefaultCurrency { get; init; }
    }
}
