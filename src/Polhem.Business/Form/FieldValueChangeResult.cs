using System.Data;
using Polhem.Api.Contracts.Form;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Output result for the <c>FieldValueChange</c> operation: what to write into the edited row and which messages
    /// to show on its fields.
    /// </summary>
    public sealed class FieldValueChangeResult : BusinessResult, IFieldValueChangeResponse
    {
        /// <summary>
        /// Gets or sets a one-row table of the fields to write and their values, or <c>null</c> when nothing is to be
        /// written. <see cref="SetValue(string, object?, Type)"/> builds it.
        /// </summary>
        public DataTable? Values { get; set; }

        /// <summary>
        /// Gets or sets the validation errors, keyed by field name.
        /// </summary>
        public Dictionary<string, string> Errors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the warnings, keyed by field name.
        /// </summary>
        public Dictionary<string, string> Warnings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        IReadOnlyDictionary<string, string> IFieldValueChangeResponse.Errors => Errors;

        /// <inheritdoc />
        IReadOnlyDictionary<string, string> IFieldValueChangeResponse.Warnings => Warnings;

        /// <summary>
        /// Asks the client to write <paramref name="value"/> into <paramref name="fieldName"/>, replacing a value set
        /// for the field before.
        /// </summary>
        /// <param name="fieldName">The field of the edited row.</param>
        /// <param name="value">The value; <c>null</c> or <see cref="DBNull"/> clears the field.</param>
        /// <param name="dataType">
        /// The column type to carry the value in; the value's own type is used when omitted, and a cleared value
        /// without one travels as text.
        /// </param>
        public void SetValue(string fieldName, object? value, Type? dataType = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(fieldName);
            Values ??= new DataTable("Values");
            if (Values.Rows.Count == 0) { Values.Rows.Add(Values.NewRow()); }
            if (!Values.Columns.Contains(fieldName))
            {
                var type = dataType ?? (value is null or DBNull ? typeof(string) : value.GetType());
                Values.Columns.Add(fieldName, type);
            }
            Values.Rows[0][fieldName] = value ?? DBNull.Value;
        }
    }
}
