using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Xml.Serialization;
using Polhem.Base.Attributes;
using Polhem.Base.Collections;

namespace Polhem.Definition.Layouts
{
    /// <summary>
    /// A grid layout for tabular data.
    /// </summary>
    [Description("Grid layout for tabular data.")]
    [TreeNode]
    public sealed class LayoutGrid : CollectionItem
    {
        private LayoutColumnCollection? _columns = null;

        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="LayoutGrid"/>.
        /// </summary>
        public LayoutGrid()
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="LayoutGrid"/>.
        /// </summary>
        /// <param name="tableName">The table name.</param>
        /// <param name="caption">The caption text.</param>
        public LayoutGrid(string tableName, string caption)
        {
            TableName = tableName;
            Caption = caption;
        }

        #endregion

        /// <summary>
        /// Gets or sets the table name.
        /// </summary>
        [Category(PropertyCategories.Data)]
        [XmlAttribute]
        [NotifyParentProperty(true)]
        [Description("Table name.")]
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the caption text.
        /// </summary>
        [XmlAttribute]
        [NotifyParentProperty(true)]
        [Description("Caption text.")]
        [DefaultValue("")]
        public string Caption { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the actions allowed on the grid control.
        /// </summary>
        [XmlAttribute]
        [Description("Actions allowed on the grid control.")]
        [DefaultValue(GridControlAllowActions.All)]
        public GridControlAllowActions AllowActions { get; set; } = GridControlAllowActions.All;

        /// <summary>
        /// Gets or sets the form modes in which this grid allows its editing
        /// actions. Combined with <see cref="AllowActions"/>: an action is effective
        /// only when its flag is granted and the current form mode is allowed here.
        /// </summary>
        [XmlAttribute]
        [Description("Form modes in which this grid allows editing actions.")]
        [DefaultValue(FormEditModes.All)]
        public FormEditModes AllowEditModes { get; set; } = FormEditModes.All;

        /// <summary>
        /// Gets the column collection.
        /// </summary>
        [Description("Column collection.")]
        [Browsable(false)]
        [DefaultValue(null)]
        public LayoutColumnCollection? Columns
        {
            get
            {
                if (_columns == null) { _columns = []; }
                return _columns;
            }
        }

        /// <summary>
        /// Gets whether <see cref="Columns"/> is written; <c>false</c> while it is empty. <c>XmlSerializer</c>
        /// and <see cref="Polhem.Base.Serialization.JsonCodec"/> read this <c>{Property}Specified</c> member.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ColumnsSpecified => _columns is { Count: > 0 };

        /// <summary>
        /// Creates a fully independent copy of this grid, including its columns.
        /// </summary>
        /// <returns>A new <see cref="LayoutGrid"/> sharing no mutable state with this one.</returns>
        public LayoutGrid Clone()
        {
            var copy = new LayoutGrid(TableName, Caption)
            {
                AllowActions = AllowActions,
                AllowEditModes = AllowEditModes,
            };
            // Reads the backing field, not the public getter: the getter reports null while a
            // serialization pass is in progress.
            if (_columns != null)
                foreach (var column in _columns)
                    copy.Columns!.Add(column.Clone());
            return copy;
        }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            return $"{TableName} - {Caption}";
        }
    }
}
