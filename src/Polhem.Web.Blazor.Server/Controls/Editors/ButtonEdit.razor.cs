using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.ButtonEdit"/>: an HTML input with a trailing button over
    /// <see cref="ButtonEditorBase"/>, whose lookup opens <see cref="LookupDialog"/>. This is the behavior of the Avalonia
    /// head's <c>Polhem.UI.Avalonia.Controls.Editors.ButtonEdit</c>.
    /// </summary>
    public sealed partial class ButtonEdit : ButtonEditorBase
    {
        private const string ModifierClass = "polhem-field-editor--button";

        private string InputCssClass
        {
            get
            {
                var css = BuildCssClass(ModifierClass);
                return OpenTarget is null ? css : css + " polhem-field-editor--opens-record";
            }
        }

        private int? MaxLengthAttribute => MaxLength > 0 ? MaxLength : null;
    }
}
