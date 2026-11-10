using Polhem.Core.Data;
using Polhem.Definition.Attributes;
using Polhem.Definition.Identity;
using Polhem.Definition.Sorting;
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
    [TreeNode("{0} - {1}", "TableName,DisplayName")]
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
        /// Makes this a table whose rows belong to a company: adds <see cref="SysFields.CompanyId"/> after
        /// <see cref="SysFields.RowId"/> when it is missing, and leads with it every unique index other than
        /// the row identity indexes (on <see cref="SysFields.No"/> or <see cref="SysFields.RowId"/> alone).
        /// </summary>
        /// <returns><c>true</c> when the table changed.</returns>
        /// <remarks>
        /// Companies sharing one company database may reuse a number or any other business key, so such a
        /// key is unique within one company only. <see cref="TableSchemaGenerator"/> applies this to every
        /// table of a company form schema. WARNING: a table schema served by the definition cache is shared;
        /// apply this to a <see cref="Clone"/>.
        /// </remarks>
        public bool ApplyCompanyScope()
        {
            bool changed = false;
            var fields = Fields!;
            if (!fields.Contains(SysFields.CompanyId))
            {
                int rowIdIndex = fields.Contains(SysFields.RowId) ? fields.IndexOf(fields[SysFields.RowId]) : -1;
                fields.Insert(rowIdIndex + 1, new DbField(SysFields.CompanyId, "Company Id", FieldDbType.String)
                {
                    Length = CompanyInfo.CompanyIdMaxLength
                });
                changed = true;
            }
            foreach (var index in Indexes!.Where(NeedsCompanyLead))
            {
                index.IndexFields!.Insert(0, new IndexField(SysFields.CompanyId, SortDirection.Asc));
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Determines whether this table already carries what <see cref="ApplyCompanyScope"/> adds.
        /// </summary>
        public bool HasCompanyScope()
            => Fields!.Contains(SysFields.CompanyId) && !Indexes!.Any(NeedsCompanyLead);

        private static bool NeedsCompanyLead(DbTableIndex index)
        {
            if (!index.Unique || index.IndexFields is not { Count: > 0 } keys) return false;
            if (string.Equals(keys[0].FieldName, SysFields.CompanyId, StringComparison.OrdinalIgnoreCase)) return false;
            bool rowIdentity = keys.Count == 1
                && (string.Equals(keys[0].FieldName, SysFields.No, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(keys[0].FieldName, SysFields.RowId, StringComparison.OrdinalIgnoreCase));
            return !rowIdentity;
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
