using Polhem.Api.Contracts.Form;

namespace Polhem.Api.Core.Messages.Form
{
    /// <summary>
    /// API response for the form DeleteUserQuery operation.
    /// </summary>
    public sealed class DeleteUserQueryResponse : ApiResponse, IDeleteUserQueryResponse
    {
        /// <summary>
        /// Gets or sets the number of queries deleted: zero when the query no longer exists.
        /// </summary>
        public int RowsAffected { get; set; }
    }
}
