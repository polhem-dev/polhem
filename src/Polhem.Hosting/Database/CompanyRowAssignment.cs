namespace Polhem.Hosting.Database
{
    /// <summary>
    /// Rows of a company table that carried no company and were given the one company their database
    /// belongs to, during <see cref="IDatabaseInitializer.UpgradeTables"/>.
    /// </summary>
    public sealed class CompanyRowAssignment
    {
        /// <summary>Gets the database the table is in.</summary>
        public string DatabaseId { get; init; } = string.Empty;

        /// <summary>Gets the table.</summary>
        public string TableName { get; init; } = string.Empty;

        /// <summary>Gets the company the rows were given.</summary>
        public string CompanyId { get; init; } = string.Empty;

        /// <summary>Gets how many rows were given the company, or would be in a dry run.</summary>
        public int Rows { get; init; }
    }
}
