using Polhem.Api.Contracts.Form;

namespace Polhem.Api.Core.Messages.Form
{
    /// <summary>
    /// API request for the form DeleteUserQuery operation.
    /// </summary>
    public sealed class DeleteUserQueryRequest : ApiRequest, IDeleteUserQueryRequest
    {
        /// <summary>
        /// Gets or sets the <c>sys_rowid</c> of the query to delete.
        /// </summary>
        public Guid RowId { get; set; }
    }
}
