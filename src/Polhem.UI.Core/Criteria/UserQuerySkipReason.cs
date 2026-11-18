namespace Polhem.UI.Core.Criteria
{
    /// <summary>
    /// Why <see cref="UserQueryCodec.Deserialize"/> left out a stored condition row.
    /// </summary>
    public enum UserQuerySkipReason
    {
        /// <summary>The field is no longer one the user can query.</summary>
        UnknownField,

        /// <summary>The field no longer offers the stored operator.</summary>
        OperatorNotOffered,

        /// <summary>A stored value cannot be read as the field's current type.</summary>
        InvalidValue,
    }
}
