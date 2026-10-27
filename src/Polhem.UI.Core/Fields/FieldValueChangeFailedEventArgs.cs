using System.Data;

namespace Polhem.UI.Core.Fields
{
    /// <summary>
    /// Event data for <see cref="FieldValueChangeCoordinator.Failed"/>: a server callback could not be completed.
    /// </summary>
    public sealed class FieldValueChangeFailedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FieldValueChangeFailedEventArgs"/>.
        /// </summary>
        /// <param name="tableName">The table of the field that changed.</param>
        /// <param name="fieldName">The field that changed.</param>
        /// <param name="row">The row of the field.</param>
        /// <param name="exception">What the call threw.</param>
        public FieldValueChangeFailedEventArgs(string tableName, string fieldName, DataRow row, Exception exception)
        {
            TableName = tableName;
            FieldName = fieldName;
            Row = row;
            Exception = exception;
        }

        /// <summary>Gets the table of the field that changed.</summary>
        public string TableName { get; }

        /// <summary>Gets the field that changed.</summary>
        public string FieldName { get; }

        /// <summary>Gets the row of the field.</summary>
        public DataRow Row { get; }

        /// <summary>Gets what the call threw.</summary>
        public Exception Exception { get; }
    }
}
