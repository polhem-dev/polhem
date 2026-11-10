namespace Polhem.Repository.Abstractions.System
{
    /// <summary>
    /// Gives the rows of a company table that belong to no company their company, for an upgrade that adds
    /// <c>sys_company_id</c> to a database that already holds data.
    /// </summary>
    /// <remarks>
    /// A framework repository: obtained through <see cref="Polhem.Repository.Abstractions.Factories.IRepositoryFactory.Create{T}(Guid)"/>. It names the database on every
    /// call, because an upgrade walks every company database of the deployment.
    /// </remarks>
    public interface ICompanyRowAssignmentRepository
    {
        /// <summary>
        /// Counts the rows of <paramref name="tableName"/> that belong to no company.
        /// </summary>
        /// <param name="databaseId">The database item that holds the table.</param>
        /// <param name="tableName">The company table.</param>
        /// <param name="hasCompanyColumn">
        /// Whether the table has <c>sys_company_id</c> yet. Without it, every row belongs to no company.
        /// </param>
        /// <returns>The number of rows with no company.</returns>
        int CountUnassignedRows(string databaseId, string tableName, bool hasCompanyColumn);

        /// <summary>
        /// Gives every row of <paramref name="tableName"/> that belongs to no company to <paramref name="companyId"/>.
        /// </summary>
        /// <param name="databaseId">The database item that holds the table.</param>
        /// <param name="tableName">The company table, which has <c>sys_company_id</c>.</param>
        /// <param name="companyId">The company the rows belong to.</param>
        void AssignUnassignedRows(string databaseId, string tableName, string companyId);
    }
}
