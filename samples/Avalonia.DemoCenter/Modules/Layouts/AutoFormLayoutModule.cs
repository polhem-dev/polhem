using Avalonia;
using Avalonia.Controls;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.Controls.Editors;
using Avalonia.DemoCenter.Modules.Views;

namespace Avalonia.DemoCenter.Modules.Layouts
{
    /// <summary>
    /// Design-time layout generation: <c>FormLayoutGenerator.Generate</c> derives the form's sections
    /// and field placement from the schema, which is how a definition editor produces the starting
    /// point for a <c>FormLayout</c> definition file. The result is then rendered with the same
    /// primitives the production <c>FormView</c> uses — which, at runtime, reads the stored
    /// definition rather than generating one.
    /// </summary>
    public sealed class AutoFormLayoutModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Layout";

        /// <inheritdoc/>
        public override string Title => "FormLayout generated at design time";

        /// <inheritdoc/>
        public override string Description =>
            "FormLayoutGenerator.Generate produces a form layout (sections and field placement) from the schema as a design-time starting point; "
            + "at run time the app reads the saved FormLayout definition instead of deriving one on the fly.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var schema = SampleFormData.BuildMasterFormSchema();
            var data = SampleFormData.BuildMasterForm(schema);
            var layout = FormLayoutGenerator.Generate(schema, "default");
            // The presenter renders the layout alone; its editors and detail grids bind to the
            // data object the ambient FormScope hands down when they join the logical tree.
            var root = new ScrollViewer
            {
                Content = new FormLayoutPresenter
                {
                    Layout = layout,
                    Margin = new Thickness(4),
                },
            };
            FormScope.SetDataObject(root, data);
            return root;
        }
    }
}
