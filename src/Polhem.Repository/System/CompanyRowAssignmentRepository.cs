using System.Globalization;
using Polhem.Db;
using Polhem.Definition;
using Polhem.Repository.Abstractions.System;

namespace Polhem.Repository.System
{
    /// <summary>
    /// Default <see cref="ICompanyRowAssignmentRepository"/>.
    /// </summary>
    internal sealed class CompanyRowAssignmentRepository : RepositoryBase, ICompanyRowAssignmentRepository
    {
        public CompanyRowAssignmentRepository(IRepositoryContext ctx, Guid accessToken, string progId)
            : base(ctx, accessToken, progId, scope: null)
        {
        }

        /// <inheritdoc/>
        public int CountUnassignedRows(string databaseId, string tableName, bool hasCompanyColumn)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databaseId);
            ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

            var databaseType = Context.ConnectionManager.GetConnectionInfo(databaseId).DatabaseType;
            string table = databaseType.QuoteIdentifier(tableName);
            string sql = hasCompanyColumn
                ? $"SELECT COUNT(*) FROM {table} WHERE {Unassigned(databaseType)}"
                : $"SELECT COUNT(*) FROM {table}";
            // NOTE: unbound on purpose. The rows counted belong to no company, so there is no company to bind to.
            var scalar = CreateDbAccess(databaseId).ExecuteScalar(sql);
            return Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        }

        /// <inheritdoc/>
        public void AssignUnassignedRows(string databaseId, string tableName, string companyId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databaseId);
            ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
            ArgumentException.ThrowIfNullOrWhiteSpace(companyId);

            var databaseType = Context.ConnectionManager.GetConnectionInfo(databaseId).DatabaseType;
            string table = databaseType.QuoteIdentifier(tableName);
            string column = databaseType.QuoteIdentifier(SysFields.CompanyId);
            CreateDbAccess(databaseId, companyId).ExecuteNonQuery(
                $"UPDATE {table} SET {column} = {CommandTextVariable.CompanyId} WHERE {Unassigned(databaseType)}");
        }

        // Oracle stores an empty string as NULL, so a row with no company is either.
        private static string Unassigned(Polhem.Definition.Database.DatabaseType databaseType)
        {
            string column = databaseType.QuoteIdentifier(SysFields.CompanyId);
            return $"({column} IS NULL OR {column} = '')";
        }
    }
}
