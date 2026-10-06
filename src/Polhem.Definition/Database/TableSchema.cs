using Polhem.Definition.Attributes;
using Polhem.Core.Serialization;
using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Polhem.Definition.Database
{
    /// <summary>
    /// Table schema.
    /// </summary>
    [Description("Table schema.")]
    [TreeNode]
    public sealed class TableSchema : IObjectSerializeFile
    {
        private DbFieldCollection? _fields = null;
        private DbTableIndexCollection? _indexes = null;

        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="TableSchema"/>.
        /// </summary>
        public TableSchema()
        {
        }

        #endregion

        #region IObjectSerializeFile Interface

        /// <summary>
        /// Gets the serialization-bound file path.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        public string ObjectFilePath { get; private set; } = string.Empty;

        /// <summary>
        /// Sets the serialization-bound file path.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        public void SetObjectFilePath(string filePath)
        {
            ObjectFilePath = filePath;
        }

        #endregion

        /// <summary>
        /// Gets the object creation time.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        public DateTime CreateTime { get; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the table name.
        /// </summary>
        [XmlAttribute]
        [Category(PropertyCategories.Data)]
        [NotifyParentProperty(true)]
        [Description("Table name.")]
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        [XmlAttribute]
        [Category(PropertyCategories.Data)]
        [NotifyParentProperty(true)]
        [Description("Display name.")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets the field collection.
        /// </summary>
        [Description("Field collection.")]
        [Browsable(false)]
        [DefaultValue(null)]
        public DbFieldCollection? Fields
        {
            get
            {
                if (_fields == null) { _fields = new DbFieldCollection(this); }
                return _fields;
            }
        }

        /// <summary>
        /// Gets whether <see cref="Fields"/> is written; <c>false</c> while it is empty. <c>XmlSerializer</c>
        /// and <see cref="Polhem.Core.Serialization.JsonCodec"/> read this <c>{Property}Specified</c> member.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool FieldsSpecified => _fields is { Count: > 0 };

        /// <summary>
        /// Gets the index collection.
        /// </summary>
        [Description("Index collection.")]
        [Browsable(false)]
        [DefaultValue(null)]
        public DbTableIndexCollection? Indexes
        {
            get
            {
                if (_indexes == null) { _indexes = new DbTableIndexCollection(this); }
                return _indexes;
            }
        }

        /// <summary>
        /// Gets whether <see cref="Indexes"/> is written; <c>false</c> while it is empty. <c>XmlSerializer</c>
        /// and <see cref="Polhem.Core.Serialization.JsonCodec"/> read this <c>{Property}Specified</c> member.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IndexesSpecified => _indexes is { Count: > 0 };

        /// <summary>
        /// Gets the primary key index.
        /// </summary>
        public DbTableIndex? GetPrimaryKey()
        {
            foreach (DbTableIndex index in Indexes!)
            {
                if (index.PrimaryKey)
                    return index;
            }
            return null;
        }

        /// <summary>
        /// Creates a copy of this instance.
        /// </summary>
        public TableSchema Clone()
        {
            var table = new TableSchema();
            table.TableName = TableName;
            table.DisplayName = DisplayName;
            foreach (DbTableIndex index in Indexes!)
                table.Indexes!.Add(index.Clone());
            foreach (DbField field in Fields!)
                table.Fields!.Add(field.Clone());
            return table;
        }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            return $"{TableName} - {DisplayName}";
        }
    }
}
