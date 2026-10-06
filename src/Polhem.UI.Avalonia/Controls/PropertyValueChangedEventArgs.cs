using System.ComponentModel;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// Provides data for <see cref="PropertyGridControl.PropertyValueChanged"/>.
    /// </summary>
    public sealed class PropertyValueChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of <see cref="PropertyValueChangedEventArgs"/>.
        /// </summary>
        /// <param name="component">The object whose property changed.</param>
        /// <param name="property">The property that changed.</param>
        /// <param name="oldValue">The value before the change.</param>
        /// <param name="newValue">The value after the change.</param>
        public PropertyValueChangedEventArgs(object component, PropertyDescriptor property, object? oldValue, object? newValue)
        {
            ArgumentNullException.ThrowIfNull(component);
            ArgumentNullException.ThrowIfNull(property);
            Component = component;
            Property = property;
            OldValue = oldValue;
            NewValue = newValue;
        }

        /// <summary>
        /// Gets the object whose property changed.
        /// </summary>
        public object Component { get; }

        /// <summary>
        /// Gets the property that changed.
        /// </summary>
        public PropertyDescriptor Property { get; }

        /// <summary>
        /// Gets the value before the change.
        /// </summary>
        public object? OldValue { get; }

        /// <summary>
        /// Gets the value after the change, as the property returns it once written.
        /// </summary>
        public object? NewValue { get; }
    }
}
