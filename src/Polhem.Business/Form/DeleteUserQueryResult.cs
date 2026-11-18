using Polhem.Api.Contracts.Form;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Output result for the <c>DeleteUserQuery</c> operation.
    /// </summary>
    public sealed class DeleteUserQueryResult : BusinessResult, IDeleteUserQueryResponse
    {
        /// <summary>
        /// Gets or sets the number of queries deleted: zero when the query no longer exists.
        /// </summary>
        public int RowsAffected { get; set; }
    }
}
