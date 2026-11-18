using Polhem.Api.Contracts.Form;

namespace Polhem.Api.Core.Messages.Form
{
    /// <summary>
    /// API request for the form SaveUserQuery operation.
    /// </summary>
    public sealed class SaveUserQueryRequest : ApiRequest, ISaveUserQueryRequest
    {
        /// <summary>
        /// Gets or sets the <c>sys_rowid</c> of the query to overwrite, or <see cref="Guid.Empty"/> to save a new one.
        /// </summary>
        public Guid RowId { get; set; }

        /// <summary>
        /// Gets or sets the name the query is listed under.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether every user of the company who can read the form may apply the query.
        /// </summary>
        public bool IsShared { get; set; }

        /// <summary>
        /// Gets or sets the stored criteria text, written by the front end; the server does not read it.
        /// </summary>
        public string Criteria { get; set; } = string.Empty;
    }
}
