namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// A step of a turn, raised by <see cref="FormDraftSession.Activity"/> so the host can show what the model is doing.
    /// </summary>
    public sealed class DraftActivityEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DraftActivityEventArgs"/> class.
        /// </summary>
        /// <param name="kind">What happened.</param>
        /// <param name="toolName">The tool, such as <c>upsert_field</c>; empty when no tool is involved.</param>
        /// <param name="subject">What the tool works on, such as a field name; empty when there is none.</param>
        /// <param name="detail">The reason a tool was refused, or the reasoning summary; otherwise empty.</param>
        public DraftActivityEventArgs(DraftActivityKind kind, string toolName = "", string subject = "", string detail = "")
        {
            Kind = kind;
            ToolName = toolName;
            Subject = subject;
            Detail = detail;
        }

        /// <summary>Gets what happened.</summary>
        public DraftActivityKind Kind { get; }

        /// <summary>Gets the tool, such as <c>upsert_field</c>; empty when no tool is involved.</summary>
        public string ToolName { get; }

        /// <summary>
        /// Gets what the tool works on: a field name (prefixed with its detail table, such as <c>LeaveRequestDetail.day</c>),
        /// a table, a ProgId or a search; empty when there is none.
        /// </summary>
        public string Subject { get; }

        /// <summary>
        /// Gets the reason a tool was refused (<see cref="DraftActivityKind.ToolRefused"/>) or the summary of the model's
        /// reasoning (<see cref="DraftActivityKind.Reasoning"/>), in the model's words; otherwise empty.
        /// </summary>
        public string Detail { get; }
    }
}
