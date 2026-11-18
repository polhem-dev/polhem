using Polhem.Api.Contracts.Form;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Input arguments for the <c>DeleteUserQuery</c> operation.
    /// </summary>
    public sealed class DeleteUserQueryArgs : BusinessArgs, IDeleteUserQueryRequest
    {
        /// <summary>
        /// Gets or sets the <c>sys_rowid</c> of the query to delete.
        /// </summary>
        public Guid RowId { get; set; }
    }
}
