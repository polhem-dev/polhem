namespace Polhem.UI.Core
{
    /// <summary>
    /// The row ids that stand for the signed-in user in the entered company, which a "me" query condition resolves to.
    /// </summary>
    /// <param name="UserRowId">The user's row id (<c>st_user.sys_rowid</c>); <see cref="Guid.Empty"/> when unknown.</param>
    /// <param name="EmployeeRowId">The row id of the employee linked to the user in the entered company
    /// (<c>st_employee.sys_rowid</c>); <see cref="Guid.Empty"/> when the user has none there.</param>
    /// <remarks>
    /// The two ids are matched together, as the record scope's <c>Own</c> strategy matches an owner column: a column
    /// that holds either one belongs to the user. Row ids are GUIDs, so a user's and an employee's do not clash.
    /// </remarks>
    public sealed record UserIdentity(Guid UserRowId, Guid EmployeeRowId)
    {
        /// <summary>Gets the ids that are known, the user's first; empty when neither is.</summary>
        /// <remarks>Computed on each read rather than stored, so it stays out of the record's equality.</remarks>
        public IReadOnlyList<Guid> RowIds => [.. new[] { UserRowId, EmployeeRowId }.Where(id => id != Guid.Empty)];
    }
}
