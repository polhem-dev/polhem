using Polhem.Definition.Layouts;

namespace Polhem.UI.Core.Fields
{
    /// <summary>
    /// Decides whether a field editor accepts input, the same way in every UI head.
    /// </summary>
    public static class FieldEditRules
    {
        /// <summary>
        /// Determines whether a field is editable in <paramref name="formMode"/>, combining the mode, the
        /// layout read-only flag and the master field's <see cref="LayoutField.AllowEditModes"/>.
        /// </summary>
        /// <remarks>
        /// A grid column (any <see cref="LayoutFieldBase"/> that is not a <see cref="LayoutField"/>) carries
        /// no per-mode setting, because the grid gates its columns as a whole, so it follows the mode and
        /// its read-only flag alone. With no layout field at all only the mode decides.
        /// </remarks>
        /// <param name="formMode">The single-record form mode.</param>
        /// <param name="layoutField">The layout field or grid column describing the editor, or <c>null</c>.</param>
        /// <returns><c>true</c> when the editor accepts input.</returns>
        public static bool AllowsEdit(SingleFormMode formMode, LayoutFieldBase? layoutField)
            => formMode != SingleFormMode.View
                && !(layoutField?.ReadOnly ?? false)
                && (layoutField is not LayoutField field || field.AllowEditModes.Allows(formMode));
    }
}
