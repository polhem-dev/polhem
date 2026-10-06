using System.Collections;
using System.ComponentModel;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// Describes a collection property to edit: the object that owns it, the property, the collection and the type of
    /// its items.
    /// </summary>
    /// <remarks>
    /// <see cref="PropertyGridControl"/> passes one to <see cref="PropertyGridControl.CollectionEditorProvider"/> when the
    /// user asks to edit a collection, and <see cref="CollectionEditDialog.ShowAsync(global::Avalonia.Visual, CollectionEditContext, CancellationToken)"/>
    /// edits the collection it describes.
    /// </remarks>
    public sealed class CollectionEditContext
    {
        /// <summary>
        /// Initializes a new instance of <see cref="CollectionEditContext"/> for <paramref name="property"/> of
        /// <paramref name="component"/>.
        /// </summary>
        /// <param name="component">The object that owns the collection.</param>
        /// <param name="property">The collection property.</param>
        /// <exception cref="ArgumentException">The property's value on <paramref name="component"/> is not an <see cref="IList"/>.</exception>
        public CollectionEditContext(object component, PropertyDescriptor property)
        {
            ArgumentNullException.ThrowIfNull(component);
            ArgumentNullException.ThrowIfNull(property);
            Component = component;
            Property = property;
            Collection = property.GetValue(component) as IList
                ?? throw new ArgumentException($"The value of {property.Name} is not an IList.", nameof(property));
            ItemType = PropertyGridMetadata.GetItemType(Collection.GetType());
        }

        /// <summary>
        /// Gets the object that owns the collection.
        /// </summary>
        public object Component { get; }

        /// <summary>
        /// Gets the collection property.
        /// </summary>
        public PropertyDescriptor Property { get; }

        /// <summary>
        /// Gets the collection to edit. Its items are changed in place; the collection itself is never replaced.
        /// </summary>
        public IList Collection { get; }

        /// <summary>
        /// Gets the type of the collection's items, from its <see cref="Polhem.Core.Collections.CollectionBase{T}"/> or
        /// <see cref="Polhem.Core.Collections.KeyCollectionBase{T}"/> base or its <see cref="IList{T}"/> interface;
        /// <see cref="object"/> when none of them tells.
        /// </summary>
        public Type ItemType { get; }
    }
}
