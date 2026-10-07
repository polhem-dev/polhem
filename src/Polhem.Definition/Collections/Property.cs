using System.ComponentModel;
using System.Xml.Serialization;
using Polhem.Core.Collections;

namespace Polhem.Definition.Collections
{
    /// <summary>
    /// A custom property.
    /// </summary>
    [Description("Custom property.")]
    public sealed class Property : KeyCollectionItem
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="Property"/>.
        /// </summary>
        public Property()
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="Property"/>.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The property value.</param>
        public Property(string name, string value)
        {
            Name = name;
            Value = value;
        }

        #endregion

        /// <summary>
        /// Gets or sets the property name.
        /// </summary>
        [XmlAttribute]
        [Description("Property name.")]
        [Category(PropertyCategories.Data)]
        public string Name
        {
            get { return base.Key; }
            set { base.Key = value; }
        }

        /// <summary>
        /// Gets or sets the property value.
        /// </summary>
        [XmlAttribute]
        [Description("Property value.")]
        [Category(PropertyCategories.Data)]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            return $"{Name}={Value}";
        }
    }
}
