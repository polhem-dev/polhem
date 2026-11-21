using System.Globalization;
using Microsoft.Extensions.AI;

namespace Polhem.Definition.Drafting.Rules
{
    /// <summary>
    /// Settings for <see cref="RuleDrafting"/>.
    /// </summary>
    public sealed class RuleDraftingOptions
    {
        /// <summary>Gets or sets the options passed to the model with every request, such as the model id; <see langword="null"/> for none.</summary>
        public ChatOptions? ChatOptions { get; set; }

        /// <summary>Gets or sets how many times the model may submit within one request before it must reply instead.</summary>
        public int MaxToolRounds { get; set; } = 6;

        /// <summary>Gets or sets the language of the explanation and the samples' descriptions; <see langword="null"/> for English.</summary>
        public CultureInfo? ReplyCulture { get; set; }

        /// <summary>
        /// Gets or sets the languages the rule's message must be translated into besides the English base text, by
        /// culture name, such as <c>zh-TW</c>: the languages the form's application already has.
        /// </summary>
        public IReadOnlyList<string> MessageLanguages { get; set; } = [];

        /// <summary>Gets or sets the functions an expression may call, from the expression engine's catalog.</summary>
        public IReadOnlyList<RuleDraftFunction> Functions { get; set; } = [];

        /// <summary>Gets or sets the IANA time zone id the samples' <c>Today()</c> and <c>Now()</c> see; empty for UTC.</summary>
        public string TimeZoneId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the host renders Markdown in the model's replies and in the explanation. When it does not,
        /// the default, the model is asked to write plain text. The rule message and the sample descriptions are plain
        /// text either way.
        /// </summary>
        public bool MarkdownReplies { get; set; }
    }
}
