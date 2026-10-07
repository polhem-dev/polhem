namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// What a <see cref="PropertyGridText"/> passed to <see cref="PropertyGridControl.LabelTranslator"/> is.
    /// </summary>
    public enum PropertyGridTextKind
    {
        /// <summary>A category header, from <c>[Category]</c>.</summary>
        Category,

        /// <summary>The label of a property, from <c>[DisplayName]</c> or the property name.</summary>
        DisplayName,

        /// <summary>The description of a property, from <c>[Description]</c>.</summary>
        Description,

        /// <summary>
        /// The display format of an item in the list of <see cref="CollectionEditDialog"/>, from the item type's
        /// <see cref="Polhem.Definition.Attributes.TreeNodeAttribute"/>: a literal label or a composite format string.
        /// </summary>
        ItemLabel,
    }
}
