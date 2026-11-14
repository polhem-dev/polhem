namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// A problem found in a draft, by <see cref="FormDraftConverter"/> or by the host's validation.
    /// </summary>
    public sealed class DraftIssue
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DraftIssue"/> class.
        /// </summary>
        /// <param name="severity">How serious the problem is.</param>
        /// <param name="location">Where it is, such as <c>LeaveRequest.leave_type</c>.</param>
        /// <param name="message">What is wrong, in English.</param>
        public DraftIssue(DraftIssueSeverity severity, string location, string message)
        {
            Severity = severity;
            Location = location;
            Message = message;
        }

        /// <summary>Gets how serious the problem is.</summary>
        public DraftIssueSeverity Severity { get; }

        /// <summary>Gets where the problem is, such as <c>LeaveRequest.leave_type</c>.</summary>
        public string Location { get; }

        /// <summary>Gets what is wrong.</summary>
        public string Message { get; }

        /// <inheritdoc/>
        public override string ToString() => $"{Severity} {Location}: {Message}";
    }
}
