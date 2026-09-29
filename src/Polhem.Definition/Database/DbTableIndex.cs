using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Xml.Serialization;
using Polhem.Core.Attributes;
using Polhem.Core.Collections;

namespace Polhem.Definition.Database
{
    /// <summary>
    /// Table index schema.
    /// </summary>
    [Description("Table index schema.")]
    [TreeNode]
    public sealed class DbTableIndex : KeyCollectionItem
    {
        private IndexFieldCollection? _indexFields = null;

        /// <summary>
        /// Gets or sets the index name.
        /// </summary>
        [XmlAttribute]
        [Category(PropertyCategories.Data)]
        [NotifyParentProperty(true)]
        [Description("Index name.")]
        public string Name
        {
            get { return base.Key; }
            set { base.Key = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the index is unique.
        /// </summary>
        [XmlAttribute]
        [Category(PropertyCategories.Data)]
        [Description("Indicates whether the index is unique.")]
        [DefaultValue(false)]
        public bool Unique { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether this is the primary key.
        /// </summary>
        [XmlAttribute]
        [Category(PropertyCategories.Data)]
        [Description("Indicates whether this is the primary key.")]
        [DefaultValue(false)]
        public bool PrimaryKey { get; set; } = false;

        /// <summary>
        /// Gets the index field collection.
        /// </summary>
        [Description("Index field collection.")]
        [Browsable(false)]
        [DefaultValue(null)]
        public IndexFieldCollection? IndexFields
        {
            get
            {
                if (_indexFields == null) { _indexFields = []; }
                return _indexFields;
            }
        }

        /// <summary>
        /// Gets whether <see cref="IndexFields"/> is written; <c>false</c> while it is empty. <c>XmlSerializer</c>
        /// and <see cref="Polhem.Core.Serialization.JsonCodec"/> read this <c>{Property}Specified</c> member.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IndexFieldsSpecified => _indexFields is { Count: > 0 };

        /// <summary>
        /// Creates a copy of this instance.
        /// </summary>
        public DbTableIndex Clone()
        {
            var index = new DbTableIndex();
            index.Name = Name;
            index.PrimaryKey = PrimaryKey;
            index.Unique = Unique;
            foreach (IndexField indexField in IndexFields!)
                index.IndexFields!.Add(indexField.Clone());
            return index;
        }

        /// <summary>
        /// Compares whether the schema is identical to another instance.
        /// </summary>
        /// <param name="source">The source object to compare against.</param>
        /// <param name="databaseType">The database type used to determine sort-direction semantics
        /// (only SQL Server distinguishes ASC/DESC at the index-field level).</param>
        public bool Compare(DbTableIndex source, DatabaseType databaseType)
        {
            // Uniqueness differs, return false
            if (Unique != source.Unique) { return false; }
            // Index field count differs, return false
            if (IndexFields!.Count != source.IndexFields!.Count) { return false; }
            // Compare each index field schema
            foreach (IndexField indexField in IndexFields)
            {
                // Index field does not exist, return false
                if (!source.IndexFields.Contains(indexField.FieldName)) { return false; }
                // Sort direction differs, return false
                if (databaseType == DatabaseType.SQLServer && indexField.SortDirection != source.IndexFields[indexField.FieldName].SortDirection) { return false; }
            }
            return true;
        }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            return Name;
        }
    }
}
