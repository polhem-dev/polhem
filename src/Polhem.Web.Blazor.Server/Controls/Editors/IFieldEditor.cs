using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Contract shared by the field editor components that pair each <see cref="ControlType"/> with an
    /// input bound to a <see cref="FormDataObject"/> field. Extends the definition-layer
    /// <see cref="IBindFieldControl"/> / <see cref="IUIControl"/> contracts with the explicit binding
    /// surface, matching the <c>Polhem.UI.Avalonia.Controls.Editors.IFieldEditor</c> of the Avalonia head member for member.
    /// </summary>
    /// <remarks>
    /// Editors usually bind declaratively instead: set <see cref="IBindFieldControl.FieldName"/> (and
    /// optionally a layout field) as parameters and place the editor inside a <see cref="FormScope"/>,
    /// or pass the data object as a parameter. An explicit <c>Bind</c> wins over both until
    /// <see cref="Unbind"/>.
    /// </remarks>
    public interface IFieldEditor : IBindFieldControl, IUIControl
    {
        /// <summary>
        /// Binds the editor to <paramref name="dataObject"/> using the field name and rendering
        /// attributes (read-only state, edit modes) carried by <paramref name="field"/>.
        /// </summary>
        /// <param name="dataObject">The data object that backs two-way binding.</param>
        /// <param name="field">The layout field that drives editor metadata.</param>
        void Bind(FormDataObject dataObject, LayoutField field);

        /// <summary>
        /// Binds the editor to <paramref name="dataObject"/> by field name only. Schema metadata
        /// (<see cref="Polhem.Definition.Forms.FormField"/>) still applies; layout-level attributes do not.
        /// </summary>
        /// <param name="dataObject">The data object that backs two-way binding.</param>
        /// <param name="fieldName">The field (column) name to bind.</param>
        void Bind(FormDataObject dataObject, string fieldName);

        /// <summary>
        /// Binds the editor to a specific row (master or detail) of <paramref name="dataObject"/>.
        /// Value reads and write-backs target <paramref name="row"/>; schema metadata resolves against
        /// the row's table.
        /// </summary>
        /// <param name="dataObject">The data object that backs two-way binding.</param>
        /// <param name="field">The layout field / grid column carrying rendering attributes.</param>
        /// <param name="row">The row to bind.</param>
        void Bind(FormDataObject dataObject, LayoutFieldBase field, System.Data.DataRow row);

        /// <summary>
        /// Releases an explicit binding and returns the editor to its declarative binding.
        /// </summary>
        void Unbind();
    }
}
