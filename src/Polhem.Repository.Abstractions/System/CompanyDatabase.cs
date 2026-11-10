namespace Polhem.Repository.Abstractions.System
{
    /// <summary>
    /// A company and the database item its data lives in, as <c>st_company</c> records them.
    /// </summary>
    /// <param name="CompanyId">The company id (<c>st_company.sys_id</c>).</param>
    /// <param name="DatabaseId">The company's database item (<c>st_company.company_database_id</c>).</param>
    public readonly record struct CompanyDatabase(string CompanyId, string DatabaseId);
}
