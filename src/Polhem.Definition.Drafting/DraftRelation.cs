namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// The existing form a <see cref="DraftField"/> picks a record from, with how sure the model is and why.
    /// </summary>
    public sealed class DraftRelation
    {
        /// <summary>Gets or sets the ProgId of the related form, such as <c>Employee</c>. Empty while undecided.</summary>
        public string ProgId { get; set; } = string.Empty;

        /// <summary>Gets or sets how sure the model is of the match, from 0 to 1.</summary>
        public double Confidence { get; set; }

        /// <summary>Gets or sets why the model chose this form, shown to the user.</summary>
        public string Reason { get; set; } = string.Empty;
    }
}
