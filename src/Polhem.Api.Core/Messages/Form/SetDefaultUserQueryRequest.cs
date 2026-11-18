using Polhem.Api.Contracts.Form;

namespace Polhem.Api.Core.Messages.Form
{
    /// <summary>
    /// API request for the form SetDefaultUserQuery operation.
    /// </summary>
    public sealed class SetDefaultUserQueryRequest : ApiRequest, ISetDefaultUserQueryRequest
    {
        /// <summary>
        /// Gets or sets the <c>sys_rowid</c> of the query to apply when the list opens, or <see cref="Guid.Empty"/> to apply none.
        /// </summary>
        public Guid RowId { get; set; }
    }
}
