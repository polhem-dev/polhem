using Polhem.UI.Core.DataObjects;

namespace Polhem.Web.Blazor.Controls
{
    public abstract partial class GridControlBase
    {
        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Releases the data object subscription and the editor script.</summary>
        /// <remarks>
        /// A renderer calls only this method on a component that also implements <see cref="IDisposable"/>, so it releases
        /// the subscription too. A derived grid releases its own resources in an override of <see cref="Dispose(bool)"/>.
        /// </remarks>
        /// <returns>The operation.</returns>
        public async ValueTask DisposeAsync()
        {
            // The subscription first: releasing the script talks to the browser and can fail.
            Dispose();
            if (_script is not null) await _script.DisposeAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// Releases the data object subscription.
        /// </summary>
        /// <param name="disposing"><c>true</c> when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;
            if (disposing)
                Subscribe(null);
        }

        private void Subscribe(FormDataObject? dataObject)
        {
            if (ReferenceEquals(dataObject, _subscribed)) return;
            if (_subscribed is not null)
            {
                _subscribed.DataSetReplaced -= OnDataSetReplaced;
                _subscribed.FieldValueChanged -= OnFieldValueChanged;
                _subscribed.RowAdded -= OnRowChanged;
                _subscribed.RowDeleted -= OnRowChanged;
            }
            _subscribed = dataObject;
            if (dataObject is not null)
            {
                dataObject.DataSetReplaced += OnDataSetReplaced;
                dataObject.FieldValueChanged += OnFieldValueChanged;
                dataObject.RowAdded += OnRowChanged;
                dataObject.RowDeleted += OnRowChanged;
            }
        }

        private void OnDataSetReplaced(object? sender, EventArgs e)
        {
            _selectedRow = null;
            _editingRow = null;
            _ = InvokeAsync(StateHasChanged);
        }

        private void OnFieldValueChanged(object? sender, FieldValueChangedEventArgs e)
        {
            if (string.Equals(e.TableName, EffectiveTableName, StringComparison.OrdinalIgnoreCase))
                _ = InvokeAsync(StateHasChanged);
        }

        private void OnRowChanged(object? sender, RowChangedEventArgs e)
        {
            if (string.Equals(e.TableName, EffectiveTableName, StringComparison.OrdinalIgnoreCase))
                _ = InvokeAsync(StateHasChanged);
        }
    }
}
