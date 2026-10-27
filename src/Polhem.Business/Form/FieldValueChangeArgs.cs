using System.Data;
using Polhem.Api.Contracts.Form;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Input arguments for the <c>FieldValueChange</c> operation.
    /// </summary>
    public sealed class FieldValueChangeArgs : BusinessArgs, IFieldValueChangeRequest
    {
        /// <summary>
        /// Gets or sets the name of the form table that holds the field.
        /// </summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the field whose value changed.
        /// </summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the new value in the invariant text form a field binding uses; empty when cleared.
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a one-row table holding the edited row after the change, or <c>null</c>.
        /// </summary>
        public DataTable? Row { get; set; }
    }
}
