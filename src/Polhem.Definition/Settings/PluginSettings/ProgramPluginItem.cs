using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Xml.Serialization;
using Polhem.Core.Attributes;
using Polhem.Core.Collections;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// The plugin chain bound to one progId.
    /// </summary>
    [Description("Plugins bound to one program.")]
    [TreeNode]
    public sealed class ProgramPluginItem : KeyCollectionItem
    {
        private PluginItemCollection? _plugins = null;

        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="ProgramPluginItem"/>.
        /// </summary>
        public ProgramPluginItem()
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="ProgramPluginItem"/>.
        /// </summary>
        /// <param name="progId">The program ID.</param>
        public ProgramPluginItem(string progId)
        {
            ProgId = progId;
        }

        #endregion

        /// <summary>
        /// Gets or sets the program ID.
        /// </summary>
        [XmlAttribute]
        [Description("Program ID.")]
        public string ProgId
        {
            get { return base.Key; }
            set { base.Key = value; }
        }

        /// <summary>
        /// Gets the ordered plugin chain of this program.
        /// </summary>
        /// <remarks>
        /// Declaration order is execution order, and it is the only ordering mechanism — there is
        /// deliberately no priority number, which in practice degenerates into a 10/20/30 ledger
        /// nobody can reason about.
        /// </remarks>
        [Description("Plugin collection.")]
        [DefaultValue(null)]
        public PluginItemCollection? Plugins
        {
            get
            {
                if (_plugins == null) { _plugins = new PluginItemCollection(this); }
                return _plugins;
            }
        }

        /// <summary>
        /// Gets whether <see cref="Plugins"/> is written; <c>false</c> while it is empty. <c>XmlSerializer</c>
        /// and <see cref="Polhem.Core.Serialization.JsonCodec"/> read this <c>{Property}Specified</c> member.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool PluginsSpecified => _plugins is { Count: > 0 };

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            return $"{this.ProgId} ({this.Plugins?.Count ?? 0})";
        }
    }
}
