namespace Polhem.Web.Blazor.Controls.Editors
{
    /// <summary>
    /// Base component of a check box bound to a Boolean field.
    /// </summary>
    public abstract class CheckEditorBase : FieldEditorBase
    {
        /// <summary>
        /// Gets a value indicating whether the box is checked.
        /// </summary>
        protected bool IsChecked { get; private set; }

        /// <inheritdoc />
        protected override void RefreshFromSource()
        {
            IsChecked = string.Equals(GetValue(), bool.TrueString, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Writes the state the user chose back to the bound field.
        /// </summary>
        /// <param name="value">The new state.</param>
        protected void CommitChecked(bool value)
        {
            WriteBack(value.ToString());
            RefreshFromSource();
        }
    }
}
