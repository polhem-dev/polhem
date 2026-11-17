namespace Polhem.UI.Core.Criteria
{
    /// <summary>
    /// Why a condition row is in a <see cref="QueryCriteria"/>.
    /// </summary>
    public enum CriteriaRowKind
    {
        /// <summary>The user added it; it can be removed.</summary>
        Optional,

        /// <summary>The query starts with it, usually with a value; it can be removed.</summary>
        Default,

        /// <summary>The query cannot run without a value in it, and it cannot be removed.</summary>
        Required,
    }
}
