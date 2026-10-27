using System.Data;
using Polhem.Definition.Forms;

namespace Polhem.UI.Core.Fields
{
    /// <summary>
    /// Event data for <see cref="FieldValueChangeCoordinator.Applied"/>: a server callback answered and its values have
    /// been written into the row.
    /// </summary>
    public sealed class FieldValueChangeAppliedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FieldValueChangeAppliedEventArgs"/>.
        /// </summary>
        /// <param name="tableName">The table of the field that changed.</param>
        /// <param name="fieldName">The field that changed.</param>
        /// <param name="row">The row the values were written into.</param>
        /// <param name="errors">The validation errors the server returned, keyed by field name.</param>
        /// <param name="warnings">The warnings the server returned, keyed by field name.</param>
        public FieldValueChangeAppliedEventArgs(string tableName, string fieldName, DataRow row,
            IReadOnlyDictionary<string, string> errors, IReadOnlyDictionary<string, string> warnings)
        {
            TableName = tableName;
            FieldName = fieldName;
            Row = row;
            Errors = errors;
            Warnings = warnings;
        }

        /// <summary>Gets the table of the field that changed.</summary>
        public string TableName { get; }

        /// <summary>Gets the field that changed.</summary>
        public string FieldName { get; }

        /// <summary>Gets the row the values were written into.</summary>
        public DataRow Row { get; }

        /// <summary>Gets the validation errors, keyed by field name.</summary>
        public IReadOnlyDictionary<string, string> Errors { get; }

        /// <summary>Gets the warnings, keyed by field name.</summary>
        public IReadOnlyDictionary<string, string> Warnings { get; }

        /// <summary>Gets whether the server returned any error or warning.</summary>
        public bool HasMessages => Errors.Count > 0 || Warnings.Count > 0;

        /// <summary>
        /// Formats the errors, then the warnings, one per line as <c>caption: message</c>, taking each caption from
        /// <paramref name="table"/> and falling back to the field name.
        /// </summary>
        /// <param name="table">The form table of <see cref="TableName"/>, or <c>null</c>.</param>
        public string FormatMessages(FormTable? table)
        {
            return string.Join(Environment.NewLine, Errors.Concat(Warnings).Select(pair =>
            {
                var caption = table?.Fields?.GetOrDefault(pair.Key)?.Caption;
                return $"{(string.IsNullOrEmpty(caption) ? pair.Key : caption)}: {pair.Value}";
            }));
        }
    }
}
