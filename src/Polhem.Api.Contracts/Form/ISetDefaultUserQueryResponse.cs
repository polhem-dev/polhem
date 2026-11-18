namespace Polhem.Api.Contracts.Form
{
    /// <summary>
    /// Contract interface for the SetDefaultUserQuery response.
    /// </summary>
    public interface ISetDefaultUserQueryResponse
    {
        /// <summary>
        /// Gets the <c>sys_rowid</c> of the caller's default query after the change, or <see cref="Guid.Empty"/>.
        /// </summary>
        Guid DefaultRowId { get; }
    }
}
