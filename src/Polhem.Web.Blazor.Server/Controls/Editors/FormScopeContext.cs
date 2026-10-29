using Microsoft.AspNetCore.Components;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Fields;
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
            : this(dataObject, formMode, default)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="FormScopeContext"/> that also carries the handler a relation field
        /// asks to open its record through.
        /// </summary>
        /// <param name="dataObject">The data object editors without their own bind to, or <c>null</c>.</param>
        /// <param name="formMode">The form mode that decides whether the editors accept input.</param>
        /// <param name="openRelation">The handler that opens a relation field's record, or <c>default</c> for none.</param>
        public FormScopeContext(FormDataObject? dataObject, SingleFormMode formMode, EventCallback<RelationTarget> openRelation)
        {
            DataObject = dataObject;
            FormMode = formMode;
            OpenRelation = openRelation;
        }

        /// <summary>
        /// Gets the data object editors without their own bind to, or <c>null</c>.
        /// </summary>
        public FormDataObject? DataObject { get; }

        /// <summary>
        /// Gets the form mode that decides whether the editors accept input.
        /// </summary>
        public SingleFormMode FormMode { get; }

        /// <summary>
        /// Gets the handler a relation field on a read-only form asks to open its record through. Without a delegate
        /// the editors inside do not offer to open one.
        /// </summary>
        public EventCallback<RelationTarget> OpenRelation { get; }
    }
}
