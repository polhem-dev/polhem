using System.Data;
using Polhem.Api.Client.Connectors;
using Polhem.UI.Core;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;

namespace Polhem.Web.Blazor.Server.DataObjects
{
    /// <summary>
    /// Holds the in-memory <see cref="System.Data.DataSet"/> for a single form view
    /// (one master row plus zero or more detail tables) and exposes a small string-based
    /// access surface for two-way binding by the field editors in
    /// <see cref="Polhem.Web.Blazor.Server.Controls.Editors"/> and by <see cref="Polhem.Web.Blazor.Server.Components.DynamicGrid"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The constructor seeds an empty <see cref="DataSet"/> derived from
    /// <see cref="FormSchema"/>. The async methods (<see cref="LoadAsync"/>,
    /// <see cref="NewAsync"/>, <see cref="SaveAsync"/>, <see cref="DeleteAsync"/>)
    /// round-trip through the supplied <see cref="FormApiConnector"/> and replace
    /// the local <see cref="DataSet"/> with the server response.
    /// </para>
    /// <para>
    /// The value rules this type used to carry privately — DataSet seeding, string/column
    /// coercion, display formatting, the CRUD preconditions — live once in <c>Polhem.UI.Core</c>
    /// (<see cref="FormValueBinding"/> and <see cref="FormDataGuard"/>), which every front end references,
    /// this head and <c>Polhem.UI.Avalonia</c> included. They are head-agnostic: the same
    /// <see cref="FormSchema"/> and the same server responses drive every head.
    /// </para>
    /// <para>
    /// NOTE: this used to be a verbatim copy of the Avalonia head's version, marked "deliberately
    /// parallel" with a note that nothing enforced the parallel. The two copies did diverge, with one
    /// side writing <see cref="DBNull"/> into a non-nullable column long after the other had fixed it.
    /// This head references <c>Polhem.UI.Core</c> but never its process-wide <see cref="UIServices"/>, which
    /// would share one user's services across circuits; <c>UIServicesUsageGateTests</c> enforces that.
    /// </para>
    /// <para>
    /// NOTE: the async methods resume on the caller's context (<c>ConfigureAwait(true)</c>). Their
    /// continuations replace <see cref="DataSet"/> and flip <see cref="IsLoading"/> /
    /// <see cref="IsDirty"/>, which the components read while rendering, and Blazor renders and
    /// handles events for a circuit on that circuit's dispatcher; called from a component event
    /// handler, this keeps those writes on it. The Avalonia head's counterpart does the same for
    /// its UI thread.
    /// </para>
    /// </remarks>
    public sealed partial class FormDataObject
    {
        private readonly FormSchema _schema;
        private readonly FormApiConnector? _connector;
        private bool _isDirty;

        /// <summary>
        /// Initializes a new instance of <see cref="FormDataObject"/> and derives the
        /// empty <see cref="DataSet"/> shape from <paramref name="schema"/>.
        /// </summary>
        /// <param name="schema">The form schema that drives column derivation.</param>
        /// <param name="connector">
        /// The connector used for server round-trips (<see cref="LoadAsync"/>,
        /// <see cref="NewAsync"/>, <see cref="SaveAsync"/>, <see cref="DeleteAsync"/>).
        /// Optional when only the in-memory <see cref="DataSet"/> surface is needed.
        /// </param>
        public FormDataObject(FormSchema schema, FormApiConnector? connector = null)
        {
            ArgumentNullException.ThrowIfNull(schema);
            if (string.IsNullOrWhiteSpace(schema.ProgId))
                throw new ArgumentException("FormSchema.ProgId must not be empty.", nameof(schema));

            _schema = schema;
            _connector = connector;
            DataSet = null!;
            ReplaceDataSet(FormValueBinding.BuildEmptyDataSet(schema), notify: false);
        }

        /// <summary>
        /// Gets the underlying dataset that holds the master row and detail tables.
        /// </summary>
        public DataSet DataSet { get; private set; }

        /// <summary>
        /// Gets the master <see cref="DataTable"/> (the table whose name equals
        /// <see cref="FormSchema.ProgId"/>).
        /// </summary>
        public DataTable MasterTable => DataSet.GetMasterTable()
            ?? throw new InvalidOperationException("Master table is missing from the dataset.");

        /// <summary>
        /// Gets the first row of the master table, or <c>null</c> if no master row exists yet.
        /// </summary>
        public DataRow? MasterRow => MasterTable.IsEmpty() ? null : MasterTable.Rows[0];

        /// <summary>
        /// Gets the detail tables (all dataset tables other than the master table).
        /// </summary>
        public IEnumerable<DataTable> DetailTables
        {
            get
            {
                var master = MasterTable;
                foreach (DataTable table in DataSet.Tables)
                {
                    if (!ReferenceEquals(table, master))
                        yield return table;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether an asynchronous round-trip is in progress.
        /// </summary>
        public bool IsLoading { get; private set; }

        /// <summary>
        /// Gets a value indicating whether any row has been modified since the last load/save.
        /// </summary>
        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty == value) return;
                _isDirty = value;
                IsDirtyChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Raised after any field value changes in any table of the <see cref="DataSet"/>, master and
        /// detail alike, whatever the write path. The event is bridged from the
        /// <see cref="DataTable.ColumnChanged"/> event so no writer has to remember to raise it.
        /// </summary>
        /// <remarks>
        /// Raised on the thread that wrote the value. A component that re-renders in response marshals
        /// through <c>InvokeAsync</c>, as the field editors do.
        /// </remarks>
        public event EventHandler<FieldValueChangedEventArgs>? FieldValueChanged;

        /// <summary>
        /// Raised when <see cref="IsDirty"/> flips.
        /// </summary>
        public event EventHandler? IsDirtyChanged;

        /// <summary>
        /// Raised after the underlying <see cref="DataSet"/> is replaced or reset
        /// (<see cref="LoadAsync"/>, <see cref="SaveAsync"/>, <see cref="DeleteAsync"/>,
        /// <see cref="NewAsync"/>, <see cref="InitializeNewMaster"/>). Bound editors re-read their values.
        /// </summary>
        public event EventHandler? DataSetReplaced;

        /// <summary>
        /// Reads the field value from the master row and renders it as a string suitable
        /// for HTML <c>value</c> attributes. Returns an empty string when the master row
        /// is absent, the field does not exist, or the value is <see cref="DBNull"/>.
        /// </summary>
        /// <param name="fieldName">The field (column) name.</param>
        public string GetField(string fieldName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

            var row = MasterRow;
            if (row is null) return string.Empty;
            return GetField(row, fieldName);
        }

        /// <summary>
        /// Reads the field value from <paramref name="row"/> (master or detail) and renders it as a
        /// binding string.
        /// </summary>
        /// <param name="row">The row to read from; it must belong to <see cref="DataSet"/>.</param>
        /// <param name="fieldName">The field (column) name.</param>
        public string GetField(DataRow row, string fieldName)
        {
            ArgumentNullException.ThrowIfNull(row);
            ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
            EnsureOwnedRow(row);

            if (!row.Table.Columns.Contains(fieldName)) return string.Empty;
            return FormValueBinding.ToBindingString(row[fieldName]);
        }

        /// <summary>
        /// Writes <paramref name="value"/> to the master row after coercing it to the
        /// column's declared CLR type. An empty input falls back to the column default
        /// (or <see cref="DBNull"/> when the column allows nulls).
        /// </summary>
        /// <remarks>
        /// A value equal to the stored one is not written, so re-committing an unchanged input neither
        /// dirties the object nor raises <see cref="FieldValueChanged"/>.
        /// </remarks>
        /// <param name="fieldName">The field (column) name.</param>
        /// <param name="value">The string value supplied by the bound input.</param>
        public void SetField(string fieldName, string? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

            var row = MasterRow;
            if (row is null) return;
            SetField(row, fieldName, value);
        }

        /// <summary>
        /// Writes <paramref name="value"/> to <paramref name="row"/> (master or detail) after coercing
        /// it to the column's declared CLR type, with the same empty-input and unchanged-value rules as
        /// the master overload.
        /// </summary>
        /// <param name="row">The row to write to; it must belong to <see cref="DataSet"/>.</param>
        /// <param name="fieldName">The field (column) name.</param>
        /// <param name="value">The string value supplied by the bound input.</param>
        public void SetField(DataRow row, string fieldName, string? value)
        {
            ArgumentNullException.ThrowIfNull(row);
            ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
            EnsureOwnedRow(row);

            if (!row.Table.Columns.Contains(fieldName)) return;

            var column = row.Table.Columns[fieldName]!;
            var newValue = FormValueBinding.ToColumnValue(value, column);
            if (Equals(newValue, row[fieldName])) return;

            // The write raises FieldValueChanged and marks the object dirty through the DataTable
            // event bridge.
            row[fieldName] = newValue;
        }

        /// <summary>
        /// Creates an empty master row populated with column defaults, replacing any
        /// existing row. Used when callers want a blank skeleton without going through
        /// the backend (e.g. unit tests or offline previews); production flows should
        /// prefer <see cref="NewAsync"/> so the server can seed defaults and
        /// <c>sys_rowid</c>.
        /// </summary>
        public void InitializeNewMaster()
        {
            MasterTable.Rows.Clear();
            MasterTable.Rows.Add(MasterTable.NewRow());
            IsDirty = false;
            // The dataset instance is unchanged, but its content was reset: bound editors re-read.
            DataSetReplaced?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Looks up the <see cref="FormField"/> metadata on the master table for the given
        /// field name, or <c>null</c> if no such field exists. UI components use this to
        /// reach metadata that the layout-level types do not carry (for example,
        /// <see cref="FormField.ListItems"/> for dropdowns).
        /// </summary>
        /// <param name="fieldName">The field (column) name.</param>
        public FormField? GetFormField(string fieldName)
        {
            var master = _schema.MasterTable;
            if (master?.Fields is null) return null;
            return master.Fields.Contains(fieldName) ? master.Fields[fieldName] : null;
        }

        /// <summary>
        /// Looks up the <see cref="FormField"/> metadata on the named table (master or detail), or
        /// <c>null</c> if the table or field does not exist.
        /// </summary>
        /// <param name="tableName">The schema table name.</param>
        /// <param name="fieldName">The field (column) name.</param>
        public FormField? GetFormField(string tableName, string fieldName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
            ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

            var table = _schema.Tables?.GetOrDefault(tableName);
            if (table?.Fields is null) return null;
            return table.Fields.Contains(fieldName) ? table.Fields[fieldName] : null;
        }

        /// <summary>
        /// Loads the master row (and its details) identified by <paramref name="rowId"/>
        /// from the backend BO and replaces the local <see cref="DataSet"/>.
        /// </summary>
        /// <param name="rowId">The master row identifier (<c>sys_rowid</c>).</param>
        /// <param name="cancellationToken">A token that cancels the round trip.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no <see cref="FormApiConnector"/> was supplied to the constructor,
        /// or when the server responds with a null <see cref="DataSet"/> (no row matched).
        /// </exception>
        public async Task LoadAsync(Guid rowId, CancellationToken cancellationToken = default)
        {
            var connector = FormDataGuard.RequireConnector(_connector, nameof(LoadAsync));

            IsLoading = true;
            try
            {
                var response = await connector.GetDataAsync(rowId, cancellationToken).ConfigureAwait(true);
                if (response.DataSet is null)
                    throw new InvalidOperationException(
                        $"No master row found for {SysFields.RowId} = {rowId}.");

                ReplaceDataSet(response.DataSet);
                IsDirty = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Persists the current <see cref="DataSet"/> through the backend BO and replaces
        /// the local <see cref="DataSet"/> with the refreshed copy returned by the server
        /// (so that server-generated columns surface back to the caller).
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the round trip.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no <see cref="FormApiConnector"/> was supplied to the constructor.
        /// </exception>
        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            var connector = FormDataGuard.RequireConnector(_connector, nameof(SaveAsync));

            IsLoading = true;
            try
            {
                var response = await connector.SaveAsync(DataSet, cancellationToken).ConfigureAwait(true);
                if (response.DataSet is not null)
                    ReplaceDataSet(response.DataSet);
                IsDirty = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Deletes the current master row through the backend BO and resets the local
        /// <see cref="DataSet"/> to the empty schema-derived skeleton.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the round trip.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no <see cref="FormApiConnector"/> was supplied to the constructor,
        /// when there is no master row to delete, or when the master table does not carry
        /// a <c>sys_rowid</c> column.
        /// </exception>
        public async Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            var connector = FormDataGuard.RequireConnector(_connector, nameof(DeleteAsync));
            var rowId = FormDataGuard.RequireMasterRowId(MasterRow);

            IsLoading = true;
            try
            {
                await connector.DeleteAsync(rowId, cancellationToken).ConfigureAwait(true);
                ReplaceDataSet(FormValueBinding.BuildEmptyDataSet(_schema));
                IsDirty = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Requests a blank <see cref="DataSet"/> skeleton seeded with FormSchema defaults
        /// and a server-issued <c>sys_rowid</c> from the backend BO, and replaces the
        /// local <see cref="DataSet"/>.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the round trip.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no <see cref="FormApiConnector"/> was supplied to the constructor,
        /// or when the server responds with a null <see cref="DataSet"/>.
        /// </exception>
        public async Task NewAsync(CancellationToken cancellationToken = default)
        {
            var connector = FormDataGuard.RequireConnector(_connector, nameof(NewAsync));

            IsLoading = true;
            try
            {
                var response = await connector.GetNewDataAsync(cancellationToken).ConfigureAwait(true);
                if (response.DataSet is null)
                    throw new InvalidOperationException(
                        "GetNewData returned a null DataSet; cannot initialize a new master row.");

                ReplaceDataSet(response.DataSet);
                IsDirty = false;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
