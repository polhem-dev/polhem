namespace Polhem.Db
{
    /// <summary>
    /// Constants for database command text variable names.
    /// </summary>
    public static class CommandTextVariable
    {
        /// <summary>
        /// Placeholder string representing the parameter name list.
        /// </summary>
        public const string Parameters = "{@Parameters}";

        /// <summary>
        /// Placeholder for the company a <see cref="DbAccess"/> is bound to (<see cref="DbAccess.BindCompany"/>),
        /// written where a statement compares or stores <c>sys_company_id</c>.
        /// </summary>
        /// <remarks>
        /// It becomes a parameter carrying the bound company, never text spliced into the statement. A
        /// statement run through a company-bound <see cref="DbAccess"/> must contain it, and one run through
        /// an unbound <see cref="DbAccess"/> must not; <see cref="DbCommandSpec.CreateCommand(Polhem.Definition.Database.DatabaseType, System.Data.Common.DbConnection)"/>
        /// refuses either mismatch. The check sees only that the placeholder is present, not that every
        /// company table in the statement is compared with it.
        /// </remarks>
        public const string CompanyId = "{@CompanyId}";
    }
}
