namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// The cost of the current turn, raised by <see cref="FormDraftSession.UsageChanged"/>.
    /// </summary>
    public sealed class FormDraftUsageEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FormDraftUsageEventArgs"/> class.
        /// </summary>
        /// <param name="usage">The cost of the turn so far.</param>
        public FormDraftUsageEventArgs(FormDraftUsage usage)
        {
            ArgumentNullException.ThrowIfNull(usage);
            Usage = usage;
        }

        /// <summary>Gets the cost of the turn so far.</summary>
        public FormDraftUsage Usage { get; }
    }
}
