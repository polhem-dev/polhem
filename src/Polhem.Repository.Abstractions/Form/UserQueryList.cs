using System.Data;

namespace Polhem.Repository.Abstractions.Form
{
    /// <summary>
    /// The user queries a user can apply on a form, as <see cref="IUserQueryRepository.GetQueries"/> reads them.
    /// </summary>
    /// <param name="Table">The queries, with the columns <c>sys_rowid</c>, <c>sys_name</c>, <c>is_shared</c>,
    /// <c>is_own</c> and <c>criteria</c>; the user's own come first, each group ordered by name.</param>
    /// <param name="DefaultRowId">The <c>sys_rowid</c> the user's default names, or <see cref="Guid.Empty"/>. It may
    /// name a query that is not in <paramref name="Table"/> any more, when its owner stopped sharing it.</param>
    public sealed record UserQueryList(DataTable Table, Guid DefaultRowId);
}
