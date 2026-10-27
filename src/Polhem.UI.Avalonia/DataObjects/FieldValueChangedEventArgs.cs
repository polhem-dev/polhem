using System.Data;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Avalonia.DataObjects
{
    /// <summary>
    /// Event data for <see cref="FormDataObject.FieldValueChanged"/>. Raised for
    /// master and detail tables alike; master subscribers filter by
    /// <see cref="TableName"/> and ignore <see cref="Row"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="TableName"/> / <see cref="FieldName"/> carry the casing the
    /// <see cref="System.Data.DataTable"/> stores (the framework's <c>AddColumn</c>
    /// uppercases column names, wire-deserialized tables keep the original casing) —
    /// compare them case-insensitively, matching DataTable lookup semantics.
    /// </remarks>
    public sealed class FieldValueChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FieldValueChangedEventArgs"/>.
        /// </summary>
        /// <param name="tableName">The table whose field changed.</param>
        /// <param name="fieldName">The field (column) name whose value changed.</param>
        /// <param name="value">The new value rendered as a binding string.</param>
        /// <param name="row">The row whose field changed.</param>
        public FieldValueChangedEventArgs(string tableName, string fieldName, string value, DataRow row)
            : this(tableName, fieldName, value, row, FieldValueOrigin.User)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="FieldValueChangedEventArgs"/> for a value written from
        /// <paramref name="origin"/>.
        /// </summary>
        /// <param name="tableName">The table whose field changed.</param>
        /// <param name="fieldName">The field (column) name whose value changed.</param>
        /// <param name="value">The new value rendered as a binding string.</param>
        /// <param name="row">The row whose field changed.</param>
        /// <param name="origin">Where the new value came from.</param>
        public FieldValueChangedEventArgs(string tableName, string fieldName, string value, DataRow row,
            FieldValueOrigin origin)
        {
            TableName = tableName;
            FieldName = fieldName;
            Value = value;
            Row = row;
            Origin = origin;
        }

        /// <summary>
        /// Gets where the new value came from: the user, a lookup pick, a server callback, or a committed row edit.
        /// </summary>
        public FieldValueOrigin Origin { get; }

        /// <summary>
        /// Gets the name of the table whose field changed.
        /// </summary>
        public string TableName { get; }

        /// <summary>
        /// Gets the field (column) name whose value changed.
        /// </summary>
        public string FieldName { get; }

        /// <summary>
        /// Gets the new value rendered as a binding string (same format as
        /// <see cref="FormDataObject.GetField(string)"/>).
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Gets the row whose field changed. Master subscribers can ignore this;
        /// detail subscribers use it to locate the affected row.
        /// </summary>
        public DataRow Row { get; }
    }
}
