using System.Data;

namespace Polhem.Web.Blazor.Server.DataObjects
{
    /// <summary>
    /// Buffered row edit sessions of <see cref="FormDataObject"/>: the edit form of a detail row writes into a
    /// session that Commit publishes and Cancel rolls back, as in the Avalonia head.
    /// </summary>
    public sealed partial class FormDataObject
    {
        private readonly HashSet<DataRow> _rowsInEdit = [];

        /// <summary>
        /// Raised after a field value changes in a row under a buffered edit session
        /// (<see cref="BeginRowEdit"/>), in place of <see cref="FieldValueChanged"/>.
        /// </summary>
        /// <remarks>
        /// The value is only proposed: <see cref="CancelRowEdit"/> rolls it back without further events, and
        /// <see cref="CommitRowEdit"/> re-publishes every field the session changed through
        /// <see cref="FieldValueChanged"/>. This event therefore does not mark the object dirty. It lets the
        /// surface editing the row preview the session, for example by recomputing computed fields and
        /// refreshing their editors.
        /// </remarks>
        public event EventHandler<FieldValueChangedEventArgs>? RowEditFieldChanged;

        /// <summary>
        /// Starts a buffered edit session on <paramref name="row"/>: subsequent writes go to the row's proposed
        /// version and ADO.NET suspends change events until the session ends.
        /// </summary>
        /// <param name="row">The row to edit; it must belong to <see cref="DataSet"/>.</param>
        public void BeginRowEdit(DataRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            EnsureOwnedRow(row);
            _rowsInEdit.Add(row);
            row.BeginEdit();
        }

        /// <summary>
        /// Commits a buffered edit session: captures which fields the session actually changed (proposed
        /// against current, taken before <c>EndEdit</c> merges them), ends the edit, then re-publishes the
        /// per-field <see cref="FieldValueChanged"/> events ADO.NET suppressed during the session. Dirty
        /// tracking flows through the event bridge.
        /// </summary>
        /// <param name="row">The row whose edit session to commit.</param>
        public void CommitRowEdit(DataRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            EnsureOwnedRow(row);
            _rowsInEdit.Remove(row);

            if (!row.HasVersion(DataRowVersion.Proposed))
            {
                row.EndEdit();
                return;
            }

            var changedFields = new List<string>();
            foreach (DataColumn column in row.Table.Columns)
            {
                if (!Equals(row[column, DataRowVersion.Proposed], row[column, DataRowVersion.Current]))
                    changedFields.Add(column.ColumnName);
            }

            if (changedFields.Count == 0)
            {
                // BeginEdit creates the proposed version eagerly, so EndEdit would raise RowChanged (and
                // dirty the object) even though nothing changed; cancelling is equivalent and stays silent.
                row.CancelEdit();
                return;
            }

            row.EndEdit();

            foreach (var fieldName in changedFields)
            {
                FieldValueChanged?.Invoke(this, new FieldValueChangedEventArgs(
                    row.Table.TableName, fieldName, GetField(row, fieldName), row));
            }
        }

        /// <summary>
        /// Cancels a buffered edit session, restoring every field to its pre-session value. No events are
        /// raised: nothing changed.
        /// </summary>
        /// <param name="row">The row whose edit session to cancel.</param>
        public void CancelRowEdit(DataRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            EnsureOwnedRow(row);
            _rowsInEdit.Remove(row);
            row.CancelEdit();
        }
    }
}
