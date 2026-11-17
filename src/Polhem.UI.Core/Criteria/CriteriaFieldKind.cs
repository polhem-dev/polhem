namespace Polhem.UI.Core.Criteria
{
    /// <summary>
    /// What a user enters for a query field, which decides the control and the operators a condition row offers.
    /// </summary>
    public enum CriteriaFieldKind
    {
        /// <summary>Free text such as a name or a remark.</summary>
        Text,

        /// <summary>A code or a document number, which is also compared as a range of codes.</summary>
        Code,

        /// <summary>A reference to a row of another form, picked through a lookup.</summary>
        Relation,

        /// <summary>One of the values the field lists.</summary>
        Choice,

        /// <summary>Yes or no, where leaving it unset matches both.</summary>
        Boolean,

        /// <summary>A number or an amount.</summary>
        Number,

        /// <summary>A calendar day.</summary>
        Date,

        /// <summary>An instant, entered by day.</summary>
        DateTime,
    }
}
