using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Xml.Serialization;
using Polhem.Definition.Attributes;
using Polhem.Core.Collections;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// A permission target model (an aggregate-root business entity), declaring the actions
    /// it supports and the default record-scope strategy per action. The model id is a
    /// PascalCase business entity name, deliberately distinct from a form's progId.
    /// </summary>
    [Description("Permission model.")]
    [TreeNode("{0} - {1}", "ModelId,DisplayName")]
    public sealed class PermissionModel : KeyCollectionItem
    {
        private PermissionRuleCollection? _rules = null;

        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="PermissionModel"/>.
        /// </summary>
        public PermissionModel()
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="PermissionModel"/>.
        /// </summary>
        /// <param name="modelId">The model id (PascalCase business entity).</param>
        /// <param name="displayName">The display name.</param>
        public PermissionModel(string modelId, string displayName)
        {
            ModelId = modelId;
            DisplayName = displayName;
        }

        #endregion

        /// <summary>
        /// Gets or sets the model id (PascalCase business entity, e.g. <c>"PurchaseOrder"</c>).
        /// </summary>
        [XmlAttribute]
        [Description("Model id.")]
        public string ModelId
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
        /// Gets the per-action permission rule collection.
        /// </summary>
        [Description("Permission rule collection.")]
        [Browsable(false)]
        [DefaultValue(null)]
        public PermissionRuleCollection? Rules
        {
            get
            {
                if (_rules == null) { _rules = new PermissionRuleCollection(this); }
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
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            return $"{this.ModelId} - {this.DisplayName}";
        }
    }
}
