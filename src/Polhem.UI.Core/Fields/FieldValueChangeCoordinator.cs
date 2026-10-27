using System.Data;
using System.Runtime.CompilerServices;
using Polhem.Api.Contracts.Form;
using Polhem.Definition.Forms;

namespace Polhem.UI.Core.Fields
{
    /// <summary>
    /// Calls the form's business object back when the user changes a field marked
    /// <see cref="FormField.ValueChangeCallback"/>, and writes what the server returns into the same row. Every UI head's
    /// data object uses this one implementation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The data object reports every field change with its <see cref="FieldValueOrigin"/>. Only a
    /// <see cref="FieldValueOrigin.User"/> change calls back, and the values a callback returns are written through the
    /// data object's writer, which stamps them <see cref="FieldValueOrigin.ValueChangeResult"/>, so they never call back
    /// again even when their fields are marked too: one edit makes at most one call.
    /// </para>
    /// <para>
    /// A response is applied only while it is still the answer to the row's current value: a newer change of the same
    /// field, a different value in the field, or a row that left its table discards it. Values the row's table has no
    /// column for are skipped.
    /// </para>
    /// <para>
    /// The call is not awaited by the edit that started it. Its continuation runs on the context that started it, so a
    /// head starts it from its UI thread or its renderer, the same way it starts a save.
    /// </para>
    /// </remarks>
    public sealed class FieldValueChangeCoordinator
    {
        private readonly FormSchema _schema;
        private readonly IFieldValueChangeConnector _connector;
        private readonly Action<DataRow, string, string?> _writeValue;
        private readonly ConditionalWeakTable<DataRow, Dictionary<string, int>> _sequences = [];

        /// <summary>
        /// Initializes a new instance of <see cref="FieldValueChangeCoordinator"/>.
        /// </summary>
        /// <param name="schema">The form's schema, which says which fields call back.</param>
        /// <param name="connector">The connector the callbacks go through.</param>
        /// <param name="writeValue">
        /// Writes a returned value, in the binding text form, into a field of a row, marking the write
        /// <see cref="FieldValueOrigin.ValueChangeResult"/>.
        /// </param>
        public FieldValueChangeCoordinator(FormSchema schema, IFieldValueChangeConnector connector,
            Action<DataRow, string, string?> writeValue)
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(connector);
            ArgumentNullException.ThrowIfNull(writeValue);
            _schema = schema;
            _connector = connector;
            _writeValue = writeValue;
        }

        /// <summary>
        /// Occurs after a callback's values have been written into the row, carrying the errors and warnings the server
        /// returned for the view to show.
        /// </summary>
        public event EventHandler<FieldValueChangeAppliedEventArgs>? Applied;

        /// <summary>
        /// Occurs when a callback for the row's current value could not be completed, such as when the server cannot be
        /// reached.
        /// </summary>
        public event EventHandler<FieldValueChangeFailedEventArgs>? Failed;

        /// <summary>
        /// Returns whether <paramref name="fieldName"/> of <paramref name="tableName"/> is marked
        /// <see cref="FormField.ValueChangeCallback"/>.
        /// </summary>
        public bool CallsBack(string tableName, string fieldName) =>
            _schema.Tables?.GetOrDefault(tableName)?.Fields?.GetOrDefault(fieldName)?.ValueChangeCallback == true;

        /// <summary>
        /// Handles a field change the data object reports: a user change of a marked field calls the server back;
        /// anything else completes at once.
        /// </summary>
        /// <param name="tableName">The table of the field.</param>
        /// <param name="fieldName">The field.</param>
        /// <param name="row">The row, after the change.</param>
        /// <param name="origin">Where the new value came from.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>A task that completes once the callback, if any, has been applied, discarded or reported.</returns>
        public Task OnFieldValueChangedAsync(string tableName, string fieldName, DataRow row, FieldValueOrigin origin,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tableName);
            ArgumentNullException.ThrowIfNull(fieldName);
            ArgumentNullException.ThrowIfNull(row);
            if (origin != FieldValueOrigin.User || !CallsBack(tableName, fieldName)) { return Task.CompletedTask; }
            return CallBackAsync(tableName, fieldName, row, cancellationToken);
        }

        /// <summary>
        /// Calls the server back for a relation field whose lookup pick has just been written in full; the data object
        /// calls it after the pick's own writes, which carry <see cref="FieldValueOrigin.LookupSelection"/>.
        /// </summary>
        /// <param name="tableName">The table of the relation field.</param>
        /// <param name="fieldName">The relation field.</param>
        /// <param name="row">The row the pick was written into.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>A task that completes once the callback, if any, has been applied, discarded or reported.</returns>
        public Task OnLookupSelectedAsync(string tableName, string fieldName, DataRow row,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tableName);
            ArgumentNullException.ThrowIfNull(fieldName);
            ArgumentNullException.ThrowIfNull(row);
            return CallsBack(tableName, fieldName)
                ? CallBackAsync(tableName, fieldName, row, cancellationToken)
                : Task.CompletedTask;
        }

        private async Task CallBackAsync(string tableName, string fieldName, DataRow row,
            CancellationToken cancellationToken)
        {
            var sequences = _sequences.GetOrCreateValue(row);
            var key = fieldName.ToUpperInvariant();
            var sequence = sequences.TryGetValue(key, out var last) ? last + 1 : 1;
            sequences[key] = sequence;
            var value = FormValueBinding.ToBindingString(row[fieldName]);

            IFieldValueChangeResponse response;
            try
            {
                response = await _connector.FieldValueChangeAsync(tableName, fieldName, value, Snapshot(row), cancellationToken)
                    .ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // UI boundary: nothing up the stack awaits a callback the edit started, so a transport or server
                // failure is handed to the view through `Failed`, the way the views report a failed save.
                if (IsCurrent(row, key, sequence, fieldName, value))
                    Failed?.Invoke(this, new FieldValueChangeFailedEventArgs(tableName, fieldName, row, ex));
                return;
            }

            if (!IsCurrent(row, key, sequence, fieldName, value)) { return; }
            if (response.Values is { Rows.Count: > 0 } values)
            {
                foreach (DataColumn column in values.Columns)
                {
                    if (row.Table.Columns.Contains(column.ColumnName))
                        _writeValue(row, column.ColumnName, FormValueBinding.ToBindingString(values.Rows[0][column]));
                }
            }
            Applied?.Invoke(this, new FieldValueChangeAppliedEventArgs(tableName, fieldName, row,
                response.Errors, response.Warnings));
        }

        private bool IsCurrent(DataRow row, string key, int sequence, string fieldName, string value) =>
            row.RowState is not (DataRowState.Detached or DataRowState.Deleted)
            && _sequences.TryGetValue(row, out var sequences)
            && sequences.TryGetValue(key, out var latest) && latest == sequence
            && string.Equals(FormValueBinding.ToBindingString(row[fieldName]), value, StringComparison.Ordinal);

        /// <summary>A one-row copy of <paramref name="row"/>'s current values, in a table shaped like its own.</summary>
        private static DataTable Snapshot(DataRow row)
        {
            var table = row.Table.Clone();
            table.Rows.Add(row.ItemArray);
            return table;
        }
    }
}
