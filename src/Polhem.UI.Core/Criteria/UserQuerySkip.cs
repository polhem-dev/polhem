namespace Polhem.UI.Core.Criteria
{
    /// <summary>
    /// A stored condition row that <see cref="UserQueryCodec.Deserialize"/> left out, so the user can be told.
    /// </summary>
    /// <param name="FieldName">The stored field name.</param>
    /// <param name="Reason">Why the row was left out.</param>
    public readonly record struct UserQuerySkip(string FieldName, UserQuerySkipReason Reason);
}
