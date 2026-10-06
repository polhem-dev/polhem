using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Xml.Serialization;
using Polhem.Definition.Attributes;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// A grouping node. Owns child nodes and references no program of its own.
    /// </summary>
    [Description("Menu folder (grouping node).")]
    [TreeNode]
    public sealed class MenuFolder : MenuNodeBase
    {
        private MenuNodeCollection? _items = null;

        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="MenuFolder"/>.
        /// </summary>
        public MenuFolder()
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="MenuFolder"/>.
        /// </summary>
        /// <param name="id">The node ID.</param>
        /// <param name="caption">The caption.</param>
        public MenuFolder(string id, string caption)
        {
            Id = id;
            Caption = caption;
        }

        #endregion

        /// <summary>
        /// Gets the child node collection.
        /// </summary>
        /// <remarks>
        /// Each subtype is declared with its own <see cref="XmlArrayItemAttribute"/> so the
        /// serializer writes <c>&lt;MenuFolder&gt;</c> and <c>&lt;MenuEntry&gt;</c> elements.
        /// Without the per-subtype declarations it falls back to one element name plus an
        /// <c>xsi:type</c> discriminator, which is markedly harder to read and to hand-edit.
        /// </remarks>
        [Description("Child node collection.")]
        [Browsable(false)]
        [DefaultValue(null)]
        [XmlArrayItem(typeof(MenuFolder))]
        [XmlArrayItem(typeof(MenuEntry))]
        public MenuNodeCollection? Items
        {
            get
            {
                if (_items == null) { _items = new MenuNodeCollection(this); }
                return _items;
            }
        }

        /// <summary>
        /// Gets whether <see cref="Items"/> is written; <c>false</c> while it is empty. <c>XmlSerializer</c>
        /// and <see cref="Polhem.Core.Serialization.JsonCodec"/> read this <c>{Property}Specified</c> member.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ItemsSpecified => _items is { Count: > 0 };

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            return $"{this.Id} - {this.Caption}";
        }
    }
}
