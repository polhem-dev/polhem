using System.Data;
using Polhem.Api.Contracts.Form;

namespace Polhem.Api.Core.Messages.Form
{
    /// <summary>
    /// API response for the form FieldValueChange operation.
    /// </summary>
    public sealed class FieldValueChangeResponse : ApiResponse, IFieldValueChangeResponse
    {
        private Dictionary<string, string> _errors = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _warnings = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets a one-row table of the fields to write and their values, or <c>null</c> when nothing is to be
        /// written.
        /// </summary>
        public DataTable? Values { get; set; }

        /// <summary>
        /// Gets or sets the validation errors, keyed by field name.
        /// </summary>
        public Dictionary<string, string> Errors
        {
            get => _errors;
            // A reader that replaces the dictionary, as the wire codecs do, must not lose the case-insensitive keys.
            set => _errors = Equals(value?.Comparer, StringComparer.OrdinalIgnoreCase)
                ? value!
                : new(value ?? [], StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets or sets the warnings, keyed by field name.
        /// </summary>
        public Dictionary<string, string> Warnings
        {
            get => _warnings;
            // A reader that replaces the dictionary, as the wire codecs do, must not lose the case-insensitive keys.
            set => _warnings = Equals(value?.Comparer, StringComparer.OrdinalIgnoreCase)
                ? value!
                : new(value ?? [], StringComparer.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        IReadOnlyDictionary<string, string> IFieldValueChangeResponse.Errors => Errors;

        /// <inheritdoc />
        IReadOnlyDictionary<string, string> IFieldValueChangeResponse.Warnings => Warnings;
    }
}
