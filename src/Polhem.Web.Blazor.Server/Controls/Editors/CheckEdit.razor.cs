using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.CheckEdit"/>: a check box bound to a Boolean field. The
    /// read-only state disables the box.
    /// </summary>
    public sealed partial class CheckEdit : FieldEditorBase
    {
        private bool _checked;

        /// <inheritdoc />
        protected override void RefreshFromSource()
        {
            _checked = string.Equals(GetValue(), bool.TrueString, StringComparison.OrdinalIgnoreCase);
        }

        private void CommitChecked(bool value)
        {
            WriteBack(value.ToString());
            RefreshFromSource();
        }
    }
}
