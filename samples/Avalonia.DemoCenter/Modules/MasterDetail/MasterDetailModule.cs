using Avalonia.Controls;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Avalonia.DemoCenter.Modules.Views;

namespace Avalonia.DemoCenter.Modules.MasterDetail
{
    /// <summary>
    /// Master-detail: the generated <c>FormLayout</c> for the Staff + Phones schema,
    /// rendering the master section above the Phones detail grid, all bound to one local
    /// data object (the same composition the production <c>FormView</c> produces).
    /// </summary>
    public sealed class MasterDetailModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Master-Detail";

        /// <inheritdoc/>
        public override string Title => "Master + detail";

        /// <inheritdoc/>
        public override string Description =>
            "A Staff master section and a Phones detail grid bound to the same FormDataObject (editable in the default Edit mode). "
            + "The production back-end load/save flow is in apps/Polhem.Northwind.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var schema = SampleFormData.BuildSchema();
            var data = SampleFormData.BuildMasterDetail(schema);
            var layout = FormLayoutGenerator.Generate(schema, "default");
            return FormLayoutRenderer.Render(data, layout, GridEditMode.InCell);
        }
    }
}
