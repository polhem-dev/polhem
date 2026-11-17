using Polhem.Definition.Layouts;
using System.ComponentModel;
using System.Xml.Serialization;
using Polhem.Core;
using Polhem.Definition.Attributes;
using Polhem.Core.Serialization;
using System.Text.Json.Serialization;

namespace Polhem.Definition.Forms
{
    /// <summary>
    /// Form schema definition.
    /// </summary>
    [Description("Form schema definition.")]
    [TreeNode("{0} - {1}", "ProgId,DisplayName")]
    public sealed class FormSchema : IObjectSerializeFile
    {
        private FormTableCollection? _tables = null;
        private FormRuleCollection? _rules = null;

        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="FormSchema"/>.
        /// </summary>
        public FormSchema()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="FormSchema"/>.
        /// </summary>
        /// <param name="progId">The program ID.</param>
        /// <param name="displayName">The display name.</param>
        public FormSchema(string progId, string displayName)
        {
            ProgId= progId;
            DisplayName = displayName;
        }

        #endregion

        #region IObjectSerializeFile Interface

        /// <summary>
        /// Gets the file path bound to serialization.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        public string ObjectFilePath { get; private set; } = string.Empty;

        /// <summary>
        /// Sets the file path bound to serialization.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        public void SetObjectFilePath(string filePath)
        {
            ObjectFilePath = filePath;
        }

        #endregion

        /// <summary>
        /// Gets the time at which this object was created.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        public DateTime CreateTime { get; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the program ID.
        /// </summary>
        [XmlAttribute()]
        [Description("Program ID.")]
        [Category(PropertyCategories.Data)]
        public string ProgId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        [XmlAttribute]
        [Category(PropertyCategories.Data)]
        [Description("Display name.")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the database category id (required).
        /// Determines which <see cref="Settings.DbCategory"/> the tables in this schema
        /// belong to, and thus where their generated <see cref="Database.TableSchema"/>
        /// files are persisted.
        /// </summary>
        [XmlAttribute]
        [Category(PropertyCategories.Data)]
        [Description("Database category id (required).")]
        public string CategoryId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the master field that holds the document currency code (by
        /// convention <c>sys_currency</c>). Amount fields with no explicit
        /// <see cref="FormField.CurrencyField"/> resolve their currency from this field; detail amount
        /// fields read the master row's value. Empty falls back to the company default currency.
        /// </summary>
        [XmlAttribute]
        [Category(PropertyCategories.Data)]
        [Description("Name of the master field holding the document currency code (SAP CUKY, convention sys_currency).")]
        [DefaultValue("")]
        public string CurrencyField { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list field collection string, with multiple fields separated by commas.
        /// </summary>
        [XmlAttribute]
        [Category(PropertyCategories.Data)]
        [Description("List field collection string, with multiple fields separated by commas.")]
        public string ListFields { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the field collection string exposed to lookup queries, with
        /// multiple fields separated by commas. Declares which fields this form returns
        /// when other forms open it as a lookup source; the server enforces this set.
        /// </summary>
        /// <remarks>
        /// When empty, lookup queries fall back to <c>sys_id</c> and <c>sys_name</c>
        /// (skipping any that the master table does not define). The response always
        /// includes <c>sys_rowid</c> regardless of this declaration.
        /// </remarks>
        [XmlAttribute]
        [Category(PropertyCategories.Data)]
        [Description("Field collection string exposed to lookup queries, with multiple fields separated by commas.")]
        [DefaultValue("")]
        public string LookupFields { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the fields a user can add query conditions for on this form's list, with multiple fields
        /// separated by commas.
        /// </summary>
        /// <remarks>
        /// When empty, the list's <see cref="ListFields"/> are offered. <see cref="GetQueryFields"/> resolves the
        /// value: only master-table fields count, relation display fields (<c>ref_*</c>) included. The server does not
        /// enforce this set; it checks every filter field against the table and refuses protected columns whatever the
        /// list says.
        /// </remarks>
        [XmlAttribute]
        [Category(PropertyCategories.Data)]
        [Description("Fields a user can add query conditions for on the list, with multiple fields separated by commas. Empty offers the ListFields.")]
        [DefaultValue("")]
        public string QueryFields { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the permission model id that this form's main aggregate maps to.
        /// References a <see cref="Polhem.Definition.Settings.PermissionModel.ModelId"/> in the permission registry; the
        /// backend method-level enforcement uses it to resolve the (model, action) to check.
        /// Empty means the form declares no permission model (enforcement is skipped).
        /// </summary>
        [XmlAttribute]
        [Category(PropertyCategories.Data)]
        [Description("Permission model id mapped to this form's main aggregate.")]
        public string PermissionModelId { get; set; } = string.Empty;

        /// <summary>
        /// Gets the table collection.
        /// </summary>
        [Description("Table collection.")]
        [DefaultValue(null)]
        [Browsable(false)]
        public FormTableCollection? Tables
        {
            get
            {
                if (_tables == null) { _tables = new FormTableCollection(this); }
                return _tables;
            }
        }

        /// <summary>
        /// Gets whether <see cref="Tables"/> is written; <c>false</c> while it is empty. <c>XmlSerializer</c>
        /// and <see cref="Polhem.Core.Serialization.JsonCodec"/> read this <c>{Property}Specified</c> member.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool TablesSpecified => _tables is { Count: > 0 };

        /// <summary>
        /// Gets the master table.
        /// </summary>
        /// <remarks>
        /// Excluded from JSON serialization because the value is always equal to
        /// <c>Tables[ProgId]</c>; emitting it would duplicate the master table
        /// payload for JSON consumers (notably the JS Plain wire format).
        /// </remarks>
        [Browsable(false)]
        [TreeNodeIgnore]
        [JsonIgnore]
        public FormTable? MasterTable
        {
            get
            {
                if (StringUtilities.IsEmpty(this.ProgId) || !this.Tables!.Contains(this.ProgId))
                    return null;
                else
                    return this.Tables[this.ProgId];
            }
        }

        /// <summary>
        /// Gets the business rule collection evaluated by the rule engine at save/delete
        /// lifecycle points (field validation and lifecycle guards). Field computation and
        /// default-value expressions are declared on the fields themselves
        /// (<see cref="FormField.ValueExpression"/> / <see cref="FormField.DefaultValueExpression"/>).
        /// </summary>
        [Description("Business rule collection.")]
        [DefaultValue(null)]
        [Browsable(false)]
        public FormRuleCollection? Rules
        {
            get
            {
                if (_rules == null) { _rules = new FormRuleCollection(this); }
                return _rules;
            }
        }

        /// <summary>
        /// Gets whether <see cref="Rules"/> is written; <c>false</c> while it is empty. <c>XmlSerializer</c>
        /// and <see cref="Polhem.Core.Serialization.JsonCodec"/> read this <c>{Property}Specified</c> member.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool RulesSpecified => _rules is { Count: > 0 };

        /// <summary>
        /// Finds a field definition by name, treating this schema as the single source of truth
        /// for field metadata (such as <see cref="FormField.SensitiveCategory"/>) that is not
        /// copied onto the generated layout. Searches the master table when <paramref name="tableName"/>
        /// is empty, otherwise the named table. Returns <c>null</c> when the table or field is absent.
        /// </summary>
        /// <param name="fieldName">The field name to find.</param>
        /// <param name="tableName">The owning table name; empty resolves to the master table.</param>
        public FormField? FindField(string fieldName, string tableName = "")
        {
            FormTable? table;
            if (StringUtilities.IsEmpty(tableName))
                table = MasterTable;
            else
                table = Tables != null && Tables.Contains(tableName) ? Tables[tableName] : null;
            if (table?.Fields == null || !table.Fields.Contains(fieldName)) { return null; }
            return table.Fields[fieldName];
        }

        /// <summary>
        /// Gets the list layout for this form schema.
        /// </summary>
        public LayoutGrid GetListLayout()
            => ListLayoutGenerator.Generate(this);

        /// <summary>
        /// Gets the lookup layout for this form schema: one column per resolved
        /// lookup field plus a hidden <c>sys_rowid</c> column, for lookup picker windows.
        /// </summary>
        public LayoutGrid GetLookupLayout()
            => LookupLayoutGenerator.Generate(this);

        /// <summary>
        /// Resolves the lookup field set this form exposes to lookup queries.
        /// Fields declared in <see cref="LookupFields"/> win; an empty declaration
        /// falls back to <c>sys_id</c> and <c>sys_name</c>. Only fields defined on
        /// the master table are returned (others are skipped); <c>sys_rowid</c> is
        /// excluded because callers always prepend it to the projection.
        /// </summary>
        public IReadOnlyList<FormField> GetLookupFields()
        {
            var fields = new List<FormField>();
            var master = MasterTable;
            if (master?.Fields == null) { return fields; }

            var names = StringUtilities.IsNotEmpty(LookupFields)
                ? LookupFields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [SysFields.Id, SysFields.Name];

            foreach (var name in names)
            {
                if (StringUtilities.IsEquals(name, SysFields.RowId)) { continue; }
                if (master.Fields.Contains(name) &&
                    !fields.Any(f => StringUtilities.IsEquals(f.FieldName, name)))
                {
                    fields.Add(master.Fields[name]);
                }
            }
            return fields;
        }

        /// <summary>
        /// Resolves the fields a user can add query conditions for on this form's list.
        /// Fields declared in <see cref="QueryFields"/> win; an empty declaration falls back to
        /// <see cref="ListFields"/>, and when both are empty no field is offered. Only fields defined on the master
        /// table are returned, in the declared order and once each; <c>sys_rowid</c> and the stored columns
        /// <see cref="ProtectedFields"/> lists are left out.
        /// </summary>
        /// <remarks>
        /// Every other field the master table declares is returned, whatever its type; a front end skips the ones it
        /// offers no condition for. A relation field that reads a protected column of the related table is not
        /// detected here, because that needs the query's joins; the server refuses a filter on it.
        /// </remarks>
        public IReadOnlyList<FormField> GetQueryFields()
        {
            var fields = new List<FormField>();
            var master = MasterTable;
            if (master?.Fields == null) { return fields; }

            var declared = StringUtilities.IsNotEmpty(QueryFields) ? QueryFields : ListFields;
            var dbTableName = string.IsNullOrWhiteSpace(master.DbTableName) ? master.TableName : master.DbTableName;
            foreach (var name in declared.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (StringUtilities.IsEquals(name, SysFields.RowId)) { continue; }
                if (!master.Fields.Contains(name)) { continue; }
                var field = master.Fields[name];
                if (field.Type == Database.FieldType.DbField && ProtectedFields.IsProtected(dbTableName, field.FieldName)) { continue; }
                if (!fields.Any(f => StringUtilities.IsEquals(f.FieldName, field.FieldName)))
                {
                    fields.Add(field);
                }
            }
            return fields;
        }

        /// <summary>
        /// Creates a deep copy of this instance. Use this whenever a per-session
        /// view of a cached <see cref="FormSchema"/> must be mutated (e.g. before
        /// applying language-specific localization).
        /// </summary>
        /// <remarks>
        /// Cached <see cref="FormSchema"/> instances returned by
        /// <see cref="Storage.IDefineAccess.GetFormSchema"/> are shared across
        /// every session in the process — see <c>docs/en/architecture/development-constraints.md</c>
        /// § <i>Cached Data Immutability After Init</i>. Mutating without
        /// cloning first leaks state across sessions and races under concurrency.
        /// </remarks>
        public FormSchema Clone()
        {
            var copy = new FormSchema(ProgId, DisplayName)
            {
                CategoryId = CategoryId,
                CurrencyField = CurrencyField,
                ListFields = ListFields,
                LookupFields = LookupFields,
                QueryFields = QueryFields,
                PermissionModelId = PermissionModelId,
            };
            if (_tables != null)
                foreach (var table in _tables)
                    copy.Tables!.Add(table.Clone());
            if (_rules != null)
                foreach (var rule in _rules)
                    copy.Rules!.Add(rule.Clone());
            return copy;
        }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            return $"{this.ProgId} - {this.DisplayName}";
        }
    }
}
