using System.Data;
using Polhem.UI.Core;

namespace Polhem.Web.Blazor.Server.DataObjects
{
    /// <summary>
    /// DataTable-event half of <see cref="FormDataObject"/>: bridges the table events of the current
    /// <see cref="FormDataObject.DataSet"/> to <see cref="FormDataObject.FieldValueChanged"/> and to
    /// dirty tracking.
    /// </summary>
    public sealed partial class FormDataObject
    {
        /// <summary>
        /// The single assignment point for <see cref="DataSet"/>: moves the table event subscriptions
        /// from the old dataset to the new one (subscribing only after the server has fully populated
        /// it, so loading raises nothing) and notifies subscribers.
        /// </summary>
        private void ReplaceDataSet(DataSet dataSet, bool notify = true)
        {
            DetachTableEvents(DataSet);
            DataSet = dataSet;
            AttachTableEvents(dataSet);
            if (notify)
                DataSetReplaced?.Invoke(this, EventArgs.Empty);
        }

        private void AttachTableEvents(DataSet dataSet)
        {
            foreach (DataTable table in dataSet.Tables)
            {
                table.ColumnChanged += OnTableColumnChanged;
                table.RowChanged += OnTableRowChanged;
                table.RowDeleted += OnTableRowDeleted;
            }
        }

        private void DetachTableEvents(DataSet? dataSet)
        {
            if (dataSet is null) return;
            foreach (DataTable table in dataSet.Tables)
            {
                table.ColumnChanged -= OnTableColumnChanged;
                table.RowChanged -= OnTableRowChanged;
                table.RowDeleted -= OnTableRowDeleted;
            }
        }

        private void EnsureOwnedRow(DataRow row)
        {
            if (!ReferenceEquals(row.Table.DataSet, DataSet))
                throw new ArgumentException("Row does not belong to this data object's DataSet.", nameof(row));
        }

        private void OnTableColumnChanged(object? sender, DataColumnChangeEventArgs e)
        {
            // Seeding a detached row (NewRow before Rows.Add) stays silent; attaching the row marks
            // dirty through RowChanged instead.
            if (e.Row.RowState == DataRowState.Detached) return;
            IsDirty = true;
            FieldValueChanged?.Invoke(this, new FieldValueChangedEventArgs(
                ((DataTable)sender!).TableName,
                e.Column!.ColumnName,
                FormValueBinding.ToBindingString(e.ProposedValue),
                e.Row));
        }

        private void OnTableRowChanged(object? sender, DataRowChangeEventArgs e)
        {
            // Only data mutations dirty the object; framework actions (AcceptChanges raises Commit,
            // RejectChanges raises Rollback) do not.
            if (e.Action is DataRowAction.Add or DataRowAction.Change)
                IsDirty = true;
        }

        private void OnTableRowDeleted(object? sender, DataRowChangeEventArgs e)
        {
            IsDirty = true;
        }
    }
}
