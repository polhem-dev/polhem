using Polhem.Api.Contracts.Form;

namespace Polhem.Api.Core.Messages.Form
{
    /// <summary>
    /// API response for the form SetDefaultUserQuery operation.
    /// </summary>
    public sealed class SetDefaultUserQueryResponse : ApiResponse, ISetDefaultUserQueryResponse
    {
        /// <summary>
        /// Gets or sets the <c>sys_rowid</c> of the caller's default query after the change, or <see cref="Guid.Empty"/>.
        /// </summary>
        public Guid DefaultRowId { get; set; }
    }
}
