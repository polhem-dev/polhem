using System.ComponentModel;
using System.Xml.Serialization;
using Polhem.Core.Collections;

namespace Polhem.Definition.Collections
{
    /// <summary>
    /// List item.
    /// </summary>
    public sealed class ListItem : KeyCollectionItem
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="ListItem"/>.
        /// </summary>
        public ListItem()
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="ListItem"/>.
        /// </summary>
        /// <param name="value">The item value.</param>
        /// <param name="text">The display text.</param>
        public ListItem(string value, string text)
        {
            this.Value = value;
            Text = text;
        }

        #endregion

        /// <summary>
        /// Gets or sets the item value.
        /// </summary>
        [XmlAttribute]
        [Description("Item value.")]
        [Category(PropertyCategories.Data)]
        public string Value
        {
            get { return base.Key; }
            set { base.Key = value; }
        }

        /// <summary>
        /// Gets or sets the display text.
        /// </summary>
        /// <remarks>
        /// When no text is set, the getter returns <see cref="Value"/>, so an item declared with only a value
        /// shows that value in every drop-down. The form workbook import reads a list item without a text the
        /// same way.
        /// </remarks>
        [XmlAttribute]
        [Description("Display text. When empty, the value is shown.")]
        [Category(PropertyCategories.Data)]
        public string Text
        {
            get { return string.IsNullOrEmpty(_text) ? Value : _text; }
            set { _text = value; }
        }
        private string _text = string.Empty;

        /// <summary>
        /// Creates a deep copy of this instance. Result is unparented (no owning collection).
        /// </summary>
        public ListItem Clone() => new(Value, Text);

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            return this.Text;
        }
    }
}
