using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Xml.Serialization;
using Polhem.Base.Attributes;
using Polhem.Base.Collections;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// A logical database category definition.
    /// </summary>
    [Description("Database category.")]
    [TreeNode]
    public class DbCategory : KeyCollectionItem
    {
        private TableItemCollection? _tables = null;

        /// <summary>
        /// Gets or sets the category id.
        /// </summary>
        [XmlAttribute]
        [Description("Category id.")]
        public string Id
        {
            get { return base.Key; }
            set { base.Key = value; }
        }

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        [XmlAttribute]
        [Description("Display name.")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets the table collection.
        /// </summary>
        [Description("Table collection.")]
        [Browsable(false)]
        [DefaultValue(null)]
        public TableItemCollection? Tables
        {
            get
            {
                if (_tables == null) { _tables = new TableItemCollection(this); }
                return _tables;
            }
        }

        /// <summary>
        /// Gets whether <see cref="Tables"/> is written; <c>false</c> while it is empty. <c>XmlSerializer</c>
        /// and <see cref="Polhem.Base.Serialization.JsonCodec"/> read this <c>{Property}Specified</c> member.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool TablesSpecified => _tables is { Count: > 0 };

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            return $"{Id} - {DisplayName}";
        }
    }
}
