using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.Controls.Editors;

namespace Polhem.Web.Blazor.Server.Controls
{
    /// <summary>
    /// A <see cref="FormLayoutPresenterBase"/> drawn with HTML: each editor is chosen by
    /// <see cref="FieldEditorFactory"/> from its <see cref="ControlType"/>, and each detail grid is a
    /// <see cref="GridControl"/>.
    /// </summary>
    public sealed partial class FormLayoutPresenter : FormLayoutPresenterBase
    {
        private string LabelClass(LayoutField field)
            => ShowsRequiredCue(field)
                ? "polhem-form-layout__label polhem-form-layout__label--required"
                : "polhem-form-layout__label";

        private string RootClass
            => IsCompact ? "polhem-form-layout polhem-form-layout--compact" : "polhem-form-layout";
    }
}
