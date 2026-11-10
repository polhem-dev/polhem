namespace Polhem.Repository.Abstractions.System
{
    /// <summary>
    /// Writes a new deployment's first administrator, company and access to the common database.
    /// </summary>
    /// <remarks>
    /// A framework repository: obtained through <see cref="Polhem.Repository.Abstractions.Factories.IRepositoryFactory.Create{T}(Guid)"/>. The columns it writes are the
    /// framework's own <c>st_user</c>, <c>st_company</c> and <c>st_user_company</c> definitions, so a column added
    /// to one of them is written here, next to the other statements on the same tables.
    /// </remarks>
    public interface IAdministratorSeedRepository
    {
        /// <summary>
        /// Writes whichever of the user, the company and the access between them is missing, in one transaction,
        /// and leaves what exists as it is.
        /// </summary>
        /// <param name="record">The administrator, company and access to write.</param>
        /// <returns>Which of the three were written.</returns>
        AdministratorSeedOutcome SeedAdministrator(AdministratorSeedRecord record);
    }
}
