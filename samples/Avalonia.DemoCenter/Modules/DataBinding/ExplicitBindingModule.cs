using Avalonia.Controls;
using Polhem.Base.Data;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Avalonia.DemoCenter.Modules.DataEditors;

namespace Avalonia.DemoCenter.Modules.DataBinding
{
    /// <summary>
    /// Explicit binding: the host calls <c>editor.Bind(dataObject, layoutField)</c> directly,
    /// without relying on an ambient <see cref="FormScope"/>. Useful when a control sits
    /// outside a scoped container or the host wants full control of the binding.
    /// </summary>
    public sealed class ExplicitBindingModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Data Binding";

        /// <inheritdoc/>
        public override string Title => "Explicit binding";

        /// <inheritdoc/>
        public override string Description =>
            "editor.Bind(dataObject, layoutField) binds directly without the container's ambient scope; useful when a control sits outside the scope or the host manages binding itself.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var data = DataEditorParts.SingleField("name", "Name", FieldDbType.String, initialValue: "Alice Chen");

            var editor = new TextEdit();
            editor.Bind(data, new LayoutField { FieldName = "name" });

            // No FormScope.SetDataObject on the root: the binding is explicit. FormMode still
            // flows from the shell's ambient scope, so the editor follows View/Add/Edit.
            var stack = new StackPanel { Spacing = 16, Margin = new Thickness(4) };
            stack.Children.Add(DataEditorParts.Section(
                "editor.Bind(dataObject, layoutField)",
                "The root sets no ambient DataObject; the editors are bound explicitly with Bind(...).",
                DataEditorParts.LabeledRow("name", editor)));
            stack.Children.Add(DataEditorParts.Section(
                "Live values",
                null,
                DataEditorParts.LiveValue(data, "name")));
            return new ScrollViewer { Content = stack };
        }
    }
}
