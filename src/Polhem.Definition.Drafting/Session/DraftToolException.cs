namespace Polhem.Definition.Drafting.Session
{
    /// <summary>
    /// A tool call the session refuses. Its message goes back to the model as the tool's result, and the conversation
    /// goes on.
    /// </summary>
    internal sealed class DraftToolException : Exception
    {
        public DraftToolException(string message)
            : base(message)
        {
        }
    }
}
