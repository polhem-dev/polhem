using System.Data;

namespace Polhem.UI.Core.DataObjects
{
    /// <summary>
    /// Decides which changes of a <see cref="FormDataObject"/> a bound field editor reacts to, the same way in every
    /// UI head.
    /// </summary>
    /// <remarks>
    /// An editor binds either to a master field (no target row) or to a field of one row, such as a detail row in an
    /// edit form. Field names compare case-insensitively, following <see cref="DataTable"/> lookup semantics.
    /// </remarks>
    public static class FieldValueChangeFilter
    {
        /// <summary>
        /// Returns whether a field value change concerns an editor.
        /// </summary>
        /// <param name="e">The change.</param>
        /// <param name="targetRow">The row the editor is bound to, or <c>null</c> for a master binding.</param>
        /// <param name="masterTableName">The data object's master table name, used for a master binding.</param>
        /// <param name="fieldName">The field the editor shows.</param>
        /// <param name="watchFieldNames">Further fields whose changes the editor also shows, or <c>null</c>.</param>
        /// <returns>
        /// <c>true</c> when the change is on the editor's row (its exact row, or the master table for a master
        /// binding) and names its field or one of the watched fields.
        /// </returns>
        public static bool Concerns(FieldValueChangedEventArgs e, DataRow? targetRow, string? masterTableName,
            string fieldName, IReadOnlyList<string>? watchFieldNames)
        {
            ArgumentNullException.ThrowIfNull(e);
            bool onEditorRow = targetRow is not null
                ? ReferenceEquals(e.Row, targetRow)
                : string.Equals(e.TableName, masterTableName, StringComparison.OrdinalIgnoreCase);
            if (!onEditorRow) { return false; }

            return string.Equals(e.FieldName, fieldName, StringComparison.OrdinalIgnoreCase)
                || (watchFieldNames is { Count: > 0 }
                    && watchFieldNames.Any(w => string.Equals(e.FieldName, w, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Returns whether an editor reloads its value when the data object replaces its dataset.
        /// </summary>
        /// <param name="targetRow">The row the editor is bound to, or <c>null</c> for a master binding.</param>
        /// <returns><c>true</c> for a master binding; <c>false</c> for a row binding.</returns>
        /// <remarks>
        /// A row binding points into the replaced dataset, which the data object no longer owns, so reading the row
        /// through it throws an <see cref="ArgumentException"/>. A replacement that keeps the dataset (a new master
        /// row) leaves a detail row as it was. <c>FieldEditorBinderAdditionalTests</c> (Avalonia, which reread the row
        /// and threw until this rule was shared) and <c>FieldEditorBaseTests</c> (Blazor) replace the dataset under a
        /// row-bound editor.
        /// </remarks>
        public static bool RefreshesOnDataSetReplaced(DataRow? targetRow) => targetRow is null;
    }
}
