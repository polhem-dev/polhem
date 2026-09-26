using Avalonia.Controls;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Avalonia.DemoCenter.Modules.Views;

namespace Avalonia.DemoCenter.Modules.Layouts
{
    /// <summary>
    /// Multi-column layout: the generated <c>FormLayout</c> is set to two columns and one
    /// field is given a column span of two, showing CSS-grid-like field placement.
    /// </summary>
    public sealed class MultiColumnLayoutModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Layout";

        /// <inheritdoc/>
        public override string Title => "Multi-column layout (ColumnCount / ColumnSpan)";

        /// <inheritdoc/>
        public override string Description =>
            "FormLayout.ColumnCount=2 lays the fields out in two columns, and the notes field spans the whole row with ColumnSpan=2; fields wrap automatically according to their spans.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var schema = SampleFormData.BuildMasterFormSchema();
            var data = SampleFormData.BuildMasterForm(schema);

            var layout = FormLayoutGenerator.Generate(schema, "default");
            layout.ColumnCount = 2;
            // Let the memo field span the full width of the two-column grid.
            foreach (var section in layout.Sections ?? [])
            {
                foreach (var field in section.Fields ?? [])
                {
                    if (field.FieldName == "notes")
                        field.ColumnSpan = 2;
                }
            }

            return FormLayoutRenderer.Render(data, layout, GridEditMode.InCell);
        }
    }
}
