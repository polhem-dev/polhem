using Microsoft.Extensions.AI;

namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// Settings of a <see cref="FormDraftSession"/>.
    /// </summary>
    public sealed class FormDraftingOptions
    {
        /// <summary>
        /// Gets or sets the chat options every request starts from, such as the model id. The session copies them and
        /// replaces their tools with its own; the instance given here is not changed.
        /// </summary>
        public ChatOptions? ChatOptions { get; set; }

        /// <summary>
        /// Gets or sets how many rounds of tool calls one turn may take before the model is asked to answer without
        /// tools. The default is 30.
        /// </summary>
        public int MaxToolRoundsPerTurn { get; set; } = 30;

        /// <summary>
        /// Gets or sets the confidence below which a relation the model proposes must be confirmed by the user before the
        /// draft can be marked ready. The default is 0.8.
        /// </summary>
        public double RelationConfirmationThreshold { get; set; } = 0.8;
    }
}
