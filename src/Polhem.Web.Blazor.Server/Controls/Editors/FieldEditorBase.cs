using System.Data;
using Microsoft.AspNetCore.Components;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Fields;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Base component of the field editors: resolves the binding (explicit <c>Bind</c>, then the
    /// <see cref="DataObject"/> parameter, then the enclosing <see cref="FormScope"/>), follows the form
    /// mode, and keeps the editor in step with values other parties write.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Avalonia head keeps this state machine in a separate <c>FieldEditorBinder</c> that each
    /// editor holds, because its editors derive from different native controls and cannot share a base
    /// class. Blazor components can, so the same behavior lives here. This is the one deliberate
    /// structural difference between the heads; the override points (<see cref="RefreshFromSource"/>,
    /// <see cref="ApplyMetadata"/>, <see cref="SetControlState"/>) keep the Avalonia names.
    /// </para>
    /// <para>
    /// Changes the editor did not make itself (a value written by code, a lookup write-back, a reload)
    /// arrive through <see cref="FormDataObject.FieldValueChanged"/> and
    /// <see cref="FormDataObject.DataSetReplaced"/>, and re-render the editor through
    /// <see cref="ComponentBase.InvokeAsync(Action)"/>, so a write from outside the circuit's
    /// dispatcher is still safe. The subscriptions end when the component is disposed.
    /// </para>
    /// </remarks>
    public abstract class FieldEditorBase : ComponentBase, IFieldEditor, IDisposable
    {
        private ExplicitBinding? _explicit;
        private SingleFormMode? _controlStateMode;
        private FormDataObject? _subscribed;
        private bool _writing;
        private bool _disposed;

        /// <summary>
        /// Gets or sets the bound field (column) name. When empty, the name of <see cref="LayoutField"/> is used.
        /// </summary>
        [Parameter]
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the layout field (or grid column) whose read-only flag and edit modes the editor
        /// follows. It applies with or without a data object, so a layout preview shows the right state.
        /// </summary>
        [Parameter]
        public LayoutFieldBase? LayoutField { get; set; }

        /// <summary>
        /// Gets or sets the row to bind, or <c>null</c> to bind the master row.
        /// </summary>
        [Parameter]
        public DataRow? Row { get; set; }

        /// <summary>
        /// Gets or sets the data object to bind. When <c>null</c>, the enclosing <see cref="FormScope"/>'s
        /// data object is used.
        /// </summary>
        [Parameter]
        public FormDataObject? DataObject { get; set; }

        /// <summary>
        /// Gets or sets the id of the rendered input, so a <c>label</c> can point at it.
        /// </summary>
        [Parameter]
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets additional attributes applied to the rendered input.
        /// </summary>
        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

        [CascadingParameter]
        private FormScopeContext? Scope { get; set; }

        /// <summary>
        /// Gets or sets the bound field value as a binding string. Setting it writes the value to the
        /// data object, the same way a user commit does.
        /// </summary>
        public object? FieldValue
        {
            get => IsBound ? GetValue() : null;
            set
            {
                WriteBack(value?.ToString());
                RefreshFromSource();
            }
        }

        /// <summary>
        /// Gets the data object the editor is bound to, or <c>null</c>.
        /// </summary>
        protected FormDataObject? BoundDataObject { get; private set; }

        /// <summary>
        /// Gets the field name the editor is bound to, or an empty string.
        /// </summary>
        protected string BoundFieldName { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the layout field that describes the editor: the one supplied to an explicit bind, else
        /// <see cref="LayoutField"/>, else <c>null</c>.
        /// </summary>
        protected LayoutFieldBase? BoundLayoutField { get; private set; }

        /// <summary>
        /// Gets the row a row-scoped bind targets, or <c>null</c> for master-row bindings.
        /// </summary>
        protected DataRow? TargetRow { get; private set; }

        /// <summary>
        /// Gets the schema metadata resolved for the bound field, or <c>null</c>.
        /// </summary>
        protected FormField? FormField { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the editor is bound to a field.
        /// </summary>
        protected bool IsBound => BoundDataObject is not null && BoundFieldName.Length > 0;

        /// <summary>
        /// Gets a value indicating whether the layout marks the field read-only.
        /// </summary>
        protected bool IsLayoutReadOnly => BoundLayoutField?.ReadOnly ?? false;

        /// <summary>
        /// Gets the form mode in force: the one passed to <see cref="SetControlState"/>, else the enclosing
        /// <see cref="FormScope"/>'s, else <see cref="SingleFormMode.Edit"/>.
        /// </summary>
        protected SingleFormMode FormMode => _controlStateMode ?? Scope?.FormMode ?? SingleFormMode.Edit;

        /// <summary>
        /// Gets a value indicating whether the editor accepts input in the current <see cref="FormMode"/>.
        /// </summary>
        protected bool IsEditable => AllowsEdit(FormMode);

        /// <summary>
        /// Gets or sets additional fields whose value changes refresh the editor, for editors that show
        /// other fields than the one they bind.
        /// </summary>
        protected IReadOnlyList<string>? WatchFieldNames { get; set; }

        /// <inheritdoc />
        public void Bind(FormDataObject dataObject, LayoutField field)
        {
            ArgumentNullException.ThrowIfNull(dataObject);
            ArgumentNullException.ThrowIfNull(field);
            BindExplicit(new ExplicitBinding(dataObject, field.FieldName, field, null));
        }

        /// <inheritdoc />
        public void Bind(FormDataObject dataObject, string fieldName)
        {
            ArgumentNullException.ThrowIfNull(dataObject);
            ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
            BindExplicit(new ExplicitBinding(dataObject, fieldName, null, null));
        }

        /// <inheritdoc />
        public void Bind(FormDataObject dataObject, LayoutFieldBase field, DataRow row)
        {
            ArgumentNullException.ThrowIfNull(dataObject);
            ArgumentNullException.ThrowIfNull(field);
            ArgumentNullException.ThrowIfNull(row);
            BindExplicit(new ExplicitBinding(dataObject, field.FieldName, field, row));
        }

        /// <inheritdoc />
        public void Unbind()
        {
            _explicit = null;
            ApplyBinding();
            _ = InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Puts the editor in the read-only or editable state of <paramref name="formMode"/>, overriding
        /// the enclosing <see cref="FormScope"/>'s mode.
        /// </summary>
        /// <param name="formMode">The single-record form mode.</param>
        public virtual void SetControlState(SingleFormMode formMode)
        {
            _controlStateMode = formMode;
            _ = InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Determines whether the bound field is editable in <paramref name="formMode"/>; the rule is
        /// <see cref="FieldEditRules.AllowsEdit"/>, which every UI head shares.
        /// </summary>
        /// <param name="formMode">The single-record form mode.</param>
        /// <returns><c>true</c> when the editor accepts input.</returns>
        protected bool AllowsEdit(SingleFormMode formMode) => FieldEditRules.AllowsEdit(formMode, BoundLayoutField);

        /// <summary>
        /// Reads the bound field value as a binding string; empty when unbound.
        /// </summary>
        /// <returns>The binding string.</returns>
        protected string GetValue()
        {
            if (!IsBound) return string.Empty;
            return TargetRow is not null
                ? BoundDataObject!.GetField(TargetRow, BoundFieldName)
                : BoundDataObject!.GetField(BoundFieldName);
        }

        /// <summary>
        /// Writes the editor value back to the data object. The editor's own write does not echo back
        /// as a refresh; a no-op when unbound.
        /// </summary>
        /// <param name="value">The editor value rendered as a binding string.</param>
        protected void WriteBack(string? value)
        {
            if (_writing || !IsBound) return;
            _writing = true;
            try
            {
                if (TargetRow is not null)
                    BoundDataObject!.SetField(TargetRow, BoundFieldName, value);
                else
                    BoundDataObject!.SetField(BoundFieldName, value);
            }
            finally
            {
                _writing = false;
            }
        }

        /// <summary>
        /// Builds the class attribute of the rendered input: the shared editor class, the editor's own
        /// modifier class, and the read-only modifier when the editor does not accept input.
        /// </summary>
        /// <param name="modifierClass">The editor's modifier class, such as <c>polhem-field-editor--text</c>.</param>
        /// <returns>The class attribute value.</returns>
        protected string BuildCssClass(string modifierClass)
            => IsEditable
                ? "polhem-field-editor " + modifierClass
                : "polhem-field-editor " + modifierClass + " polhem-field-editor--readonly";

        /// <summary>
        /// Pulls the bound value from the data object into the editor's display state.
        /// </summary>
        protected abstract void RefreshFromSource();

        /// <summary>
        /// Applies layout and schema metadata to the editor. Runs whenever the binding changes, before
        /// <see cref="RefreshFromSource"/>.
        /// </summary>
        protected virtual void ApplyMetadata()
        {
        }

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
            base.OnParametersSet();
            ApplyBinding();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the data object subscriptions.
        /// </summary>
        /// <param name="disposing"><c>true</c> when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;
            if (disposing)
                Subscribe(null);
        }

        private void BindExplicit(ExplicitBinding binding)
        {
            _explicit = binding;
            ApplyBinding();
            _ = InvokeAsync(StateHasChanged);
        }

        private void ApplyBinding()
        {
            FormDataObject? dataObject;
            string fieldName;
            LayoutFieldBase? layoutField;
            DataRow? row;
            if (_explicit is { } bound)
            {
                (dataObject, fieldName, layoutField, row) = (bound.DataObject, bound.FieldName, bound.LayoutField ?? LayoutField, bound.Row);
            }
            else
            {
                dataObject = DataObject ?? Scope?.DataObject;
                fieldName = !string.IsNullOrWhiteSpace(FieldName) ? FieldName : LayoutField?.FieldName ?? string.Empty;
                layoutField = LayoutField;
                row = Row;
            }

            if (ReferenceEquals(dataObject, BoundDataObject)
                && string.Equals(fieldName, BoundFieldName, StringComparison.Ordinal)
                && ReferenceEquals(layoutField, BoundLayoutField)
                && ReferenceEquals(row, TargetRow))
            {
                return;
            }

            Subscribe(dataObject);
            BoundDataObject = dataObject;
            BoundFieldName = fieldName;
            BoundLayoutField = layoutField;
            TargetRow = row;
            FormField = dataObject is null || fieldName.Length == 0
                ? null
                : row is not null
                    ? dataObject.GetFormField(row.Table.TableName, fieldName)
                    : dataObject.GetFormField(fieldName);
            ApplyMetadata();
            RefreshFromSource();
        }

        private void Subscribe(FormDataObject? dataObject)
        {
            if (ReferenceEquals(dataObject, _subscribed)) return;
            if (_subscribed is not null)
            {
                _subscribed.FieldValueChanged -= OnFieldValueChanged;
                _subscribed.DataSetReplaced -= OnDataSetReplaced;
            }
            _subscribed = dataObject;
            if (dataObject is not null)
            {
                dataObject.FieldValueChanged += OnFieldValueChanged;
                dataObject.DataSetReplaced += OnDataSetReplaced;
            }
        }

        private void OnDataSetReplaced(object? sender, EventArgs e)
        {
            // A row-scoped binding points into the replaced dataset, which no longer owns the row.
            if (TargetRow is not null) return;
            _ = InvokeAsync(() =>
            {
                RefreshFromSource();
                StateHasChanged();
            });
        }

        private void OnFieldValueChanged(object? sender, FieldValueChangedEventArgs e)
        {
            // `_writing` is true while this editor itself writes back, so a self-originated change is
            // ignored. Master bindings filter by the master table name and row bindings by the exact row.
            // Names compare case-insensitively, following DataTable lookup semantics.
            if (_writing) return;
            if (TargetRow is not null)
            {
                if (!ReferenceEquals(e.Row, TargetRow)) return;
            }
            else if (!string.Equals(e.TableName, BoundDataObject?.MasterTable.TableName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            if (string.Equals(e.FieldName, BoundFieldName, StringComparison.OrdinalIgnoreCase)
                || (WatchFieldNames is { Count: > 0 } watched
                    && watched.Any(w => string.Equals(e.FieldName, w, StringComparison.OrdinalIgnoreCase))))
            {
                _ = InvokeAsync(() =>
                {
                    RefreshFromSource();
                    StateHasChanged();
                });
            }
        }

        private sealed record ExplicitBinding(FormDataObject DataObject, string FieldName, LayoutFieldBase? LayoutField, DataRow? Row);
    }
}
