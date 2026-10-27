using System.Data;
using Polhem.Api.Contracts.Form;
using Polhem.Definition.Forms;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Avalonia.DataObjects
{
    /// <summary>
    /// Server callback half of <see cref="FormDataObject"/>: hands every field change to the shared
    /// <see cref="FieldValueChangeCoordinator"/> with where it came from, and writes what the server returns.
    /// </summary>
    /// <remarks>
    /// The rules (which changes call back, which answers are stale, how an answer is applied) live in the coordinator;
    /// this half only stamps each write with its <see cref="FieldValueOrigin"/> and starts the callbacks.
    /// </remarks>
    public partial class FormDataObject
    {
        private FieldValueOrigin _writeOrigin = FieldValueOrigin.User;
        private FieldValueChangeCoordinator? _valueChange;

        /// <summary>
        /// Occurs after a server callback for a field marked <see cref="FormField.ValueChangeCallback"/> has written its
        /// values into the row, carrying the errors and warnings it returned for the view to show.
        /// </summary>
        public event EventHandler<FieldValueChangeAppliedEventArgs>? FieldValueChangeApplied;

        /// <summary>
        /// Occurs when a server callback for a field marked <see cref="FormField.ValueChangeCallback"/> could not be
        /// completed.
        /// </summary>
        public event EventHandler<FieldValueChangeFailedEventArgs>? FieldValueChangeFailed;

        /// <summary>Gets the callback started last, for tests that wait for it.</summary>
        internal Task LastFieldValueChange { get; private set; } = Task.CompletedTask;

        private void InitializeValueChange()
        {
            if (_connector is not IFieldValueChangeConnector connector) { return; }
            _valueChange = new FieldValueChangeCoordinator(_schema, connector, WriteValueChangeResult);
            _valueChange.Applied += (_, e) => FieldValueChangeApplied?.Invoke(this, e);
            _valueChange.Failed += (_, e) => FieldValueChangeFailed?.Invoke(this, e);
        }

        // An answer can arrive after a load or a new record replaced the data set; a row of the old one is left alone.
        private void WriteValueChangeResult(DataRow row, string fieldName, string? value)
        {
            if (!ReferenceEquals(row.Table.DataSet, DataSet)) { return; }
            WriteAs(FieldValueOrigin.ValueChangeResult, () => SetField(row, fieldName, value));
        }

        private void WriteAs(FieldValueOrigin origin, Action write)
        {
            var previous = _writeOrigin;
            _writeOrigin = origin;
            try
            {
                write();
            }
            finally
            {
                _writeOrigin = previous;
            }
        }

        private void NotifyValueChange(string tableName, string fieldName, DataRow row, FieldValueOrigin origin)
        {
            if (_valueChange is not null)
                LastFieldValueChange = _valueChange.OnFieldValueChangedAsync(tableName, fieldName, row, origin);
        }

        private void NotifyLookupSelected(FormField field, DataRow? targetRow)
        {
            var row = targetRow ?? MasterRow;
            if (_valueChange is not null && row is not null)
                LastFieldValueChange = _valueChange.OnLookupSelectedAsync(row.Table.TableName, field.FieldName, row);
        }
    }
}
