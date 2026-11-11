using Polhem.Definition.Layouts;
using Polhem.UI.Core.DataObjects;

namespace Polhem.UI.Core.Editing
{
    /// <summary>
    /// Contract of the field editors that pair each <see cref="ControlType"/> with a control bound to a
    /// <see cref="FormDataObject"/> field, shared by every UI head. Extends the definition-layer
    /// <see cref="IBindFieldControl"/> and <see cref="IUIControl"/> contracts with the explicit binding surface.
    /// </summary>
    /// <remarks>
    /// Editors usually bind from their context instead: given <see cref="IBindFieldControl.FieldName"/>, an editor inside
    /// a form scope binds to the scope's data object. An explicit <c>Bind</c> wins over the context until
    /// <see cref="Unbind"/>.
    /// </remarks>
    public interface IFieldEditor : IBindFieldControl, IUIControl
    {
        /// <summary>
        /// Binds the editor to <paramref name="dataObject"/> using the field name and
        /// rendering attributes (read-only state, spans) carried by <paramref name="field"/>.
        /// </summary>
        /// <param name="dataObject">The data object that backs two-way binding.</param>
        /// <param name="field">The layout field that drives editor metadata.</param>
        void Bind(FormDataObject dataObject, LayoutField field);

        /// <summary>
        /// Binds the editor to <paramref name="dataObject"/> by field name only.
        /// Schema metadata (<see cref="Polhem.Definition.Forms.FormField"/>) still applies;
        /// layout-level attributes do not.
        /// </summary>
        /// <param name="dataObject">The data object that backs two-way binding.</param>
        /// <param name="fieldName">The field (column) name to bind.</param>
        void Bind(FormDataObject dataObject, string fieldName);

        /// <summary>
        /// Binds the editor to a specific row (master or detail) of
        /// <paramref name="dataObject"/>. Value reads and write-backs target
        /// <paramref name="row"/>; schema metadata resolves against the row's table.
        /// Used by the row edit form.
        /// </summary>
        /// <param name="dataObject">The data object that backs two-way binding.</param>
        /// <param name="field">The layout field / grid column carrying rendering attributes.</param>
        /// <param name="row">The row to bind.</param>
        void Bind(FormDataObject dataObject, LayoutFieldBase field, System.Data.DataRow row);

        /// <summary>
        /// Releases the binding and unsubscribes from data object events.
        /// </summary>
        void Unbind();
    }
}
