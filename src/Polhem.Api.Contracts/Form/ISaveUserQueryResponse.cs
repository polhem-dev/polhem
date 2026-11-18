namespace Polhem.Api.Contracts.Form
{
    /// <summary>
    /// Contract interface for the SaveUserQuery response.
    /// </summary>
    public interface ISaveUserQueryResponse
    {
        /// <summary>
        /// Gets the <c>sys_rowid</c> of the saved query.
        /// </summary>
        Guid RowId { get; }
    }
}
