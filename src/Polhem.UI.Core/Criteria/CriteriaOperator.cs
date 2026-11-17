namespace Polhem.UI.Core.Criteria
{
    /// <summary>
    /// How a condition row compares its field with what the user entered.
    /// </summary>
    /// <remarks>
    /// These are the comparisons a person picks on screen, not the comparisons of a backend filter: a range may have
    /// one end left empty, and a date range may be relative. A converter such as <see cref="GetListFilterConverter"/>
    /// turns them into what a backend method takes.
    /// </remarks>
    public enum CriteriaOperator
    {
        /// <summary>The field contains the text.</summary>
        Contains,

        /// <summary>The field starts with the text.</summary>
        StartsWith,

        /// <summary>The field equals the value; for a date-time field, the value is a whole day.</summary>
        Equal,

        /// <summary>The field lies between two values, both included; either end may be left empty.</summary>
        Range,

        /// <summary>The field equals one of the values.</summary>
        In,
    }
}
