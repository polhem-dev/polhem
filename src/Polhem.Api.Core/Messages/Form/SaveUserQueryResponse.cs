using Polhem.Api.Contracts.Form;

namespace Polhem.Api.Core.Messages.Form
{
    /// <summary>
    /// API response for the form SaveUserQuery operation.
    /// </summary>
    public sealed class SaveUserQueryResponse : ApiResponse, ISaveUserQueryResponse
    {
        /// <summary>
        /// Gets or sets the <c>sys_rowid</c> of the saved query.
        /// </summary>
        public Guid RowId { get; set; }
    }
}
