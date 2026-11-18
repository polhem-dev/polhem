using Polhem.Api.Contracts.Form;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Output result for the <c>SaveUserQuery</c> operation.
    /// </summary>
    public sealed class SaveUserQueryResult : BusinessResult, ISaveUserQueryResponse
    {
        /// <summary>
        /// Gets or sets the <c>sys_rowid</c> of the saved query.
        /// </summary>
        public Guid RowId { get; set; }
    }
}
