using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.MemoEdit"/>: a multi-line <see cref="TextEdit"/>. Enter
    /// inserts a new line, so the value is committed when the user leaves the box.
    /// </summary>
    public sealed partial class MemoEdit : TextEdit
    {
        /// <inheritdoc />
        protected override string ModifierClass => "polhem-field-editor--memo";
    }
}
