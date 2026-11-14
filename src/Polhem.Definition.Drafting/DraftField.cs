using Polhem.Core.Data;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// A field of a <see cref="DraftTable"/>. A <see langword="null"/> property is one nobody has decided yet.
    /// </summary>
    public sealed class DraftField
    {
        /// <summary>
        /// Gets or sets the field name. <see cref="FormDraftConverter"/> writes it in snake_case, so <c>Leave Type</c>
        /// becomes <c>leave_type</c>.
        /// </summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>Gets or sets the English caption.</summary>
        public string Caption { get; set; } = string.Empty;

        /// <summary>Gets or sets the zh-TW caption.</summary>
        public string CaptionZhTw { get; set; } = string.Empty;

        /// <summary>Gets or sets the database type, or <see langword="null"/> while undecided.</summary>
        public FieldDbType? DbType { get; set; }

        /// <summary>Gets or sets the maximum length of a text field, or <see langword="null"/> while undecided.</summary>
        public int? MaxLength { get; set; }

        /// <summary>Gets or sets the editor, or <see langword="null"/> to let the converter choose one.</summary>
        public ControlType? ControlType { get; set; }

        /// <summary>Gets the options of a field the document shows as a set of choices, such as check boxes.</summary>
        public IList<DraftOption> Options { get; } = [];

        /// <summary>
        /// Gets or sets the expression that computes the field, such as <c>quantity * unit_price</c>. A computed field
        /// is read-only.
        /// </summary>
        public string ValueExpression { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the field must be filled in.</summary>
        public bool Required { get; set; }

        /// <summary>
        /// Gets or sets the text of the document the field was read from, so the user can see where it came from.
        /// </summary>
        public string SourceQuote { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the existing form the field picks a record from, such as an employee or a customer, or
        /// <see langword="null"/> for a field that is not a relation.
        /// </summary>
        public DraftRelation? Relation { get; set; }
    }
}
