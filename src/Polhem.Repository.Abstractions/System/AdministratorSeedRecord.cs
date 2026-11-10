namespace Polhem.Repository.Abstractions.System
{
    /// <summary>
    /// The first administrator, the company and the access between them that a new deployment's seed writes to the
    /// common database.
    /// </summary>
    /// <param name="UserId">The administrator's user id (<c>st_user.sys_id</c>).</param>
    /// <param name="UserName">The administrator's display name.</param>
    /// <param name="PasswordHash">The hashed password; the repository never sees the password itself.</param>
    /// <param name="DeploymentAdmin">Whether the administrator administers the deployment as well.</param>
    /// <param name="CompanyId">The company id, in its stored lowercase form.</param>
    /// <param name="CompanyName">The company's display name.</param>
    /// <param name="CompanyDatabaseId">The database item the company's data lives in.</param>
    /// <param name="DefaultCurrency">The company's default currency code.</param>
    public sealed record AdministratorSeedRecord(
        string UserId,
        string UserName,
        string PasswordHash,
        bool DeploymentAdmin,
        string CompanyId,
        string CompanyName,
        string CompanyDatabaseId,
        string DefaultCurrency);
}
