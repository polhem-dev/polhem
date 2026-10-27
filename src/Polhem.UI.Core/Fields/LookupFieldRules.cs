using System.Data;
using Polhem.Core;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.UI.Core.Fields
{
    /// <summary>
    /// The rules of a lookup (relation) field that every UI head shares: whether a field looks up at all,
    /// which fields it shows, and how a selection is written back. The program a lookup opens is the field's
    /// <see cref="FormField.RelationProgId"/>.
    /// </summary>
    public static class LookupFieldRules
    {
        /// <summary>
        /// Determines whether <paramref name="field"/> is a lookup field, that is, whether it declares a
        /// <see cref="FormField.RelationProgId"/>.
        /// </summary>
        /// <param name="field">The schema field, or <c>null</c>.</param>
        /// <returns><c>true</c> for a lookup field.</returns>
        public static bool HasLookup(FormField? field)
            => field is not null && StringUtilities.IsNotEmpty(field.RelationProgId);

        /// <summary>
        /// Resolves the local fields whose values a lookup field shows: the layout field's
        /// <see cref="LayoutFieldBase.DisplayFields"/> when set, else <see cref="FormField.GetDisplayFields"/>.
        /// </summary>
        /// <param name="layoutField">The layout field or grid column, or <c>null</c>.</param>
        /// <param name="formField">The schema field, or <c>null</c>.</param>
        /// <returns>The display field names in order; empty when none resolves.</returns>
        public static IReadOnlyList<string> ResolveDisplayFieldNames(LayoutFieldBase? layoutField, FormField? formField)
        {
            var fromLayout = layoutField?.DisplayFields;
            if (!string.IsNullOrEmpty(fromLayout))
                return fromLayout.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return formField?.GetDisplayFields() ?? [];
        }

        /// <summary>
        /// Writes a lookup selection: the selected row's <c>sys_rowid</c> into <paramref name="field"/>, then
        /// the source field of every <see cref="FormField.RelationFieldMappings"/> entry into its local
        /// destination field, <see cref="FieldMapping.LookupOnly"/> entries included.
        /// </summary>
        /// <remarks>
        /// A field marked <see cref="FormField.ValueChangeCallback"/> is filled by the server after the pick
        /// (<see cref="FieldValueChangeCoordinator"/>), so its sources need not be in the target form's lookup
        /// field set: those the selection has are written at once for display, the others are left to the callback.
        /// </remarks>
        /// <param name="field">The lookup field.</param>
        /// <param name="selectedRow">The row picked in the lookup.</param>
        /// <param name="setField">Writes a binding string to a local field by name.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <paramref name="selectedRow"/> lacks <c>sys_rowid</c>, or lacks a mapped source field of a
        /// field that does not call back: such a mapping must stay within the target form's lookup field set.
        /// </exception>
        public static void ApplySelection(FormField field, DataRow selectedRow, Action<string, string?> setField)
        {
            ArgumentNullException.ThrowIfNull(field);
            ArgumentNullException.ThrowIfNull(selectedRow);
            ArgumentNullException.ThrowIfNull(setField);

            if (!selectedRow.Table.Columns.Contains(SysFields.RowId))
                throw new InvalidOperationException("The lookup selection is missing the sys_rowid column.");

            setField(field.FieldName, FormValueBinding.ToBindingString(selectedRow[SysFields.RowId]));

            foreach (var mapping in ResolveMappings(field))
            {
                // A silently skipped mapping would leave stale display values behind, so a source field
                // outside the lookup result is a hard error, unless the server fills it through the callback.
                if (!selectedRow.Table.Columns.Contains(mapping.SourceField))
                {
                    if (field.ValueChangeCallback) { continue; }
                    throw new InvalidOperationException(
                        $"Lookup source field '{mapping.SourceField}' is not in the lookup result; " +
                        $"declare it in the LookupFields of form '{field.RelationProgId}'.");
                }
                setField(mapping.DestinationField, FormValueBinding.ToBindingString(selectedRow[mapping.SourceField]));
            }
        }

        /// <summary>
        /// Clears a lookup selection: writes an empty value to <paramref name="field"/> and to every mapped
        /// destination field.
        /// </summary>
        /// <param name="field">The lookup field.</param>
        /// <param name="setField">Writes a binding string to a local field by name.</param>
        public static void ClearSelection(FormField field, Action<string, string?> setField)
        {
            ArgumentNullException.ThrowIfNull(field);
            ArgumentNullException.ThrowIfNull(setField);

            setField(field.FieldName, null);
            foreach (var mapping in ResolveMappings(field))
                setField(mapping.DestinationField, null);
        }

        private static FieldMappingCollection ResolveMappings(FormField field)
            => field.RelationFieldMappings ?? [];
    }
}
