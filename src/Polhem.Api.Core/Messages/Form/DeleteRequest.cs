using Polhem.Api.Contracts.Form;

namespace Polhem.Api.Core.Messages.Form
{
    /// <summary>
    /// API request for the form Delete operation.
    /// </summary>
    public sealed class DeleteRequest : ApiRequest, IDeleteRequest
    {
        /// <summary>
        /// Gets or sets the master row identifier (<c>sys_rowid</c>) to delete.
        /// </summary>
        public Guid RowId { get; set; }
    }
}
