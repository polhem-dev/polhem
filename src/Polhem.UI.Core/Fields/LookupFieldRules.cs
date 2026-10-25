using System.Data;
using Polhem.Core;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.UI.Core.Fields
{
    /// <summary>
    /// The rules of a lookup (relation) field that every UI head shares: whether a field looks up at all,
    /// which program it looks up, which fields it shows, and how a selection is written back.
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
        /// Resolves the program a lookup opens: <see cref="FormField.LookupProgId"/> when set, else
        /// <see cref="FormField.RelationProgId"/>.
        /// </summary>
        /// <param name="field">The lookup field.</param>
        /// <returns>The program identifier.</returns>
        public static string ResolveProgId(FormField field)
        {
            ArgumentNullException.ThrowIfNull(field);
            return StringUtilities.IsNotEmpty(field.LookupProgId) ? field.LookupProgId : field.RelationProgId;
        }

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
        /// each mapped source field into its local destination field.
        /// <see cref="FormField.LookupFieldMappings"/> wins over <see cref="FormField.RelationFieldMappings"/>
        /// when both are declared.
        /// </summary>
        /// <param name="field">The lookup field.</param>
        /// <param name="selectedRow">The row picked in the lookup.</param>
        /// <param name="setField">Writes a binding string to a local field by name.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <paramref name="selectedRow"/> lacks <c>sys_rowid</c> or a mapped source field: the
        /// mapping must stay within the target form's lookup field set.
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
                // outside the lookup result is a hard error.
                if (!selectedRow.Table.Columns.Contains(mapping.SourceField))
                    throw new InvalidOperationException(
                        $"Lookup source field '{mapping.SourceField}' is not in the lookup result; " +
                        "declare it in the target form's LookupFields.");
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
        {
            if (field.LookupFieldMappings is { Count: > 0 } lookupMappings)
                return lookupMappings;
            if (field.RelationFieldMappings is { Count: > 0 } relationMappings)
                return relationMappings;
            return [];
        }
    }
}
