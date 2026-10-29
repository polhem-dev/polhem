using Microsoft.AspNetCore.Components;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Fields;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Gives the field editors inside it an ambient binding scope: a data object to bind to and the form
    /// mode to follow. The counterpart of the Avalonia head's <c>Polhem.UI.Avalonia.Controls.Editors.FormScope</c> attached properties.
    /// </summary>
    /// <remarks>
    /// An editor binds through the scope when it has a field name and no explicit bind or
    /// <see cref="FieldEditorBase.DataObject"/> parameter of its own. Scopes nest; the nearest one wins.
    /// </remarks>
    public sealed partial class FormScope : ComponentBase
    {
        private FormScopeContext _context = new(null, SingleFormMode.Edit);

        /// <summary>
        /// Gets or sets the data object the editors inside bind to.
        /// </summary>
        [Parameter]
        public FormDataObject? DataObject { get; set; }

        /// <summary>
        /// Gets or sets the form mode the editors inside follow. Defaults to <see cref="SingleFormMode.Edit"/>.
        /// </summary>
        [Parameter]
        public SingleFormMode FormMode { get; set; } = SingleFormMode.Edit;

        /// <summary>
        /// Gets or sets the handler a relation field inside asks to open its record through, on a read-only form.
        /// Without one the fields do not offer it.
        /// </summary>
        [Parameter]
        public EventCallback<RelationTarget> OpenRelation { get; set; }

        /// <summary>
        /// Gets or sets the content the scope applies to.
        /// </summary>
        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
            // A new context instance on a change is what makes the cascade notify the editors.
            if (!ReferenceEquals(_context.DataObject, DataObject) || _context.FormMode != FormMode
                || !_context.OpenRelation.Equals(OpenRelation))
                _context = new FormScopeContext(DataObject, FormMode, OpenRelation);
        }
    }
}
