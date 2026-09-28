
namespace Polhem.Definition.Forms
{
    /// <summary>
    /// A field marked <see cref="FormField.Required"/> that is empty in a row a save would send.
    /// </summary>
    /// <remarks>
    /// Reported by <see cref="RequiredFieldCheck.FindMissing"/>, once per field however many rows
    /// leave it empty.
    /// </remarks>
    public sealed class MissingRequiredField
    {
        /// <summary>
        /// Initializes a new <see cref="MissingRequiredField"/>.
        /// </summary>
        /// <param name="table">The schema table the field belongs to.</param>
        /// <param name="field">The empty required field.</param>
        /// <param name="isDetail"><c>true</c> when <paramref name="table"/> is a detail table.</param>
        public MissingRequiredField(FormTable table, FormField field, bool isDetail)
        {
            ArgumentNullException.ThrowIfNull(table);
            ArgumentNullException.ThrowIfNull(field);
            Table = table;
            Field = field;
            IsDetail = isDetail;
        }

        /// <summary>Gets the schema table the field belongs to.</summary>
        public FormTable Table { get; }

        /// <summary>Gets the empty required field.</summary>
        public FormField Field { get; }

        /// <summary>Gets whether the field belongs to a detail table rather than the master table.</summary>
        public bool IsDetail { get; }

        /// <summary>
        /// Gets the text that names the field to a user: its caption, or its field name when it has
        /// none, followed for a detail field by the table's display name in parentheses.
        /// </summary>
        /// <remarks>
        /// The captions come from the schema, so they are in the user's language when the schema was
        /// localized, which is what the UI heads' definition loaders do.
        /// </remarks>
        public string DisplayText
        {
            get
            {
                string caption = string.IsNullOrWhiteSpace(Field.Caption) ? Field.FieldName : Field.Caption;
                if (!IsDetail) { return caption; }

                string tableName = string.IsNullOrWhiteSpace(Table.DisplayName) ? Table.TableName : Table.DisplayName;
                return $"{caption} ({tableName})";
            }
        }
    }
}
