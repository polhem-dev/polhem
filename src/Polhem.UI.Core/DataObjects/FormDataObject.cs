using System.Data;
using Polhem.Api.Contracts.Form;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Core.DataObjects
{
    /// <summary>
    /// Holds the in-memory <see cref="System.Data.DataSet"/> for a single form view
    /// (one master row plus zero or more detail tables) and exposes a small string-based
    /// access surface for two-way binding by the form views and field editors of every head.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The constructor seeds an empty <see cref="DataSet"/> derived from
    /// <see cref="FormSchema"/>. The async methods (<see cref="LoadAsync"/>,
    /// <see cref="NewAsync"/>, <see cref="SaveAsync"/>, <see cref="DeleteAsync"/>)
    /// round-trip through the supplied <see cref="IFormApiConnector"/> and replace
    /// the local <see cref="DataSet"/> with the server response.
    /// </para>
    /// <para>
    /// The Avalonia and Blazor heads each used to carry a copy of this type. The copies drifted twice:
    /// once when one side wrote <see cref="DBNull"/> into a non-nullable column long after the other had
    /// fixed it, and again in comments that described column casing the framework no longer used. One
    /// shared implementation is what keeps the heads aligned.
    /// </para>
    /// <para>
    /// IMPORTANT: this type never reads the process-wide <see cref="UIServices"/>. The Blazor Server head
    /// serves many users from one process, so a value read from there would belong to whoever registered it.
    /// Anything per user, such as the time zone, is passed in by the head that constructs the object.
    /// <c>UICoreServiceLocatorGateTests</c> enforces it for the whole assembly.
    /// </para>
    /// </remarks>
    public sealed partial class FormDataObject
    {
        private readonly FormSchema _schema;
        private readonly IFormApiConnector? _connector;
        private readonly Func<string>? _timeZoneProvider;
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
        /// <param name="timeZoneProvider">
        /// Returns the signed-in user's IANA time zone id, which dates new rows default to; <c>null</c>, or a
        /// blank result, means UTC. Called each time a row is added, so a sign-in after construction takes effect.
        /// </param>
        public FormDataObject(FormSchema schema, IFormApiConnector? connector = null,
            Func<string>? timeZoneProvider = null)
        {
            ArgumentNullException.ThrowIfNull(schema);
            if (string.IsNullOrWhiteSpace(schema.ProgId))
                throw new ArgumentException("FormSchema.ProgId must not be empty.", nameof(schema));

            _schema = schema;
            _connector = connector;
            _timeZoneProvider = timeZoneProvider;
            InitializeValueChange();
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
        /// Gets a value indicating whether the data has been modified since the last
        /// load/save. Changing this raises <see cref="IsDirtyChanged"/> only on an actual
        /// transition (assigning the same value raises nothing).
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
        /// Raised after any field value changes in any table of the
        /// <see cref="DataSet"/>, master and detail alike, regardless of the write
        /// path (<see cref="SetField(string, string?)"/>, grid cell editors, lookup write-backs,
        /// direct <see cref="DataRow"/> writes). Bridged from the ADO.NET
        /// <see cref="DataTable.ColumnChanged"/> event so no writer has to remember
        /// to raise it.
        /// </summary>
        /// <remarks>
        /// Raised on the thread that wrote the value. A subscriber that updates the UI from another thread
        /// marshals to the UI thread itself.
        /// </remarks>
        public event EventHandler<FieldValueChangedEventArgs>? FieldValueChanged;

        /// <summary>
        /// Raised after a row is added to any table of the <see cref="DataSet"/> (e.g. a
        /// detail grid's add action), once it has been seeded with its defaults. Bridged from
        /// <see cref="DataTable.RowChanged"/>.
        /// </summary>
        public event EventHandler<RowChangedEventArgs>? RowAdded;

        /// <summary>
        /// Raised after a row is deleted from any table of the <see cref="DataSet"/> (e.g. a
        /// detail grid's delete action). Bridged from <see cref="DataTable.RowDeleted"/>.
        /// </summary>
        public event EventHandler<RowChangedEventArgs>? RowDeleted;

        /// <summary>
        /// Raised when <see cref="IsDirty"/> transitions (clean ↔ dirty). Drives Save-button
        /// enablement / unsaved-changes prompts without polling.
        /// </summary>
        public event EventHandler? IsDirtyChanged;

        /// <summary>
        /// Raised after the underlying <see cref="DataSet"/> is replaced or reset
        /// (<see cref="LoadAsync"/>, <see cref="SaveAsync"/>, <see cref="DeleteAsync"/>,
        /// <see cref="NewAsync"/>, <see cref="InitializeNewMaster"/>). Bound editors
        /// re-read their values instead of requiring a full visual rebuild.
        /// </summary>
        public event EventHandler? DataSetReplaced;

        /// <summary>
        /// Reads the field value from the master row and renders it as a string suitable
        /// for two-way bound input controls. Returns an empty string when the master row
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
        /// Reads the field value from <paramref name="row"/> (master or detail) and
        /// renders it as a binding string. While the row is in an edit session
        /// (<see cref="BeginRowEdit"/>), the proposed value is returned.
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
        /// dirties the object nor raises <see cref="FieldValueChanged"/>. Native controls echo the value
        /// they were just given (Avalonia raises <c>TextChanged</c> once for the initial value, before
        /// the user has touched anything). Re-writing that echo would dirty the row and, on a NOT NULL
        /// column whose <see cref="DataColumn.DefaultValue"/> is still <see cref="DBNull"/> (typical for
        /// tables deserialized from the wire), raise <c>NoNullAllowedException</c> during <c>EndEdit</c>.
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
        /// Writes <paramref name="value"/> to <paramref name="row"/> (master or
        /// detail) after coercing it to the column's declared CLR type, with the
        /// same empty-input and unchanged-value rules as the master overload.
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

            // The write itself raises FieldValueChanged and marks dirty through the DataTable
            // event bridge; the compare-first guard above keeps no-op writes from producing event noise.
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
            // The dataset instance is unchanged (no resubscription needed), but the
            // visible content was reset, so notify bound editors to re-read.
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
        /// Looks up the <see cref="FormField"/> metadata on the named table (master or
        /// detail), or <c>null</c> if the table or field does not exist. Detail-grid
        /// editors use this to reach per-column metadata such as
        /// <see cref="FormField.ListItems"/>.
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
        /// Applies a lookup selection to the bound row: writes the selected row's
        /// <c>sys_rowid</c> into <paramref name="field"/> and copies each mapped source
        /// field of <see cref="FormField.RelationFieldMappings"/> to its local destination field,
        /// <see cref="FieldMapping.LookupOnly"/> entries included. The rules are
        /// <see cref="LookupFieldRules.ApplySelection"/>.
        /// </summary>
        /// <remarks>
        /// A value copied into a relation field (<c>ref_*</c>) is display-only between saves: a
        /// reload re-derives it from the server-side relation join, which stays the single source
        /// of truth. A value a lookup-only mapping copies into a stored field is saved with the row.
        /// </remarks>
        /// <param name="field">The relation field the lookup belongs to.</param>
        /// <param name="selectedRow">The row picked in the lookup window.</param>
        /// <param name="targetRow">The detail row to write to; <c>null</c> targets the master row.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <paramref name="selectedRow"/> lacks <c>sys_rowid</c> or a mapped
        /// source field; the mapping must stay within the target form's lookup field set.
        /// </exception>
        public void ApplyLookupSelection(FormField field, DataRow selectedRow, DataRow? targetRow = null)
        {
            WriteAs(FieldValueOrigin.LookupSelection,
                () => LookupFieldRules.ApplySelection(field, selectedRow, (name, value) => SetLookupField(targetRow, name, value)));
            NotifyLookupSelected(field, targetRow);
        }

        /// <summary>
        /// Clears a lookup selection: resets <paramref name="field"/> and every mapped
        /// destination field to the column default (empty input semantics of
        /// <see cref="SetField(string, string?)"/>).
        /// </summary>
        /// <param name="field">The relation field the lookup belongs to.</param>
        /// <param name="targetRow">The detail row to write to; <c>null</c> targets the master row.</param>
        public void ClearLookupSelection(FormField field, DataRow? targetRow = null)
            => WriteAs(FieldValueOrigin.LookupSelection,
                () => LookupFieldRules.ClearSelection(field, (name, value) => SetLookupField(targetRow, name, value)));

        private void SetLookupField(DataRow? targetRow, string fieldName, string? value)
        {
            if (targetRow is not null)
                SetField(targetRow, fieldName, value);
            else
                SetField(fieldName, value);
        }
    }
}
