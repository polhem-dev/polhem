using Polhem.Api.Contracts.Form;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Input arguments for the <c>SetDefaultUserQuery</c> operation.
    /// </summary>
    public sealed class SetDefaultUserQueryArgs : BusinessArgs, ISetDefaultUserQueryRequest
    {
        /// <summary>
        /// Gets or sets the <c>sys_rowid</c> of the query to apply when the list opens, or <see cref="Guid.Empty"/> to apply none.
        /// </summary>
        public Guid RowId { get; set; }
    }
}
