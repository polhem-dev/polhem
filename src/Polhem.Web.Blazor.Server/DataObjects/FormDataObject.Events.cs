using System.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
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
            _rowsInEdit.Clear();
            DataSet = dataSet;
            AttachTableEvents(dataSet);
            if (notify)
                DataSetReplaced?.Invoke(this, EventArgs.Empty);
        }

        private void AttachTableEvents(DataSet dataSet)
        {
            foreach (DataTable table in dataSet.Tables)
            {
                table.TableNewRow += OnTableNewRow;
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
                table.TableNewRow -= OnTableNewRow;
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

        /// <summary>
        /// Seeds a freshly created row's persisted columns with type-appropriate non-null values drawn from
        /// the FormSchema, so a new row never reaches the database with a NULL that would violate a NOT NULL
        /// constraint: <c>sys_rowid</c> gets a fresh key, <c>sys_master_rowid</c> links to the loaded master,
        /// and the other columns default by type. This is the seeding the Avalonia head and the server's
        /// <c>GetNewData</c> do, through the same <see cref="FormRowDefaults"/>.
        /// </summary>
        private void OnTableNewRow(object? sender, DataTableNewRowEventArgs e)
        {
            var formTable = _schema.Tables?.GetOrDefault(e.Row.Table.TableName);
            if (formTable is null) return;
            FormRowDefaults.Apply(formTable, e.Row, ResolveMasterRowId(), UserTimeZoneId);
        }

        // The master row's sys_rowid a new detail row links through sys_master_rowid. Returns the raw value
        // (Guid or the provider's exact string form) so the link preserves the master's casing: a re-parsed
        // Guid would lowercase a string key and orphan the detail under a case-sensitive comparison.
        private object? ResolveMasterRowId()
        {
            var master = MasterRow;
            if (master is null || !master.Table.Columns.Contains(SysFields.RowId)) return null;
            var value = master[SysFields.RowId];
            return value == DBNull.Value ? null : value;
        }

        private void OnTableColumnChanged(object? sender, DataColumnChangeEventArgs e)
        {
            // Seeding a detached row (NewRow before Rows.Add) stays silent; attaching the row marks
            // dirty through RowChanged instead.
            if (e.Row.RowState == DataRowState.Detached) return;
            var origin = _writeOrigin;
            var args = new FieldValueChangedEventArgs(
                ((DataTable)sender!).TableName,
                e.Column!.ColumnName,
                FormValueBinding.ToBindingString(e.ProposedValue),
                e.Row,
                origin);
            // A row under an edit session publishes FieldValueChanged only at commit; a cancelled session
            // must leak none for values that were rolled back. The session still hears each proposed value.
            if (_rowsInEdit.Contains(e.Row))
            {
                RowEditFieldChanged?.Invoke(this, args);
                NotifyValueChange(args.TableName, args.FieldName, e.Row, origin);
                return;
            }
            IsDirty = true;
            FieldValueChanged?.Invoke(this, args);
            NotifyValueChange(args.TableName, args.FieldName, e.Row, origin);
        }

        private void OnTableRowChanged(object? sender, DataRowChangeEventArgs e)
        {
            // Only data mutations dirty the object; framework actions (AcceptChanges raises Commit,
            // RejectChanges raises Rollback) do not. Rows in an edit session dirty the object at commit.
            if (_rowsInEdit.Contains(e.Row)) return;
            if (e.Action is DataRowAction.Add or DataRowAction.Change)
                IsDirty = true;
            if (e.Action == DataRowAction.Add)
                RowAdded?.Invoke(this, new RowChangedEventArgs(((DataTable)sender!).TableName, e.Row));
        }

        private void OnTableRowDeleted(object? sender, DataRowChangeEventArgs e)
        {
            IsDirty = true;
            RowDeleted?.Invoke(this, new RowChangedEventArgs(((DataTable)sender!).TableName, e.Row));
        }
    }
}
