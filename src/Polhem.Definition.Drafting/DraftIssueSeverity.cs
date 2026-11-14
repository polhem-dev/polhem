namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// How serious a <see cref="DraftIssue"/> is.
    /// </summary>
    public enum DraftIssueSeverity
    {
        /// <summary>The form can be previewed, but something is still undecided or was filled in by a default.</summary>
        Warning,

        /// <summary>The form cannot be imported until this is fixed.</summary>
        Error,
    }
}
