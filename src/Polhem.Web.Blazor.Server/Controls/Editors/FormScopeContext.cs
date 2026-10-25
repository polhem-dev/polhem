using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// The ambient binding scope a <see cref="FormScope"/> cascades to the field editors inside it.
    /// </summary>
    public sealed class FormScopeContext
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FormScopeContext"/>.
        /// </summary>
        /// <param name="dataObject">The data object editors without their own bind to, or <c>null</c>.</param>
        /// <param name="formMode">The form mode that decides whether the editors accept input.</param>
        public FormScopeContext(FormDataObject? dataObject, SingleFormMode formMode)
        {
            DataObject = dataObject;
            FormMode = formMode;
        }

        /// <summary>
        /// Gets the data object editors without their own bind to, or <c>null</c>.
        /// </summary>
        public FormDataObject? DataObject { get; }

        /// <summary>
        /// Gets the form mode that decides whether the editors accept input.
        /// </summary>
        public SingleFormMode FormMode { get; }
    }
}
