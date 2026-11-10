using Polhem.Db;
using Polhem.Db.Dml;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Sorting;

namespace Polhem.Repository.Form
{
    /// <summary>
    /// Wraps the form command builder of a company form so that every statement it builds reads or
    /// deletes only the rows of one company.
    /// </summary>
    /// <remarks>
    /// The condition is AND-ed outside the caller's filter, so nothing in that filter, an OR
    /// included, can widen the result past the company.
    /// </remarks>
    internal sealed class CompanyScopedFormCommandBuilder : IFormCommandBuilder
    {
        private readonly IFormCommandBuilder _inner;
        private readonly string _companyId;

        /// <summary>
        /// Initializes a new <see cref="CompanyScopedFormCommandBuilder"/>.
        /// </summary>
        /// <param name="inner">The dialect's builder for the company form.</param>
        /// <param name="companyId">The company the statements are confined to.</param>
        public CompanyScopedFormCommandBuilder(IFormCommandBuilder inner, string companyId)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            ArgumentException.ThrowIfNullOrWhiteSpace(companyId);
            _companyId = companyId;
        }

        /// <inheritdoc/>
        public DbCommandSpec BuildSelect(string tableName, string selectFields, FilterNode? filter = null,
            SortFieldCollection? sortFields = null, int? skip = null, int? take = null)
            => _inner.BuildSelect(tableName, selectFields, Confine(filter), sortFields, skip, take);

        /// <inheritdoc/>
        public DbCommandSpec BuildCount(string tableName, FilterNode? filter = null)
            => _inner.BuildCount(tableName, Confine(filter));

        /// <inheritdoc/>
        public DbCommandSpec BuildDelete(string tableName, FilterNode filter)
            => _inner.BuildDelete(tableName, Confine(filter));

        private FilterGroup Confine(FilterNode? filter)
        {
            var company = FilterCondition.Equal(SysFields.CompanyId, _companyId);
            return filter == null ? FilterGroup.All(company) : FilterGroup.All(company, filter);
        }
    }
}
