namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// A saved user query as <see cref="UserQueryBar"/> lists it.
    /// </summary>
    /// <param name="RowId">The query's <c>sys_rowid</c>.</param>
    /// <param name="Name">The name it is listed under.</param>
    /// <param name="IsShared">Whether every user of the company may apply it.</param>
    /// <param name="IsOwn">Whether the signed-in user created it, and so may change it.</param>
    /// <param name="Criteria">The stored criteria text.</param>
    internal sealed record UserQueryEntry(Guid RowId, string Name, bool IsShared, bool IsOwn, string Criteria);
}
