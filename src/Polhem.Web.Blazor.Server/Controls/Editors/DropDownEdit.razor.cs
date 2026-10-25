using Polhem.Definition.Collections;
using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.DropDownEdit"/>: a select over the field's
    /// <see cref="Polhem.Definition.Forms.FormField.ListItems"/>, writing back the chosen
    /// <see cref="ListItem.Value"/>. The read-only state shows the chosen item's text.
    /// </summary>
    /// <remarks>
    /// A stored value that matches no item shows an empty selection, as the Avalonia head's
    /// <c>DropDownEdit</c> does, rather than the first item, which a select would otherwise show without
    /// the value being written.
    /// </remarks>
    public sealed partial class DropDownEdit : FieldEditorBase
    {
        private IReadOnlyList<ListItem> _items = [];
        private string _value = string.Empty;

        private ListItem? SelectedItem
            => _items.FirstOrDefault(i => string.Equals(i.Value, _value, StringComparison.Ordinal));

        /// <inheritdoc />
        protected override void ApplyMetadata()
        {
            _items = FormField?.ListItems is { } items ? items.ToList() : [];
        }

        /// <inheritdoc />
        protected override void RefreshFromSource()
        {
            _value = GetValue();
        }

        private void CommitValue(string? value)
        {
            WriteBack(value);
            RefreshFromSource();
        }
    }
}
