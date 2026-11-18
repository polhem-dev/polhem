namespace Polhem.Repository.Abstractions.Form
{
    /// <summary>
    /// Who a user query belongs to, as <see cref="IUserQueryRepository.GetOwner"/> reads it.
    /// </summary>
    /// <param name="ProgId">The form the query is for.</param>
    /// <param name="UserId">The user who created it.</param>
    /// <param name="IsShared">Whether every user of the company may apply it.</param>
    public sealed record UserQueryOwner(string ProgId, string UserId, bool IsShared);
}
