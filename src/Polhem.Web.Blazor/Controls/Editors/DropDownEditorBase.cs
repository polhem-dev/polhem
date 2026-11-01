using Polhem.Definition.Collections;

namespace Polhem.Web.Blazor.Controls.Editors
{
    /// <summary>
    /// Base component of a select over the field's <see cref="Polhem.Definition.Forms.FormField.ListItems"/>, writing
    /// back the chosen <see cref="ListItem.Value"/>.
    /// </summary>
    /// <remarks>
    /// A stored value that matches no item has no <see cref="SelectedItem"/>. A UI package shows an empty selection for
    /// it rather than the first item, which a select would otherwise show without the value being written.
    /// </remarks>
    public abstract class DropDownEditorBase : FieldEditorBase
    {
        /// <summary>
        /// Gets the items to choose from.
        /// </summary>
        protected IReadOnlyList<ListItem> Items { get; private set; } = [];

        /// <summary>
        /// Gets the stored value.
        /// </summary>
        protected string SelectedValue { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the item that matches the stored value, or <c>null</c>.
        /// </summary>
        protected ListItem? SelectedItem
            => Items.FirstOrDefault(i => string.Equals(i.Value, SelectedValue, StringComparison.Ordinal));

        /// <inheritdoc />
        protected override void ApplyMetadata()
        {
            Items = FormField?.ListItems is { } items ? items.ToList() : [];
        }

        /// <inheritdoc />
        protected override void RefreshFromSource()
        {
            SelectedValue = GetValue();
        }

        /// <summary>
        /// Writes the value the user chose back to the bound field.
        /// </summary>
        /// <param name="value">The chosen item's value.</param>
        protected void CommitValue(string? value)
        {
            WriteBack(value);
            RefreshFromSource();
        }
    }
}
