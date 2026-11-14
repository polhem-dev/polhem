using Polhem.Definition.Forms;

namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// A form converted from a <see cref="FormDraft"/>: the definition, its zh-TW texts and where it sits in the menu.
    /// </summary>
    /// <remarks>
    /// The schema holds only what the draft describes. The host adds the system fields, the database table names, the
    /// company category and the relation mappings when it applies its form conventions.
    /// </remarks>
    public sealed class FormDraftResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FormDraftResult"/> class.
        /// </summary>
        /// <param name="schema">The form definition.</param>
        public FormDraftResult(FormSchema schema)
        {
            Schema = schema;
        }

        /// <summary>Gets the form definition.</summary>
        public FormSchema Schema { get; }

        /// <summary>
        /// Gets the zh-TW texts, keyed by the sub-keys of <see cref="Polhem.Definition.Language.FormSchemaLocalizer"/>
        /// (<c>Schema.DisplayName</c>, <c>Table.LeaveRequest.DisplayName</c>, <c>Field.leave_type.Caption</c>).
        /// </summary>
        public IDictionary<string, string> ZhTw { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Gets where the form appears in the menu.</summary>
        public DraftMenuPlacement Menu { get; } = new();

        /// <summary>
        /// Gets the text of the document each field was read from, keyed by <c>TableName.FieldName</c>. A field without
        /// a source quote is left out.
        /// </summary>
        public IDictionary<string, string> FieldSources { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets what the conversion found undecided or had to leave out. The host's validation is separate
        /// (<see cref="IFormDraftingHost.Validate(FormDraftResult)"/>).
        /// </summary>
        public IList<DraftIssue> Issues { get; } = [];
    }
}
