namespace Polhem.Base.Attributes
{
    /// <summary>
    /// Custom attribute applied to a property to indicate that it should be excluded from tree node generation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class TreeNodeIgnoreAttribute : Attribute
    {
    }
}
