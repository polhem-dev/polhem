namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// A text <see cref="PropertyGridControl"/> shows from an annotation, with what it belongs to, as
    /// <see cref="PropertyGridControl.LabelTranslator"/> receives it.
    /// </summary>
    /// <remarks>
    /// The text alone does not identify a description: many properties share a description, and the same text can mean
    /// different things on different types. <see cref="ComponentType"/> and <see cref="PropertyName"/> let a translator
    /// build a resource key from the property itself.
    /// </remarks>
    public sealed class PropertyGridText
    {
        /// <summary>
        /// Initializes a new instance of <see cref="PropertyGridText"/>.
        /// </summary>
        /// <param name="kind">What the text is.</param>
        /// <param name="componentType">The type of the object the text belongs to.</param>
        /// <param name="propertyName">The name of the property the text belongs to, or <c>null</c>.</param>
        /// <param name="text">The text as written in the annotation.</param>
        public PropertyGridText(PropertyGridTextKind kind, Type componentType, string? propertyName, string text)
        {
            ArgumentNullException.ThrowIfNull(componentType);
            ArgumentNullException.ThrowIfNull(text);
            Kind = kind;
            ComponentType = componentType;
            PropertyName = propertyName;
            Text = text;
        }

        /// <summary>
        /// Gets what the text is.
        /// </summary>
        public PropertyGridTextKind Kind { get; }

        /// <summary>
        /// Gets the type of the object the text belongs to: the type of the object the grid shows, not the type that
        /// declares the property, so an inherited property can be translated per type. For
        /// <see cref="PropertyGridTextKind.ItemLabel"/> it is the type of the item.
        /// </summary>
        public Type ComponentType { get; }

        /// <summary>
        /// Gets the name of the property the text belongs to (<see cref="System.ComponentModel.MemberDescriptor.Name"/>,
        /// not its display name), or <c>null</c> for <see cref="PropertyGridTextKind.Category"/> and
        /// <see cref="PropertyGridTextKind.ItemLabel"/>.
        /// </summary>
        public string? PropertyName { get; }

        /// <summary>
        /// Gets the text as written in the annotation.
        /// </summary>
        public string Text { get; }
    }
}
