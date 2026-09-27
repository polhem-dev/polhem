using Avalonia.Media;
using Polhem.Definition.Layouts;

namespace Polhem.UI.Avalonia.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.MemoEdit"/>: a multi-line
    /// <see cref="TextEdit"/> that accepts returns and wraps text.
    /// </summary>
    public sealed class MemoEdit : TextEdit
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MemoEdit"/>.
        /// </summary>
        public MemoEdit()
        {
            AcceptsReturn = true;
            TextWrapping = TextWrapping.Wrap;
            MinHeight = 60;
        }
    }
}
