namespace Polhem.Api.Contracts.Form
{
    /// <summary>
    /// Contract interface for the SetDefaultUserQuery request: sets the user query the caller applies when the form's list opens.
    /// </summary>
    /// <remarks>
    /// The default is the caller's own choice, so it may name a query another user shares.
    /// </remarks>
    public interface ISetDefaultUserQueryRequest
    {
        /// <summary>
        /// Gets the <c>sys_rowid</c> of the query to apply when the list opens, or <see cref="Guid.Empty"/> to apply none.
        /// </summary>
        Guid RowId { get; }
    }
}
