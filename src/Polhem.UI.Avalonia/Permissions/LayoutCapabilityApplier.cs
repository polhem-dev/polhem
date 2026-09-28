using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Api.Client.Permissions;

namespace Polhem.UI.Avalonia.Permissions
{
    /// <summary>
    /// Applies a client capability snapshot onto a form's layout by hiding / marking read-only its
    /// sensitive fields in place. Capability only narrows, never widens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// IMPORTANT: mutating in place is safe only because each caller hands over a layout that belongs
    /// to one view. <see cref="Polhem.UI.Avalonia.Views.FormView"/> gets its layout from
    /// <see cref="Polhem.UI.Avalonia.Views.FormView.ResolveLayoutAsync"/>, whose default clones on
    /// every path, the host-supplied <see cref="Polhem.UI.Avalonia.Views.FormView.Layout"/> included
    /// (pinned by <c>FormViewTests.EnsureDataObject_HostSuppliedLayout_LeavesHostInstanceUnchanged</c>),
    /// and whose remarks require an override to do the same;
    /// <see cref="Polhem.UI.Avalonia.Views.ListView"/> projects a fresh list layout from the schema on
    /// each attach. A new caller that passes a cached or host-owned layout must clone it first.
    /// </para>
    /// <para>
    /// Detail grid actions (Add / Edit / Delete rows) are deliberately NOT gated here. A detail grid
    /// belongs to the same aggregate as its master, so whether its rows can be edited follows the
    /// form's edit mode — permission is already enforced upstream at the toolbar commands (entering
    /// Add / Edit requires the master model's Create / Update). Only sensitive columns are degraded.
    /// </para>
    /// </remarks>
    internal static class LayoutCapabilityApplier
    {
        /// <summary>
        /// Degrades every master section field and detail grid of a form layout. No-op when the
        /// capability snapshot is <c>null</c> (enforcement inactive) or the schema is missing.
        /// </summary>
        public static void Apply(FormLayout? layout, FormSchema? schema, IReadOnlyDictionary<string, PermissionActions>? capabilities)
        {
            if (layout == null || schema == null || capabilities == null) { return; }

            if (layout.Sections != null)
            {
                foreach (var section in layout.Sections)
                    ApplyFields(section.Fields, schema, tableName: string.Empty, capabilities);
            }
            if (layout.Details != null)
            {
                foreach (var grid in layout.Details)
                    ApplyGrid(grid, schema, capabilities);
            }
        }

        /// <summary>
        /// Hides / marks read-only any sensitive columns of a single grid. No-op when the snapshot
        /// is <c>null</c>. Grid actions are not touched — they follow the form's edit mode.
        /// </summary>
        public static void ApplyGrid(LayoutGrid? grid, FormSchema? schema, IReadOnlyDictionary<string, PermissionActions>? capabilities)
        {
            if (grid == null || schema == null || capabilities == null) { return; }

            ApplyFields(grid.Columns, schema, grid.TableName, capabilities);
        }

        private static void ApplyFields<T>(IEnumerable<T>? fields, FormSchema schema, string tableName, IReadOnlyDictionary<string, PermissionActions> capabilities)
            where T : LayoutFieldBase
        {
            if (fields == null) { return; }
            foreach (var field in fields)
            {
                var cap = ElementCapabilityResolver.Default.ResolveField(schema, field.FieldName, tableName, capabilities);
                // Narrow only: an already-hidden or already-read-only field stays so.
                if (!cap.Visible) { field.Visible = false; }
                if (cap.ReadOnly) { field.ReadOnly = true; }
            }
        }
    }
}
