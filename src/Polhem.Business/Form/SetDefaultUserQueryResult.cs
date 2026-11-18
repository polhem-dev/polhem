using Polhem.Api.Contracts.Form;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Output result for the <c>SetDefaultUserQuery</c> operation.
    /// </summary>
    public sealed class SetDefaultUserQueryResult : BusinessResult, ISetDefaultUserQueryResponse
    {
        /// <summary>
        /// Gets or sets the <c>sys_rowid</c> of the caller's default query after the change, or <see cref="Guid.Empty"/>.
        /// </summary>
        public Guid DefaultRowId { get; set; }
    }
}
