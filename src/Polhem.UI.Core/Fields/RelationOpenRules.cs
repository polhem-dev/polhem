using Polhem.Core;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Core.Permissions;

namespace Polhem.UI.Core.Fields
{
    /// <summary>
    /// The rules every UI head shares for opening the record a relation field points at from a read-only form: when a
    /// field offers it, which record it opens, and whether the user may see that record.
    /// </summary>
    /// <remarks>
    /// The head loads the related form's schema itself and asks <see cref="CanOpen"/> before it calls the backend. The
    /// backend still decides: the related form's <c>GetData</c> checks <see cref="PermissionActions.Read"/> and the
    /// record scope. Picking a value is a different permission from viewing it, because the lookup does not require
    /// <see cref="PermissionActions.Read"/> on the related form, so a user may hold a value they may not open.
    /// </remarks>
    public static class RelationOpenRules
    {
        /// <summary>
        /// Determines whether <paramref name="field"/> offers to open its related record in <paramref name="formMode"/>:
        /// only a relation field, and only on a read-only form, where the field's lookup is not available.
        /// </summary>
        /// <param name="formMode">The form mode in force.</param>
        /// <param name="field">The schema field, or <c>null</c>.</param>
        /// <returns><c>true</c> when the field opens its related record.</returns>
        public static bool IsAvailable(SingleFormMode formMode, FormField? field)
            => formMode == SingleFormMode.View && LookupFieldRules.HasLookup(field);

        /// <summary>
        /// Resolves the record <paramref name="field"/> points at when its value is <paramref name="value"/>.
        /// </summary>
        /// <param name="field">The schema field, or <c>null</c>.</param>
        /// <param name="value">
        /// The field's value: a <see cref="Guid"/>, its string form as a data object or a database returns it, or
        /// <c>null</c>, <see cref="DBNull"/> or an empty string when the field is empty.
        /// </param>
        /// <param name="target">The related record, when the method returns <c>true</c>.</param>
        /// <returns>
        /// <c>true</c> when the field is a relation field and its value names a row; <c>false</c> for any other field,
        /// an empty value, <see cref="Guid.Empty"/> or a value that is not a row id.
        /// </returns>
        public static bool TryGetTarget(FormField? field, object? value, out RelationTarget target)
        {
            target = default;
            if (field is null || !LookupFieldRules.HasLookup(field)) return false;

            Guid rowId = value switch
            {
                string text => Guid.TryParse(text, out var parsed) ? parsed : Guid.Empty,
                null => Guid.Empty,
                _ => ValueUtilities.CGuid(value),
            };
            if (rowId == Guid.Empty) return false;

            target = new RelationTarget(field.RelationProgId, rowId);
            return true;
        }

        /// <summary>
        /// Determines whether the user may open a record of the related form: whether
        /// <paramref name="capabilities"/> grant <see cref="PermissionActions.Read"/> on its permission model.
        /// </summary>
        /// <param name="relationSchema">The related form's schema.</param>
        /// <param name="capabilities">
        /// The user's capability snapshot, or <c>null</c> when no company is entered, which leaves the user unrestricted.
        /// </param>
        /// <returns>
        /// <c>true</c> when the user may read the related form, or when the form declares no permission model.
        /// </returns>
        public static bool CanOpen(FormSchema relationSchema, IReadOnlyDictionary<string, PermissionActions>? capabilities)
        {
            ArgumentNullException.ThrowIfNull(relationSchema);
            return ElementCapabilityResolver.Default.Can(relationSchema, PermissionActions.Read, capabilities);
        }
    }
}
