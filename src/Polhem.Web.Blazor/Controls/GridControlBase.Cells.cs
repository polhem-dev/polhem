using System.Data;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Sorting;
using Polhem.UI.Core;
using Polhem.UI.Core.Fields;
using Polhem.Web.Blazor.Controls.Editors;
using Polhem.UI.Core.Lists;

namespace Polhem.Web.Blazor.Controls
{
    /// <summary>
    /// What the cells and headers of a <see cref="GridControlBase"/> show.
    /// </summary>
    public abstract partial class GridControlBase
    {
        /// <summary>Determines whether a column's cells show their editor.</summary>
        /// <param name="column">The column.</param>
        /// <returns>Whether they do.</returns>
        protected bool IsInCellEditor(LayoutColumn column) => InCellEdit && !column.ReadOnly;

        /// <summary>Builds the parameters of a cell's editor.</summary>
        /// <param name="column">The column.</param>
        /// <param name="row">The row.</param>
        /// <returns>The parameters by name.</returns>
        protected Dictionary<string, object?> BuildEditorParameters(LayoutColumn column, DataRow row)
        {
            var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [nameof(FieldEditorBase.FieldName)] = column.FieldName,
                [nameof(FieldEditorBase.LayoutField)] = column,
                [nameof(FieldEditorBase.Row)] = row,
                [nameof(FieldEditorBase.DataObject)] = BoundDataObject,
            };
            if (column.ControlType == ControlType.NumericEdit)
            {
                parameters[nameof(NumericEditorBase.CurrencySettings)] = CurrencySettings;
                parameters[nameof(NumericEditorBase.UnitSettings)] = UnitSettings;
                parameters[nameof(NumericEditorBase.DefaultCurrencyCode)] = DefaultCurrencyCode;
            }
            return parameters;
        }

        /// <summary>Gets the record a double-click on the cell opens, when it offers one: a relation column with a value, on a read-only form whose scope has someone to open it.</summary>
        /// <param name="row">The row.</param>
        /// <param name="column">The column.</param>
        /// <returns>The record, or <c>null</c>.</returns>
        protected RelationTarget? OpenTarget(DataRow row, LayoutColumn column)
        {
            if (Scope?.OpenRelation.HasDelegate != true || !row.Table.Columns.Contains(column.FieldName)) return null;
            var field = ResolveFormField(column.FieldName);
            return RelationOpenRules.IsAvailable(FormMode, field)
                   && RelationOpenRules.TryGetTarget(field, row[column.FieldName], out var target)
                ? target
                : null;
        }

        /// <summary>Asks the enclosing scope to open a record.</summary>
        /// <param name="target">The record.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected Task OpenRelationAsync(RelationTarget target, CancellationToken cancellationToken = default) => Scope?.OpenRelation.InvokeAsync(target) ?? Task.CompletedTask;

        /// <summary>Formats a cell for display.</summary>
        /// <param name="row">The row.</param>
        /// <param name="column">The column.</param>
        /// <returns>The text.</returns>
        protected string FormatCell(DataRow row, LayoutColumn column)
            => GridCellFormatter.FormatColumnText(row, column, ResolveFormField(column.FieldName),
                CurrencySettings, DefaultCurrencyCode, UnitSettings,
                flag => Text(flag ? PolhemUIText.True : PolhemUIText.False));

        /// <summary>Determines whether a check column is checked in a row.</summary>
        /// <param name="row">The row.</param>
        /// <param name="column">The column.</param>
        /// <returns>Whether it is checked.</returns>
        protected static bool IsChecked(DataRow row, LayoutColumn column) => GridCellFormatter.IsChecked(row, column.FieldName);

        // The schema field behind a column: through the data object for a bound grid, through the supplied
        // table for a list-mode grid; null when neither knows the field.
        private FormField? ResolveFormField(string fieldName)
        {
            if (BoundDataObject is { } dataObject)
                return dataObject.GetFormField(EffectiveTableName, fieldName);
            var fields = FormTable?.Fields;
            return fields is not null && fields.Contains(fieldName) ? fields[fieldName] : null;
        }

        /// <summary>
        /// Determines whether a column's header shows the required cue. In a bound grid the schema's Required counts as
        /// well as the layout's, because the server enforces the schema's; a list grid shows no required cue from the
        /// schema, since nothing is entered there.
        /// </summary>
        /// <param name="column">The column.</param>
        /// <returns>Whether to show the cue.</returns>
        protected bool ShowsRequiredCue(LayoutColumn column)
        {
            var required = column.Required
                || (BoundDataObject is { } dataObject && dataObject.GetFormField(EffectiveTableName, column.FieldName)?.Required == true);
            return FieldCaptionRules.ShowsRequiredCue(column.ReadOnly, required);
        }

        /// <summary>Determines whether <paramref name="row"/> is the selected one.</summary>
        /// <param name="row">The row.</param>
        /// <returns>Whether it is selected.</returns>
        protected bool IsSelected(DataRow row)
            => SelectedRowId is { } selected
                ? FormDataGuard.TryGetRowId(row, out var rowId) && rowId == selected
                : ReferenceEquals(row, _selectedRow);

        /// <summary>
        /// Determines whether a column's header offers sorting. Oracle cannot order by a <c>Text</c> column, and a binary
        /// value has no meaningful order.
        /// </summary>
        /// <param name="column">The column.</param>
        /// <returns>Whether it offers sorting.</returns>
        protected bool IsSortable(LayoutColumn column)
            => OnSortRequested.HasDelegate
                && ResolveFormField(column.FieldName) is { DbType: not (FieldDbType.Text or FieldDbType.Binary) };

        /// <summary>Returns what a column's filter takes, or <see cref="ColumnFilterKind.None"/> when it offers none.</summary>
        /// <param name="column">The column.</param>
        /// <returns>The kind of filter.</returns>
        protected ColumnFilterKind FilterKind(LayoutColumn column)
            => Filters is null || !OnFilterChanged.HasDelegate
                ? ColumnFilterKind.None
                : ListFilterState.GetKind(column, ResolveFormField(column.FieldName));

        /// <summary>Returns the schema field behind a column.</summary>
        /// <param name="column">The column.</param>
        /// <returns>The field, or <c>null</c>.</returns>
        protected FormField? ColumnField(LayoutColumn column) => ResolveFormField(column.FieldName);

        /// <summary>Gets the mark a sorted column's caption ends with, or an empty string.</summary>
        /// <param name="column">The column.</param>
        /// <returns>The mark.</returns>
        protected string SortMark(LayoutColumn column) => GridCellFormatter.SortMark(IsSorted(column) ? SortDirection : null);

        /// <summary>Gets the <c>aria-sort</c> value of a column's header, or <c>null</c> when it is not sorted.</summary>
        /// <param name="column">The column.</param>
        /// <returns>The value.</returns>
        protected string? AriaSort(LayoutColumn column)
        {
            if (!IsSorted(column)) return null;
            return SortDirection == SortDirection.Desc ? "descending" : "ascending";
        }

        private bool IsSorted(LayoutColumn column)
            => SortFieldName is not null && string.Equals(SortFieldName, column.FieldName, StringComparison.OrdinalIgnoreCase);
    }
}
