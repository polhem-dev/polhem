namespace Polhem.UI.Core.Connectors
{
    /// <summary>
    /// What a user can enter to filter a list column.
    /// </summary>
    public enum ColumnFilterKind
    {
        /// <summary>The column offers no filter.</summary>
        None,

        /// <summary>Text the value contains.</summary>
        Text,

        /// <summary>A range of numbers; either end may be left empty.</summary>
        Number,

        /// <summary>A range of calendar days; either end may be left empty.</summary>
        Date,

        /// <summary>A range of days over date and time values; either end may be left empty.</summary>
        DateTime,

        /// <summary>Yes or no.</summary>
        Boolean,

        /// <summary>One item of the field's list.</summary>
        Choice,
    }
}
